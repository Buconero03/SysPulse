using Microsoft.Data.Sqlite;
using ProcessLens.App;

// Presentation regressions use synthetic records and the existing snapshot only.
foreach (string cultureName in new[] { "it-IT", "en-US" })
{
    var savedCulture = System.Globalization.CultureInfo.CurrentCulture;
    try
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        foreach (string decimalText in new[] { "57,7", "57.7", "0" })
        {
            string raw = $"accepted=81985 saved=81985 working_set_mib={decimalText}";
            var row = new GuardianDiagnosticView("20/10/2026 13:06:05", "health", raw);
            var view = GuardianDiagnosticPresentation.From(row);
            Check(view.OriginalMessage == raw && view.OriginalSource == "health" && view.LocalTime == row.LocalTime,
                "R10 diagnostic retains exact original source message and timestamp");
            Check(view.Message.Contains(decimalText + " MiB", StringComparison.Ordinal) &&
                view.DetailText.Contains("EVENTI ACCETTATI DALLA CODA\n81985", StringComparison.Ordinal) &&
                view.DetailText.Contains("WORKING SET TOTALE DEL SERVIZIO", StringComparison.Ordinal),
                "R10 diagnostic fields preserve producer decimals independent of display culture");
            Check(view.Limitations.Contains("cumulativi", StringComparison.Ordinal) &&
                view.Limitations.Contains("non prova la completezza", StringComparison.Ordinal) &&
                view.Limitations.Contains("non è RAM privata", StringComparison.Ordinal),
                "R10 diagnostic separates time scope completeness and memory metrics");
        }
    }
    finally { System.Globalization.CultureInfo.CurrentCulture = savedCulture; }
}
foreach (string raw in new[] { "accepted=1 saved=oops working_set_mib=1", "accepted=1 saved=1",
    "accepted=1 accepted=2 saved=1 working_set_mib=1", "accepted=1 saved=1 working_set_mib=NaN",
    "accepted=1 saved=1 working_set_mib=1\nextra", "", new string('x', 1024), "errore\r\nseconda riga 🧪" })
{
    var view = GuardianDiagnosticPresentation.From(new("orario originale", "health", raw));
    Check(view.Message == raw && view.OriginalMessage == raw && view.Source == "health",
        "R10 unknown malformed partial and multiline diagnostic is not rewritten");
}
var otherSource = GuardianDiagnosticPresentation.From(new("ora", "storage", "accepted=1 saved=1 working_set_mib=1"));
Check(otherSource.Source == "storage" && otherSource.Message == otherSource.OriginalMessage,
    "R10 health grammar is not imposed on other sources");
foreach (string name in new[] { "svchost.exe", "SVCHOST.EXE", "services", "lsass.exe", "winlogon.exe", "dwm.exe" })
{
    var snapshot = new ProcessLens.Core.ProcessSnapshot { Pid = 13, ParentPid = 1, Name = name,
        CompanyName = "Dichiarazione di prova", CommandLine = "--secret=DO_NOT_COPY", ServiceNames = new[] { "fixture" } };
    var report = WindowsComponentContext.Describe(snapshot);
    Check(report.IsApplicable && report.Report.Contains("https://learn.microsoft.com/", StringComparison.Ordinal) &&
        report.Report.Contains("Dichiarazione di prova", StringComparison.Ordinal) &&
        report.Report.Contains("Identità e attività attuale non verificate", StringComparison.Ordinal) &&
        report.Report.Contains("Firma digitale e autenticità: non verificate", StringComparison.Ordinal),
        "R10 Windows architectural role has a source and does not authenticate an instance");
    Check(report.Report.Contains("fixture", StringComparison.Ordinal) && !report.Report.Contains("DO_NOT_COPY", StringComparison.Ordinal) &&
        report.Report.Contains("assenza non confermata", StringComparison.Ordinal),
        "R10 context retains associated services without copying secrets or inventing negative critical status");
}
foreach (string name in new[] { "my-svchost.exe", "svchost.exe.bak", @"C:\Windows\System32\svchost.exe", "unknown.exe" })
{
    var report = WindowsComponentContext.Describe(new() { Pid = 1, ParentPid = 0, Name = name });
    Check(!report.IsApplicable && report.Report.Contains("Funzione sconosciuta", StringComparison.Ordinal),
        "R10 unfamiliar name path and lookalike do not acquire Windows identity");
}
var partialServices = WindowsComponentContext.Describe(new() { Pid = 1, ParentPid = 0, Name = "unknown.exe",
    ServiceNames = Enumerable.Range(1, 17).Select(n => "service" + n).ToArray() });
