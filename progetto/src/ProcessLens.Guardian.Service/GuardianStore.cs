using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using ProcessLens.Guardian.Core;

namespace ProcessLens.Guardian.Service;

// Single writer: database access never blocks the ETW callback or the sampler.
// Parameterized statements + bounded fields. SQLite WAL supports read-only reporting.
public sealed record StorageMaintenanceResult(long QuotaBytes, long MainBytes, long WalBytes,
    long EstimatedLiveBytes, int PrunedHours, bool Vacuumed, bool CheckpointBusy, bool AboveSoftQuota);

public sealed class GuardianStore : IDisposable
{
    private readonly SqliteConnection _db;
    public string DatabasePath { get; }
    public GuardianStore(string directory)
    {
        Directory.CreateDirectory(directory);
        DatabasePath = Path.Combine(directory, "guardian.sqlite3");
        _db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=3000; PRAGMA synchronous=NORMAL; PRAGMA wal_autocheckpoint=1000;");
        Exec("""
            CREATE TABLE IF NOT EXISTS process_events (
              id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, kind TEXT NOT NULL,
              pid INTEGER NOT NULL, started INTEGER NOT NULL, name TEXT NOT NULL,
              ppid INTEGER NOT NULL, source TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_events_ts ON process_events(ts);
            CREATE INDEX IF NOT EXISTS ix_events_pid ON process_events(pid,started);
            CREATE TABLE IF NOT EXISTS resources (
              bucket INTEGER NOT NULL, pid INTEGER NOT NULL, started INTEGER NOT NULL, name TEXT NOT NULL,
              cpu_sum REAL NOT NULL DEFAULT 0, cpu_count INTEGER NOT NULL DEFAULT 0,
              cpu_peak REAL, resident_peak INTEGER, commit_peak INTEGER, samples INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY(bucket,pid,started));
            CREATE INDEX IF NOT EXISTS ix_resources_bucket ON resources(bucket);
            CREATE TABLE IF NOT EXISTS port_events (
              id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, action TEXT NOT NULL,
              pid INTEGER NOT NULL, protocol TEXT NOT NULL, address TEXT NOT NULL,
              port INTEGER NOT NULL, remote_address TEXT NOT NULL, remote_port INTEGER NOT NULL, state TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_ports_ts ON port_events(ts);
            CREATE TABLE IF NOT EXISTS diagnostics (
              ts INTEGER NOT NULL, source TEXT NOT NULL, message TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_diag_ts ON diagnostics(ts);
            """);
    }
    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql; cmd.ExecuteNonQuery();
    }
    public void SaveBatch(IReadOnlyList<GuardianEvent> events)
    {
        if (events.Count == 0) return;
        using var transaction = _db.BeginTransaction();
        foreach (var ev in events)
        {
            using var c = _db.CreateCommand(); c.Transaction = transaction;
            long ts = new DateTimeOffset(ev.Utc.ToUniversalTime()).ToUnixTimeSeconds();
            c.Parameters.AddWithValue("$ts", ts);
            c.Parameters.AddWithValue("$pid", ev.Pid);
            if (ev.Kind == GuardianKind.ResourceSample)
            {
                c.CommandText = """
                    INSERT INTO resources(bucket,pid,started,name,cpu_sum,cpu_count,cpu_peak,resident_peak,commit_peak,samples)
                    VALUES($bucket,$pid,$start,$name,$sum,$count,$peak,$resident,$commit,1)
                    ON CONFLICT(bucket,pid,started) DO UPDATE SET
                      name=excluded.name,
                      cpu_sum=resources.cpu_sum+excluded.cpu_sum,
                      cpu_count=resources.cpu_count+excluded.cpu_count,
                      cpu_peak=CASE WHEN resources.cpu_peak IS NULL THEN excluded.cpu_peak WHEN excluded.cpu_peak IS NULL THEN resources.cpu_peak ELSE MAX(resources.cpu_peak,excluded.cpu_peak) END,
                      resident_peak=CASE WHEN resources.resident_peak IS NULL THEN excluded.resident_peak WHEN excluded.resident_peak IS NULL THEN resources.resident_peak ELSE MAX(resources.resident_peak,excluded.resident_peak) END,
                      commit_peak=CASE WHEN resources.commit_peak IS NULL THEN excluded.commit_peak WHEN excluded.commit_peak IS NULL THEN resources.commit_peak ELSE MAX(resources.commit_peak,excluded.commit_peak) END,
                      samples=resources.samples+1;
                    """;
                c.Parameters.AddWithValue("$bucket", GuardianMath.FiveMinuteBucket(ev.Utc));
                c.Parameters.AddWithValue("$start", ev.ProcessStartTicks);
                c.Parameters.AddWithValue("$name", Clip(ev.Name, 180));
                c.Parameters.AddWithValue("$sum", ev.CpuPercent ?? 0);
                c.Parameters.AddWithValue("$count", ev.CpuPercent.HasValue ? 1 : 0);
                c.Parameters.AddWithValue("$peak", (object?)ev.CpuPercent ?? DBNull.Value);
                c.Parameters.AddWithValue("$resident", (object?)ev.ResidentWorkingSetBytes ?? DBNull.Value);
                c.Parameters.AddWithValue("$commit", (object?)ev.PrivateCommitBytes ?? DBNull.Value);
            }
            else if (ev.Kind == GuardianKind.PortOpened || ev.Kind == GuardianKind.PortClosed || ev.Kind == GuardianKind.PortObservedAtStartup)
            {
                c.CommandText = "INSERT INTO port_events(ts,action,pid,protocol,address,port,remote_address,remote_port,state) VALUES($ts,$kind,$pid,$protocol,$address,$port,$remote,$rport,$state)";
                c.Parameters.AddWithValue("$kind", ev.Kind.ToString());
                c.Parameters.AddWithValue("$protocol", Clip(ev.Protocol, 8));
                c.Parameters.AddWithValue("$address", Clip(ev.LocalAddress, 48));
                c.Parameters.AddWithValue("$port", ev.LocalPort);
                c.Parameters.AddWithValue("$remote", Clip(ev.RemoteAddress, 48));
                c.Parameters.AddWithValue("$rport", ev.RemotePort);
                c.Parameters.AddWithValue("$state", Clip(ev.State, 24));
            }
            else if (ev.Kind == GuardianKind.Diagnostic)
            {
                c.CommandText = "INSERT INTO diagnostics(ts,source,message) VALUES($ts,$source,$detail)";
                c.Parameters.AddWithValue("$source", Clip(ev.Source, 48));
                c.Parameters.AddWithValue("$detail", Clip(ev.Detail, 480));
            }
            else
            {
                c.CommandText = "INSERT INTO process_events(ts,kind,pid,started,name,ppid,source) VALUES($ts,$kind,$pid,$start,$name,$ppid,$source)";
                c.Parameters.AddWithValue("$kind", ev.Kind.ToString());
                c.Parameters.AddWithValue("$start", ev.ProcessStartTicks);
                c.Parameters.AddWithValue("$name", Clip(ev.Name, 180));
                c.Parameters.AddWithValue("$ppid", ev.ParentPid);
                c.Parameters.AddWithValue("$source", Clip(ev.Source, 48));
            }
            c.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    private static string Clip(string text, int limit) => text.Length > limit ? text[..limit] : text;

    // A soft quota is deliberately not a hard cap: the last hour of evidence is protected,
    // and quota pressure is always reported rather than pretending retention was honored.
    public StorageMaintenanceResult Maintenance(GuardianSettings cfg, DateTime utc, long? quotaOverrideBytes = null)
    {
        long before = new DateTimeOffset(utc.ToUniversalTime().AddDays(-cfg.RetentionDays)).ToUnixTimeSeconds();
        DeleteOlderThan(before);
        long quota = quotaOverrideBytes ?? (long)cfg.MaxDatabaseMb * 1024 * 1024;
        long protectedAfter = new DateTimeOffset(utc.ToUniversalTime().AddHours(-1)).ToUnixTimeSeconds();
        var pages = PageStats();
        long estimated = pages.LiveBytes;
        int prunedHours = 0;
        // Do not use FileInfo.Length as the loop condition. DELETE marks free pages but
        // does not necessarily shrink the main database file until a VACUUM.
        // Bound cleanup to 24 chronological steps per maintenance run; never prune the protected recent hour.
        while (estimated > quota && prunedHours < 24)
        {
            long? oldest = MinTimestamp();
            if (oldest is null || oldest.Value >= protectedAfter) break;
            long nextHour = (oldest.Value / 3600 + 1) * 3600;
            long cutoff = Math.Min(nextHour, protectedAfter);
            if (cutoff <= oldest.Value) break;
            DeleteOlderThan(cutoff);
            prunedHours++;
            pages = PageStats();
            estimated = pages.LiveBytes;
        }
        // Reclaim physical free pages no more than once per daily maintenance.
        // VACUUM is intentionally omitted if the DB fits the quota; it can be costly.
        long mainBeforeVacuum = new FileInfo(DatabasePath).Length;
        bool vacuumed = false;
        if (mainBeforeVacuum > quota && pages.FreeBytes > 4L * 1024 * 1024)
        {
            Exec("VACUUM;");
            vacuumed = true;
            pages = PageStats();
            estimated = pages.LiveBytes;
        }
        // TRUNCATE is best effort: concurrent read-only analyzers may pin WAL readers.
        // The result is returned as busy instead of claiming the WAL was truncated.
        bool checkpointBusy = TryWalCheckpointTruncate();
        long mainBytes = new FileInfo(DatabasePath).Length;
        long walBytes = File.Exists(DatabasePath + "-wal") ? new FileInfo(DatabasePath + "-wal").Length : 0;
        return new StorageMaintenanceResult(quota, mainBytes, walBytes, estimated,
            prunedHours, vacuumed, checkpointBusy,
            mainBytes + walBytes > quota || estimated > quota);
    }

    private (long LiveBytes, long FreeBytes) PageStats()
    {
        long count = ScalarLong("PRAGMA page_count");
        long free = ScalarLong("PRAGMA freelist_count");
        long size = ScalarLong("PRAGMA page_size");
        return (Math.Max(0L,count-free)*size, free*size);
    }
    private long ScalarLong(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
    private bool TryWalCheckpointTruncate()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
        using var reader = cmd.ExecuteReader();
        return reader.Read() && reader.GetInt64(0) != 0;
    }
    private long? MinTimestamp()
    {
        using var c = _db.CreateCommand();
        c.CommandText = "SELECT MIN(ts) FROM (SELECT MIN(ts) ts FROM process_events UNION ALL SELECT MIN(ts) FROM port_events UNION ALL SELECT MIN(bucket) ts FROM resources UNION ALL SELECT MIN(ts) FROM diagnostics)";
        var result = c.ExecuteScalar(); return result is long value ? value : null;
    }
    private void DeleteOlderThan(long before)
    {
        using var txn = _db.BeginTransaction();
        foreach (var (table,col) in new[] {("process_events","ts"),("resources","bucket"),("port_events","ts"),("diagnostics","ts")})
        {
            using var c = _db.CreateCommand(); c.Transaction = txn;
            c.CommandText = $"DELETE FROM {table} WHERE {col} < $min";
            c.Parameters.AddWithValue("$min", before); c.ExecuteNonQuery();
        }
        txn.Commit();
    }
    public string DailyReport(DateOnly date)
    {
        long first = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc)).ToUnixTimeSeconds();
        long last = first + 86400;
        long Count(string sql)
        {
            using var c = _db.CreateCommand(); c.CommandText = sql;
            c.Parameters.AddWithValue("$a", first); c.Parameters.AddWithValue("$b", last);
            return Convert.ToInt64(c.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        long started = Count("SELECT count(*) FROM process_events WHERE ts >= $a AND ts < $b AND kind='ProcessStarted'");
        long observed = Count("SELECT count(*) FROM process_events WHERE ts >= $a AND ts < $b AND kind='ProcessObservedAtStartup'");
        long stopped = Count("SELECT count(*) FROM process_events WHERE ts >= $a AND ts < $b AND kind='ProcessExited'");
        long knownPorts = Count("SELECT count(*) FROM port_events WHERE ts >= $a AND ts < $b AND action='PortObservedAtStartup'");
        long newPorts = Count("SELECT count(*) FROM port_events WHERE ts >= $a AND ts < $b AND action='PortOpened'");
        long diagnostics = Count("SELECT count(*) FROM diagnostics WHERE ts >= $a AND ts < $b");
        var b = new StringBuilder();
        b.AppendLine($"# ProcessLens Guardian — {date:yyyy-MM-dd} (UTC)");
        b.AppendLine();
        b.AppendLine($"Processi avviati osservati: {started}; presenti al primo campione: {observed}; terminazioni osservate: {stopped}.");
        b.AppendLine($"Endpoint presenti al primo campione: {knownPorts}; nuovi endpoint individuati tra due scansioni: {newPorts}; avvisi raccolta: {diagnostics}.");
        b.AppendLine(); b.AppendLine("## Processi con maggior CPU media osservata");
        using (var c = _db.CreateCommand())
        {
            c.CommandText = """
                SELECT name,round(SUM(cpu_sum)/NULLIF(SUM(cpu_count),0),2) avg_cpu,
                round(MAX(cpu_peak),2) peak_cpu, round(MAX(resident_peak)/1048576.0,1) resident_mb
                FROM resources WHERE bucket >= $a AND bucket < $b
                GROUP BY name ORDER BY avg_cpu DESC LIMIT 12
                """;
            c.Parameters.AddWithValue("$a",first); c.Parameters.AddWithValue("$b",last);
            using var r = c.ExecuteReader();
            while(r.Read()) b.AppendLine($"- {r.GetString(0)}: media {(r.IsDBNull(1)?"N/D":r.GetValue(1))}% / picco {(r.IsDBNull(2)?"N/D":r.GetValue(2))}% / residente totale max {(r.IsDBNull(3)?"N/D":r.GetValue(3))} MB");
        }
        b.AppendLine(); b.AppendLine("## Porte TCP in ascolto non loopback rilevate");
        using (var c = _db.CreateCommand())
        {
            c.CommandText = "SELECT protocol,address,port,pid FROM port_events WHERE ts >= $a AND ts < $b AND action IN ('PortOpened','PortObservedAtStartup') AND state='LISTEN' AND address NOT IN ('127.0.0.1','::1') GROUP BY protocol,address,port,pid LIMIT 50";
            c.Parameters.AddWithValue("$a",first);c.Parameters.AddWithValue("$b",last);
            using var r=c.ExecuteReader(); while(r.Read()) b.AppendLine($"- {r.GetString(0)} {r.GetString(1)}:{r.GetInt32(2)} (PID campionato {r.GetInt32(3)}): verificare origine e firewall, non necessariamente rischioso.");
        }
        b.AppendLine();
        b.AppendLine("## Interpretazione e limiti");
        b.AppendLine("- Nessun evento qui costituisce una diagnosi di malware.");
        b.AppendLine("- Le porte sono campionate, non intercettiamo tutte le connessioni transitorie.");
        b.AppendLine("- CPU e RAM sono campionate; la memoria residente riportata include pagine condivise.");
        b.AppendLine("- Le correlazioni PID-porta e parentela sono indizi: il PID puo' essere riutilizzato.");
        b.AppendLine("- Se ETW manca, processi brevi possono sfuggire; controllare la tabella diagnostics.");
        return b.ToString();
    }
    public void Dispose() => _db.Dispose();
}
