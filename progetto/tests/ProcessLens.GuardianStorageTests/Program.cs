using ProcessLens.Guardian.Core;
using ProcessLens.Guardian.Service;

string dir = Path.Combine(Path.GetTempPath(),"ProcessLensGuardianStorageTest-"+Guid.NewGuid().ToString("N"));
try
{
    var date = new DateTime(2026,10,9,13,1,0,DateTimeKind.Utc);
    using (var store = new GuardianStore(dir))
    {
        store.SaveBatch(new GuardianEvent[] {
            new(date,GuardianKind.ProcessStarted,123,111,"test-worker",1,"etw"),
            new(date,GuardianKind.ResourceSample,123,111,"test-worker",CpuPercent:8.5,ResidentWorkingSetBytes:52428800,PrivateCommitBytes:62914560),
            new(date.AddSeconds(15),GuardianKind.ResourceSample,123,111,"test-worker",CpuPercent:11.5,ResidentWorkingSetBytes:62914560,PrivateCommitBytes:73400320),
            new(date,GuardianKind.PortOpened,123,Protocol:"TCP4",LocalAddress:"0.0.0.0",LocalPort:8080,State:"LISTEN"),
            new(date,GuardianKind.Diagnostic,Source:"etw",Detail:"Simulated fallback")
        });
        string report = store.DailyReport(new DateOnly(2026,10,9));
        if (!report.Contains("test-worker") || !report.Contains("8080") || !report.Contains("Processi avviati osservati: 1") || !report.Contains("avvisi raccolta: 1"))
            throw new Exception("Report missing expected rows:\n"+report);
        store.Maintenance(new GuardianSettings(),date.AddMinutes(1));
    }
    // v0.14 process activity: exercise rolling windows, direct parent attribution, PID reuse.
    DateTime reference = date.AddMinutes(30);
    using (var store = new GuardianStore(dir))
    {
        store.SaveBatch(new GuardianEvent[] {
            new(date.AddMinutes(2),GuardianKind.ProcessStarted,42,4200,"claude",1,"etw"),
            new(date.AddMinutes(23),GuardianKind.ProcessStarted,1000,10000,"git",42,"etw"),
            new(date.AddMinutes(28),GuardianKind.ProcessStarted,1001,10001,"git",42,"etw"),
            // A parent PID has changed identity inside the observation window:
            new(date.AddMinutes(3),GuardianKind.ProcessStarted,77,7700,"old-parent",1,"etw"),
            new(date.AddMinutes(23),GuardianKind.ProcessStarted,2000,20000,"python",77,"etw"),
            new(date.AddMinutes(25),GuardianKind.ProcessExited,77,7700,"old-parent",1,"etw"),
            new(date.AddMinutes(26),GuardianKind.ProcessStarted,77,7710,"new-parent",1,"etw"),
            new(date.AddMinutes(29),GuardianKind.ProcessStarted,2001,20010,"python",77,"etw")
        });
    }
    using (var analyzer = new GuardianActivityAnalyzer(Path.Combine(dir,"guardian.sqlite3")))
    {
        string activity = analyzer.BuildReport(30,reference);
        if (!activity.Contains("## Frequenza degli avvii") ||
            !activity.Contains("## Generatori principali") ||
            !activity.Contains("claude (compatibile") ||
            !activity.Contains("AMBIGUO") ||
            !activity.Contains("git: 2 avvii") ||
            !activity.Contains("python: 2 avvii"))
            throw new Exception("Guardian activity analysis did not distinguish evidence and PID reuse:\n"+activity);
        try { analyzer.BuildReport(4,reference); throw new Exception("Accepted an invalid duration"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine("PASS activity rolling counts, process groups, parent evidence, reused PID ambiguity, read-only SQLite");
    }
    // v0.14.1: storage quota never removes the protected recent hour, and
    // a separate health analyzer can query existing WAL-backed SQLite read-only.
    var quotaNow = new DateTime(2026,10,10,12,0,0,DateTimeKind.Utc);
    using (var store = new GuardianStore(dir))
    {
        var rows = new List<GuardianEvent>();
        for (int hour = 2; hour <= 22; hour++)
            for (int j = 0; j < 100; j++)
                rows.Add(new GuardianEvent(quotaNow.AddHours(-hour).AddSeconds(j),
                    GuardianKind.ProcessStarted, 10000 + hour*100 + j, 1,
                    "worker-" + new string('x',130), 42,"etw"));
        rows.Add(new GuardianEvent(quotaNow.AddMinutes(-10),
            GuardianKind.ProcessStarted,9999,9999,"recent-must-survive",42,"etw"));
        for (int i=0;i<rows.Count;i+=256)
            store.SaveBatch(rows.Skip(i).Take(256).ToArray());
        var result = store.Maintenance(new GuardianSettings(),quotaNow,64L*1024);
        if (result.QuotaBytes != 64L*1024 || result.PrunedHours < 1)
            throw new Exception("Expected small test quota to trigger bounded oldest-first pruning");
        using var cmd = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder {
                DataSource=Path.Combine(dir,"guardian.sqlite3"),
                Mode=Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                Pooling=false}.ToString());
        cmd.Open();
        using var check = cmd.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM process_events WHERE name='recent-must-survive'";
        if ((long)(check.ExecuteScalar() ?? 0L) != 1L)
            throw new Exception("Retention removed the protected recent hour");
        Console.WriteLine("PASS quota regression: page estimates, bounded pruning, recent evidence protected");
    }
    using (var health = new GuardianStorageHealth(Path.Combine(dir,"guardian.sqlite3")))
    {
        string report = health.BuildReport(30,quotaNow);
        if (!report.Contains("SQLite WAL") || !report.Contains("Storage & Collector Health") ||
            !report.Contains("Avvii ETW"))
            throw new Exception("Read-only health report is incomplete");
        try { health.BuildReport(1,quotaNow); throw new Exception("Accepted invalid minutes"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine("PASS read-only storage and ETW health report");
    }
    if (!File.Exists(Path.Combine(dir,"guardian.sqlite3"))) throw new Exception("SQLite file not created");
    Console.WriteLine("PASS SQLite schema, batched inserts, 5m UPSERT, daily report, retention maintenance");
}
finally { try { Directory.Delete(dir,true); } catch { } }
