using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ProcessLens.Guardian.Service;

// Read-only, retrospective process activity diagnostics. No DB migrations or service restart required.
// Evidence boundaries: PID reuse, incomplete history, timestamp resolution, bucket aggregation.
public sealed class GuardianActivityAnalyzer : IDisposable
{
    private readonly SqliteConnection _db;

    public GuardianActivityAnalyzer(string databasePath)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Guardian database not found", databasePath);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        _db.Open();
        using var c = _db.CreateCommand();
        c.CommandText = "PRAGMA query_only=ON; PRAGMA busy_timeout=3000;";
        c.ExecuteNonQuery();
    }

    private static long Seconds(DateTime date) => new DateTimeOffset(date.ToUniversalTime()).ToUnixTimeSeconds();
    private static string Clean(string value) => value.Replace('\r',' ').Replace('\n',' ').Replace('|','/').Replace('`',' ');

    private SqliteCommand Query(string sql, long first, long last)
    {
        var c = _db.CreateCommand();
        c.CommandText = sql;
        c.Parameters.AddWithValue("$from", first);
        c.Parameters.AddWithValue("$to", last);
        return c;
    }

    private long CountStarts(long first, long last, string? name = null)
    {
        using var cmd = Query("SELECT count(*) FROM process_events WHERE ts >= $from AND ts < $to " +
            "AND kind='ProcessStarted'" + (name is null ? "" : " AND name=$name"),first,last);
        if (name is not null) cmd.Parameters.AddWithValue("$name",name);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    private string ParentEvidence(int ppid, long firstChild, long lastChild)
    {
        // Identify the parent instance most recently observed at the FIRST child event.
        // If the PID is reused, the attribution of this group must be treated as ambiguous.
        using var birth = _db.CreateCommand();
        birth.CommandText = """
            SELECT id,ts,kind,name FROM process_events
            WHERE pid=$pid AND kind IN ('ProcessStarted','ProcessObservedAtStartup') AND ts <= $first
            ORDER BY ts DESC,id DESC LIMIT 1
            """;
        birth.Parameters.AddWithValue("$pid",ppid);
        birth.Parameters.AddWithValue("$first",firstChild);
        using var reader = birth.ExecuteReader();
        if (!reader.Read()) return "Non identificato (storico incompleto)";
        long birthId = reader.GetInt64(0);
        long birthTs = reader.GetInt64(1);
        string kind = reader.GetString(2);
        string name = Clean(reader.GetString(3));
        reader.Close();

        using var changes = _db.CreateCommand();
        changes.CommandText = """
            SELECT count(*) FROM process_events
            WHERE pid=$pid AND id > $birthId AND ts >= $born AND ts <= $last
            AND kind IN ('ProcessStarted','ProcessObservedAtStartup','ProcessExited')
            """;
        changes.Parameters.AddWithValue("$pid",ppid);
        changes.Parameters.AddWithValue("$birthId",birthId);
        changes.Parameters.AddWithValue("$born",birthTs);
        changes.Parameters.AddWithValue("$last",lastChild);
        long transitions = (long)(changes.ExecuteScalar() ?? 0L);
        if (transitions > 0) return $"{name} (AMBIGUO: uscita/riutilizzo PID nel periodo)";
        return kind == "ProcessObservedAtStartup"
            ? $"{name} (osservato allo startup, parentela non certa)"
            : $"{name} (compatibile per PID e cronologia; non prova definitiva)";
    }

    public string BuildReport(int minutes = 30, DateTime? referenceUtc = null)
    {
        if (minutes < 5 || minutes > 1440) throw new ArgumentOutOfRangeException(nameof(minutes),"Window must be 5-1440 minutes");
        DateTime now = (referenceUtc ?? DateTime.UtcNow).ToUniversalTime();
        long end = Seconds(now) + 1;
        long start = end - minutes * 60L;
        long five = end - 300;
        long prior = end - 600;
        var b = new StringBuilder();
        b.AppendLine("# ProcessLens Guardian - Process Activity Intelligence (anteprima v0.14.0)");
        b.AppendLine($"Finestra: {now.AddMinutes(-minutes):yyyy-MM-dd HH:mm:ss} - {now:yyyy-MM-dd HH:mm:ss} UTC ({minutes} minuti).");
        b.AppendLine("Lettura SQLite: sola consultazione. Nessun comando di processo acquisito.");
        b.AppendLine();

        long recent = CountStarts(five,end);
        long previous = CountStarts(prior,five);
        long total = CountStarts(start,end);
        b.AppendLine("## Frequenza degli avvii");
        b.AppendLine($"- Ultimi 5 minuti: {recent} avvii ({recent / 5.0:F1}/min).");
        b.AppendLine($"- 5 minuti precedenti: {previous} avvii ({previous / 5.0:F1}/min).");
        b.AppendLine($"- Totale finestra: {total} avvii ({total / (double)minutes:F1}/min).");
        b.AppendLine("- Variazione: " + (previous == 0 ? "non calcolabile (base precedente zero)."
            : $"{((recent - previous) * 100.0 / previous):+0.0;-0.0;0.0}% rispetto ai 5 minuti precedenti."));
        b.AppendLine();

        b.AppendLine("## Eventi per origine e tipo");
        using (var c = Query("""
            SELECT source,kind,count(*) FROM process_events WHERE ts >= $from AND ts < $to
            GROUP BY source,kind ORDER BY source,kind
            """,start,end))
        using (var r=c.ExecuteReader())
        {
            bool any=false;
            while(r.Read()) { any=true; b.AppendLine($"- {Clean(r.GetString(0))} / {Clean(r.GetString(1))}: {r.GetInt64(2)}"); }
            if (!any) b.AppendLine("- Nessun evento processo registrato nella finestra.");
        }
        b.AppendLine();

        b.AppendLine("## Eseguibili con piu' avvii (top 12)");
        using (var c=Query("""
            SELECT name,count(*) FROM process_events
            WHERE ts >= $from AND ts < $to AND kind='ProcessStarted'
            GROUP BY name ORDER BY count(*) DESC,name LIMIT 12
            """,start,end))
        using (var r=c.ExecuteReader())
            while(r.Read()) b.AppendLine($"- {Clean(r.GetString(0))}: {r.GetInt64(1)} avvii");
        b.AppendLine();

        b.AppendLine("## Generatori principali di processi (top 15)");
        b.AppendLine("I nomi dei padri sono inferiti dallo storico; un PPID da solo non identifica un'istanza in modo univoco.");
        var parents=new List<(int Pid,string ChildName,long Events,long First,long Last)>();
        using (var c=Query("""
            SELECT ppid,name,count(*),min(ts),max(ts) FROM process_events
            WHERE ts >= $from AND ts < $to AND kind='ProcessStarted' AND ppid>0
            GROUP BY ppid,name ORDER BY count(*) DESC,ppid LIMIT 15
            """,start,end))
        using (var r=c.ExecuteReader())
            while(r.Read()) parents.Add((r.GetInt32(0),r.GetString(1),r.GetInt64(2),r.GetInt64(3),r.GetInt64(4)));
        foreach(var p in parents)
            b.AppendLine($"- PPID {p.Pid} -> {Clean(p.ChildName)}: {p.Events} avvii; padre possibile: {ParentEvidence(p.Pid,p.First,p.Last)}");
        if (parents.Count == 0) b.AppendLine("- Nessuna relazione padre-figlio disponibile.");
        b.AppendLine();

        b.AppendLine("## CPU/RAM osservate (campioni aggregati, top 10 per CPU media)");
        long bucketStart=(start / 300)*300;
        using (var c=Query("""
            SELECT name,round(sum(cpu_sum)/nullif(sum(cpu_count),0),2),
                   round(max(cpu_peak),2),round(max(resident_peak)/1048576.0,1),sum(samples)
            FROM resources WHERE bucket >= $from AND bucket < $to
            GROUP BY name ORDER BY sum(cpu_sum)/nullif(sum(cpu_count),0) DESC LIMIT 10
            """,bucketStart,end))
        using (var r=c.ExecuteReader())
        {
            bool any=false;
            while(r.Read())
            {
                any=true;
                string avg=r.IsDBNull(1)?"N/D":Convert.ToString(r.GetValue(1),CultureInfo.InvariantCulture)!;
                string peak=r.IsDBNull(2)?"N/D":Convert.ToString(r.GetValue(2),CultureInfo.InvariantCulture)!;
                string ram=r.IsDBNull(3)?"N/D":Convert.ToString(r.GetValue(3),CultureInfo.InvariantCulture)!;
                b.AppendLine($"- {Clean(r.GetString(0))}: CPU media {avg}%, picco {peak}%, RAM residente di un'istanza fino a {ram} MB, campioni {r.GetInt64(4)}");
            }
            if(!any) b.AppendLine("- Nessun campione risorse disponibile.");
        }
        b.AppendLine();
        b.AppendLine("## Volume della raccolta e stato ETW");
        long totalEvents = 0;
        using (var c=Query("SELECT count(*) FROM process_events WHERE ts >= $from AND ts < $to",start,end))
            totalEvents=(long)(c.ExecuteScalar() ?? 0L);
        b.AppendLine($"- Eventi processo registrati nella finestra: {totalEvents} ({totalEvents / (double)minutes:F1}/min).");
        foreach (var suffix in new[]{"", "-wal", "-shm"})
        {
            string path = _db.DataSource + suffix;
            if (File.Exists(path))
                b.AppendLine($"- Storage {Path.GetFileName(path)}: {new FileInfo(path).Length / 1048576.0:F2} MiB.");
        }
        using (var c=Query("SELECT count(*) FROM diagnostics WHERE ts >= $from AND ts < $to AND source='queue'",start,end))
            b.AppendLine($"- Avvisi della coda nella finestra: {(long)(c.ExecuteScalar() ?? 0L)} (consultare diagnostics per quantificare le perdite).");
        using (var c=Query("SELECT max(ts) FROM process_events WHERE ts >= $from AND ts < $to AND source='etw'",start,end))
        {
            var lastEtw = c.ExecuteScalar();
            b.AppendLine(lastEtw is long last
                ? $"- Ultimo evento ETW nella finestra: {DateTimeOffset.FromUnixTimeSeconds(last):yyyy-MM-dd HH:mm:ss} UTC."
                : "- Nessun evento ETW nella finestra: verificare se il provider e' abilitato e se ci sono attivita' osservabili.");
        }
        b.AppendLine();

        b.AppendLine("## Limiti delle evidenze");
        b.AppendLine("- Una frequenza elevata non indica automaticamente malware, abuso o malfunzionamento.");
        b.AppendLine("- ETW e polling possono osservare le stesse istanze; il Collector tenta di sopprimere duplicati, senza garanzia di completezza assoluta.");
        b.AppendLine("- I PID possono essere riutilizzati; i padri non vengono attribuiti definitivamente solo dal PPID. Le transizioni durante il periodo rendono ambigua l'attribuzione.");
        b.AppendLine("- Timestamp SQLite in secondi: eventi entro lo stesso secondo non hanno un ordine temporale certo usando solo ts.");
        b.AppendLine("- Le metriche CPU/RAM sono campionate per nome, non attribuite al processo padre; bucket di 5 minuti possono estendersi oltre la finestra.");
        b.AppendLine("- Non vengono acquisiti argomenti della riga di comando, percorsi dei repository o azioni Git.");
        return b.ToString();
    }

    public void Dispose() => _db.Dispose();
}
