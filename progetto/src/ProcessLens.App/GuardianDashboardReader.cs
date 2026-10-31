using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ProcessLens.App;

// Snapshot-only read path. Never modify Guardian's database or the collector's lifecycle.
public sealed record GuardianEventView(string LocalTime, string Name, string Kind, int Pid, int ParentPid, string Source);
public sealed record GuardianTopProcessView(string Name, long Starts, double StartsPerMinute);
public sealed record GuardianActivityBucketView(string TimeLocal, long Starts, double PercentOfMax);
public sealed record GuardianParentView(int ParentPid, string CandidateParent, string ChildName, long Starts, string Evidence);
public sealed record GuardianResourceView(string Name, double AverageCpu, double PeakCpu, double PeakResidentMiB, long Samples);
public sealed record GuardianDiagnosticView(string LocalTime, string Source, string Message);
public sealed record GuardianDashboardSnapshot(
    DateTime ReadUtc, int WindowMinutes, long Started, long Exited, long TotalEvents,
    long EtwStarts, long EtwExits, long QueueWarnings, long StorageWarnings,
    DateTime? LastEtwUtc, long DatabaseBytes, long WalBytes,
    IReadOnlyList<GuardianEventView> Events,
    IReadOnlyList<GuardianTopProcessView> TopProcesses,
    IReadOnlyList<GuardianActivityBucketView> Trend,
    IReadOnlyList<GuardianParentView> Parents,
    IReadOnlyList<GuardianResourceView> Resources,
    IReadOnlyList<GuardianDiagnosticView> Diagnostics);

public static class GuardianDashboardReader
{
    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProcessLens", "Guardian", "guardian.sqlite3");