Check(partialServices.IsApplicable && partialServices.Report.Contains("elenco parziale", StringComparison.Ordinal) &&
    partialServices.Report.Contains("N/D", StringComparison.Ordinal), "R10 bounded services and unavailable metadata are explicit");
Console.WriteLine("R10 Guardian diagnostics and Windows-context presentation regressions complete.");

string directory = Path.Combine(Path.GetTempPath(), "ProcessLens-GuiTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string path = Path.Combine(directory, "guardian.sqlite3");
DateTime now = DateTime.UtcNow;
long ts = new DateTimeOffset(now.AddMinutes(-2)).ToUnixTimeSeconds();
try
{
    using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
    {
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE process_events (id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL,
                kind TEXT NOT NULL, pid INTEGER NOT NULL, started INTEGER NOT NULL,
                name TEXT NOT NULL, ppid INTEGER NOT NULL, source TEXT NOT NULL);
            CREATE TABLE resources (bucket INTEGER NOT NULL,pid INTEGER NOT NULL,started INTEGER NOT NULL,
                name TEXT NOT NULL,cpu_sum REAL NOT NULL,cpu_count INTEGER NOT NULL,cpu_peak REAL,
                resident_peak INTEGER,commit_peak INTEGER,samples INTEGER NOT NULL);
            CREATE TABLE diagnostics (ts INTEGER NOT NULL,source TEXT NOT NULL,message TEXT NOT NULL);
            """;
        cmd.ExecuteNonQuery();
        using var insert = db.CreateCommand();
        insert.CommandText = """
            INSERT INTO process_events(ts,kind,pid,started,name,ppid,source) VALUES
              ($ts,'ProcessStarted',500,123,'claude',4,'etw'),
              ($ts,'ProcessStarted',700,456,'git',500,'etw'),
              ($ts,'ProcessExited',700,456,'git',500,'etw');
            INSERT INTO resources(bucket,pid,started,name,cpu_sum,cpu_count,cpu_peak,resident_peak,commit_peak,samples)
              VALUES ($bucket,500,123,'claude',6,2,4,104857600,0,2);
            INSERT INTO diagnostics(ts,source,message) VALUES($ts,'queue','queue saturated');
            """;
        insert.Parameters.AddWithValue("$ts", ts);
        insert.Parameters.AddWithValue("$bucket", (ts / 300) * 300);
        insert.ExecuteNonQuery();
    }
    var snap = GuardianDashboardReader.Read(path, 30, 250, now);
    Check(snap.Started == 2 && snap.Exited == 1 && snap.EtwStarts == 2 && snap.TotalEvents == 3, "ETW counters");
    Check(snap.Parents.Any(x => x.ParentPid == 500 && x.CandidateParent == "claude" && x.ChildName == "git"), "possible parent with caveat");
    Check(snap.QueueWarnings == 1 && snap.Resources.Any(x => x.Name == "claude"), "health and resources");
    Check(snap.Trend.Sum(x => x.Starts) == 2, "ETW trend counts");
    Check(snap.Events.Count == 3 && snap.Diagnostics.Count == 1, "bounded events and diagnostics");
    var older = GuardianDashboardReader.Read(path, 1, 10, now);
    Check(older.TotalEvents == 0 && older.Events.Count == 0, "time window filters");
    bool blocked = false;
    try { GuardianDashboardReader.Read(path, 5000); } catch (ArgumentOutOfRangeException) { blocked = true; }
    Check(blocked, "interval bounded");
    using (var readOnly = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString()))
    {
        readOnly.Open();
        using var c = readOnly.CreateCommand();
        c.CommandText = "SELECT COUNT(*) FROM process_events";
        Check(Convert.ToInt64(c.ExecuteScalar()) == 3, "data not modified");
    }
    // Lifecycle regression tests: reused PIDs must not merge different generations.
    long firstChildId;
    using (var write = new SqliteConnection(new SqliteConnectionStringBuilder {
        DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ToString()))
    {
        write.Open();
        using var script = write.CreateCommand();
        script.CommandText = """
            INSERT INTO process_events(ts,kind,pid,started,name,ppid,source) VALUES
              ($t-100,'ProcessStarted',9000,111,'claude-old',4,'etw'),
              ($t-90,'ProcessStarted',9100,222,'git-old',9000,'etw'),
              ($t-80,'ProcessStarted',9200,333,'git-helper',9100,'etw'),
              ($t-70,'ProcessExited',9100,222,'git-old',9000,'etw'),
              ($t-60,'ProcessExited',9000,111,'claude-old',4,'etw'),
              ($t-50,'ProcessStarted',9000,444,'claude-new',5,'etw'),
              ($t-40,'ProcessStarted',9300,555,'git-new',9000,'etw'),
              ($t-30,'ProcessStarted',6001,666,'orphan-child',6000,'etw'),
              ($t-20,'ProcessStarted',7000,777,'same-second-parent',4,'etw'),
              ($t-20,'ProcessStarted',7001,888,'same-second-child',7000,'etw');
            """;
        script.Parameters.AddWithValue("$t", ts);
        script.ExecuteNonQuery();
        using var first = write.CreateCommand();
        first.CommandText = "SELECT id FROM process_events WHERE pid=9100 AND kind='ProcessStarted' LIMIT 1";
        firstChildId = Convert.ToInt64(first.ExecuteScalar());
    }
    var latestParent = GuardianLineageReader.Read(path, 9000, 30, now);
    Check(latestParent.FocusName == "claude-new" &&
          latestParent.Children.Any(c => c.Name == "git-new") &&
          latestParent.Children.All(c => c.Name != "git-old"), "reused PID generations segregated");
    var oldChild = GuardianLineageReader.Read(path, 9100, 30, now,
        focusStartEventId: firstChildId);
    Check(oldChild.Ancestors.Any(a => a.Pid == 9000 && a.Name == "claude-old") &&
          oldChild.Children.Any(c => c.Pid == 9200), "historic pinned event + ancestry + descendants");
    var oldParentId = GuardianLineageReader.Read(path, 9000, 30, now,
        focusStartEventId: FindEventId(path, 9000, "claude-old"));
    Check(oldParentId.FocusName == "claude-old" &&
          oldParentId.Children.Any(c => c.Pid == 9100) &&
          oldParentId.Children.All(c => c.Pid != 9300), "historic parent contains only historic generation");
    var unknown = GuardianLineageReader.Read(path, 6000, 30, now);
    Check(unknown.Children.Count == 1 &&
          unknown.Children[0].LinkStatus == "Non verificabile", "missing parent evidence reported");
    var sameSecond = GuardianLineageReader.Read(path, 7000, 30, now);
    Check(sameSecond.Children.Count == 1 &&
          sameSecond.Children[0].LinkStatus == "Ambigua", "second precision ambiguity exposed");
    bool invalidPid = false;
    try { GuardianLineageReader.Read(path, 0); } catch (ArgumentOutOfRangeException) { invalidPid = true; }
    Check(invalidPid, "PID argument validated");
    Console.WriteLine("PASS Guardian Lineage: lifecycle selection, historical event pinning, PID reuse, depth navigation, ambiguity, read-only");

    Console.WriteLine("PASS Guardian Dashboard: read-only queries, event activity, parent evidence, storage, bounded windows");
}
finally
{
    try { Directory.Delete(directory, true); } catch { }
}

static void Check(bool ok, string message)
{
    if (!ok) throw new InvalidOperationException("FAIL " + message);
}

static long FindEventId(string path, int pid, string name)
{
    using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
        DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
    db.Open();
    using var c = db.CreateCommand();
    c.CommandText = "SELECT id FROM process_events WHERE pid=$pid AND name=$name AND kind='ProcessStarted' LIMIT 1";
    c.Parameters.AddWithValue("$pid", pid);
    c.Parameters.AddWithValue("$name", name);
    return Convert.ToInt64(c.ExecuteScalar());
}
