using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ProcessLens.Guardian.Service;

// Separate, strictly read-only health query; safe while the collector keeps its WAL writer open.
public sealed class GuardianStorageHealth : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly string _path;
    public GuardianStorageHealth(string databasePath)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Guardian database not found", databasePath);
        _path = databasePath;
        _db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        _db.Open();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=ON; PRAGMA busy_timeout=3000";
        cmd.ExecuteNonQuery();
    }
    private long Number(string sql, long since = 0)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$since", since);
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
    }
    private static long Bytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0L;
    public string BuildReport(int minutes = 30, DateTime? referenceUtc = null)
    {
        if (minutes is < 5 or > 1440) throw new ArgumentOutOfRangeException(nameof(minutes));
        DateTime now = (referenceUtc ?? DateTime.UtcNow).ToUniversalTime();
        long since = new DateTimeOffset(now.AddMinutes(-minutes)).ToUnixTimeSeconds();
        long latestEtw = Number("SELECT COALESCE(MAX(ts),0) FROM process_events WHERE source='etw'");
        long latestEvent = Number("SELECT COALESCE(MAX(ts),0) FROM process_events");
        long process = Number("SELECT COUNT(*) FROM process_events WHERE ts >= $since",since);
        long etwStarts = Number("SELECT COUNT(*) FROM process_events WHERE ts >= $since AND source='etw' AND kind='ProcessStarted'",since);
        long queueLoss = Number("SELECT COUNT(*) FROM diagnostics WHERE ts >= $since AND source='queue'",since);
        long storageLoss = Number("SELECT COUNT(*) FROM diagnostics WHERE ts >= $since AND source='storage' AND message LIKE 'Lost % events due to failed SQLite transactions'",since);
        long main = Bytes(_path), wal = Bytes(_path+"-wal"), shm = Bytes(_path+"-shm");
        var b = new StringBuilder();
        b.AppendLine("# ProcessLens Guardian — Storage & Collector Health (v0.14.1)");
        b.AppendLine($"Intervallo: ultimi {minutes} minuti, riferimento {now:yyyy-MM-dd HH:mm:ss} UTC.");
        b.AppendLine("Modalita': database SQLite aperto in sola lettura; servizio non alterato.");
        b.AppendLine(); b.AppendLine("## Attivita' ETW e affidabilita'");
        b.AppendLine($"- Eventi processo nel periodo (ETW + polling): {process}.");
        b.AppendLine($"- Avvii ETW nel periodo: {etwStarts}.");
        b.AppendLine($"- Ultimo evento ETW: {FormatTime(latestEtw)}.");
        b.AppendLine($"- Ultimo evento processo: {FormatTime(latestEvent)}.");
        b.AppendLine($"- Diagnostiche di overflow coda nel periodo: {queueLoss}.");
        b.AppendLine($"- Diagnostiche di batch SQLite persi nel periodo: {storageLoss}.");
        b.AppendLine("- Zero diagnostiche NON garantisce che ETW sia completo: i messaggi possono mancare se SQLite non e' scrivibile.");
        b.AppendLine(); b.AppendLine("## Storage fisico");
        b.AppendLine($"- SQLite principale: {main / 1048576.0:F2} MiB.");
        b.AppendLine($"- SQLite WAL: {wal / 1048576.0:F2} MiB.");
        b.AppendLine($"- SQLite SHM: {shm / 1048576.0:F2} MiB.");
        b.AppendLine($"- Somma principale + WAL: {(main+wal) / 1048576.0:F2} MiB.");
        b.AppendLine("- Il WAL puo' mantenere la stessa dimensione pur riutilizzando pagine; non misura da solo la crescita definitiva.");
        b.AppendLine(); b.AppendLine("## Ultime diagnostiche rilevanti");
        using(var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT ts,source,message FROM diagnostics WHERE source IN ('queue','storage','etw','health') ORDER BY ts DESC LIMIT 12";
            using var reader = cmd.ExecuteReader();
            int found = 0;
            while (reader.Read())
            {
                found++;
                string details = reader.GetString(2).Replace('\r',' ').Replace('\n',' ');
                b.AppendLine($"- {FormatTime(reader.GetInt64(0))} [{reader.GetString(1)}] {details}");
            }
            if (found == 0) b.AppendLine("- Nessuna diagnostica disponibile.");
        }
        return b.ToString();
    }
    private static string FormatTime(long timestamp) => timestamp > 0
        ? DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
        : "nessuno";
    public void Dispose() => _db.Dispose();
}