    public static GuardianDashboardSnapshot Read(string path, int windowMinutes = 30, int maxEvents = 250, DateTime? referenceUtc = null)
    {
        if (windowMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(windowMinutes));
        if (maxEvents is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(maxEvents));
        if (!File.Exists(path)) throw new FileNotFoundException(
            "Il database Guardian non esiste. Verificare che il servizio sia installato e abbia prodotto dati.", path);

        var now = (referenceUtc ?? DateTime.UtcNow).ToUniversalTime();
        long since = new DateTimeOffset(now.AddMinutes(-windowMinutes)).ToUnixTimeSeconds();
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = 2
        }.ToString());
        db.Open();
        using (var setup = db.CreateCommand())
        {
            setup.CommandTimeout = 2;
            setup.CommandText = "PRAGMA query_only=ON; PRAGMA busy_timeout=1500;";
            setup.ExecuteNonQuery();
        }

        SqliteCommand Command(string sql)
        {
            var command = db.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$since", since);
            return command;
        }

        long Count(string sql)
        {
            using var cmd = Command(sql);
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
        }

        long total = Count("SELECT COUNT(*) FROM process_events WHERE ts >= $since");
        long starts = Count("SELECT COUNT(*) FROM process_events WHERE ts >= $since AND kind='ProcessStarted'");
        long exited = Count("SELECT COUNT(*) FROM process_events WHERE ts >= $since AND kind='ProcessExited'");
        long etwStarts = Count("SELECT COUNT(*) FROM process_events WHERE ts >= $since AND source='etw' AND kind='ProcessStarted'");
        long etwExits = Count("SELECT COUNT(*) FROM process_events WHERE ts >= $since AND source='etw' AND kind='ProcessExited'");
        long queueWarnings = Count("SELECT COUNT(*) FROM diagnostics WHERE ts >= $since AND source='queue'");
        long storageWarnings = Count("SELECT COUNT(*) FROM diagnostics WHERE ts >= $since AND source='storage' AND message LIKE 'Lost % events due to failed SQLite transactions'");
        var latestUnix = Count("SELECT COALESCE(MAX(ts),0) FROM process_events WHERE ts >= $since AND source='etw'");
        DateTime? latestEtw = latestUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(latestUnix).UtcDateTime : null;

        var events = new List<GuardianEventView>();
        using (var cmd = Command("SELECT ts, name, kind, pid, ppid, source FROM process_events WHERE ts >= $since ORDER BY ts DESC, id DESC LIMIT $limit"))
        {
            cmd.Parameters.AddWithValue("$limit", maxEvents);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) events.Add(new GuardianEventView(
                LocalTime(reader.GetInt64(0)), reader.GetString(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetInt32(4), reader.GetString(5)));
        }

        var top = new List<GuardianTopProcessView>();
        using (var cmd = Command("SELECT name, COUNT(*) FROM process_events WHERE ts >= $since AND kind='ProcessStarted' AND source='etw' GROUP BY name ORDER BY COUNT(*) DESC LIMIT 18"))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
            {
                long count = reader.GetInt64(1);
                top.Add(new GuardianTopProcessView(reader.GetString(0), count, Math.Round((double)count / windowMinutes, 2)));
            }

        // A small, bounded histogram; no permanent history is retained in the WPF process.
        int bucketSeconds = Math.Max(60, windowMinutes * 60 / 12);
        var histogram = new Dictionary<long, long>();
        using (var cmd = Command("SELECT (ts / $period) * $period, COUNT(*) FROM process_events WHERE ts >= $since AND kind='ProcessStarted' AND source='etw' GROUP BY (ts / $period) ORDER BY (ts / $period)"))
        {
            cmd.Parameters.AddWithValue("$period", bucketSeconds);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) histogram[reader.GetInt64(0)] = reader.GetInt64(1);
        }
        long firstBucket = (since / bucketSeconds) * bucketSeconds;
        long lastBucket = (new DateTimeOffset(now).ToUnixTimeSeconds() / bucketSeconds) * bucketSeconds;
        long highest = histogram.Count == 0 ? 1 : Math.Max(1, histogram.Values.Max());
        var trend = new List<GuardianActivityBucketView>();
        for (long bucket = firstBucket; bucket <= lastBucket; bucket += bucketSeconds)
        {
            histogram.TryGetValue(bucket, out long count);
            trend.Add(new GuardianActivityBucketView(
                DateTimeOffset.FromUnixTimeSeconds(bucket).ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture),
                count, Math.Round(100.0 * count / highest, 1)));
        }

        var parents = new List<GuardianParentView>();
        // Read grouped child launches first, then look for a historical parent candidate.
        // Never present a PID-only parent lookup as proven ancestry.
        var groups = new List<(int Ppid, string Child, long Starts, long FirstTs)>();
        using (var cmd = Command("SELECT ppid, name, COUNT(*), MIN(ts) FROM process_events WHERE ts >= $since AND kind='ProcessStarted' AND source='etw' AND ppid > 0 GROUP BY ppid, name ORDER BY COUNT(*) DESC LIMIT 18"))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) groups.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3)));

        foreach (var group in groups)
        {
            string candidate = "Non identificato";
            string evidence = "Nessun evento padre compatibile";
            using var cmd = db.CreateCommand();
            cmd.CommandTimeout = 2;
            cmd.CommandText = "SELECT name, kind FROM process_events WHERE pid=$pid AND ts <= $childTime AND kind IN ('ProcessStarted','ProcessObservedAtStartup','ProcessExited') ORDER BY ts DESC,id DESC LIMIT 1";
            cmd.Parameters.AddWithValue("$pid", group.Ppid);
            cmd.Parameters.AddWithValue("$childTime", group.FirstTs);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                string parentName = reader.GetString(0), kind = reader.GetString(1);
                if (kind != "ProcessExited")
                {
                    candidate = parentName;
                    evidence = kind == "ProcessObservedAtStartup"
                        ? "Osservato allo startup; parentela non confermata"
                        : "Corrispondenza PID/orario; riuso PID possibile";
                }
                else evidence = "L'ultimo evento del PID padre e' una terminazione";
            }
            parents.Add(new GuardianParentView(group.Ppid, candidate, group.Child, group.Starts, evidence));
        }

        var resources = new List<GuardianResourceView>();
        using (var cmd = Command("""
            SELECT name, COALESCE(SUM(cpu_sum)/NULLIF(SUM(cpu_count),0),0),
              COALESCE(MAX(cpu_peak),0), COALESCE(MAX(resident_peak)/1048576.0,0), SUM(samples)
            FROM resources WHERE bucket >= ($since - 300)
            GROUP BY name HAVING SUM(cpu_count) > 0 ORDER BY SUM(cpu_sum)/NULLIF(SUM(cpu_count),0) DESC LIMIT 16
            """))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) resources.Add(new GuardianResourceView(reader.GetString(0),
                Math.Round(reader.GetDouble(1), 2), Math.Round(reader.GetDouble(2), 2),
                Math.Round(reader.GetDouble(3), 1), reader.GetInt64(4)));

        var diagnostics = new List<GuardianDiagnosticView>();
        using (var cmd = Command("SELECT ts,source,message FROM diagnostics WHERE ts >= $since ORDER BY ts DESC LIMIT 45"))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read()) diagnostics.Add(new GuardianDiagnosticView(
                LocalTime(reader.GetInt64(0)), reader.GetString(1), reader.GetString(2)));

        long Bytes(string file) => File.Exists(file) ? new FileInfo(file).Length : 0;
        return new GuardianDashboardSnapshot(now, windowMinutes, starts, exited, total,
            etwStarts, etwExits, queueWarnings, storageWarnings, latestEtw,
            Bytes(path), Bytes(path + "-wal"), events, top, trend, parents, resources, diagnostics);
    }

    private static string LocalTime(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
        .ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
}
