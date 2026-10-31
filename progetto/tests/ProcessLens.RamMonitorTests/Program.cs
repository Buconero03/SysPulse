using ProcessLens.RamMonitor;
static void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL RAM monitor: " + name);
    Console.WriteLine("PASS RAM monitor: " + name);
}
static ProcessReading Row(int pid, long? bytes, bool included = true) =>
    new(pid, 0, "test.exe", Path.Combine(Path.GetTempPath(), "test.exe"), 100, DateTime.UnixEpoch,
        included ? "ExactExecutablePath" : "OutsideScope", included, bytes, bytes.HasValue ? bytes.Value + 10 : null, bytes.HasValue ? bytes.Value + 20 : null, "");
var first = Row(1, 100);
var second = Row(2, 200);
var totals = SampleTotals.Aggregate(new[] { first, second }, true);
Check(totals.PrivateResident.CompleteBytes == 300 && totals.IncludedProcesses == 2, "per-PID private bytes summed without using working set or commit");
Check(totals.WorkingSet.CompleteBytes == 320 && totals.PrivateCommit.CompleteBytes == 340, "three metrics remain distinct");
var partial = SampleTotals.Aggregate(new[] { first, Row(2, null) }, true);
Check(!partial.PrivateResident.Complete && partial.PrivateResident.CompleteBytes is null && partial.PrivateResident.ObservedBytes == 100,
    "one missing private reading makes total partial, not a complete lower number");
var noPrivate = SampleTotals.Aggregate(new[] { Row(2, null) with { WorkingSetBytes = 900, PrivateCommitBytes = 500 } }, true);
Check(noPrivate.PrivateResident.ObservedBytes is null && noPrivate.WorkingSet.CompleteBytes == 900 && noPrivate.PrivateCommit.CompleteBytes == 500,
    "EX-only fallback cannot turn total resident or commit into private RAM");
var unknown = SampleTotals.Aggregate(new[] { first, Row(9, null, false) with { Association = "Unresolved" } }, true);
Check(unknown.UnresolvedCandidates == 1 && unknown.PrivateResident.CompleteBytes is null, "unresolved candidate prevents complete total");
var failedEnum = SampleTotals.Aggregate(Array.Empty<ProcessReading>(), false);
Check(failedEnum.PrivateResident.CompleteBytes is null && failedEnum.PrivateResident.ObservedBytes is null, "failed enumeration cannot establish no processes or zero RAM");
var empty = SampleTotals.Aggregate(Array.Empty<ProcessReading>(), true);
Check(empty.PrivateResident.CompleteBytes == 0 && empty.IncludedProcesses == 0, "successful empty scope snapshot establishes only zero observed members");
Check(SampleTotals.Aggregate(new[] { Row(1, 0) }, true).PrivateResident.CompleteBytes == 0, "genuinely read zero remains valid");
var meter = Row(77, 9000, false) with { Association = "MonitorExcluded" };
Check(SampleTotals.Aggregate(new[] { first, meter, Row(88, 7000, false) }, true).PrivateResident.CompleteBytes == 100,
    "monitor and other installations excluded from suite sum");
Check(SampleTotals.Aggregate(new[] { first, first }, true).PrivateResident.CompleteBytes == 100, "duplicate instance not double counted");
var parent = first with { StartUtcTicks = 200, Pid = 11 };
var child = second with { StartUtcTicks = 300, ParentPid = 11, HandleParentPid = 11, Pid = 12 };
Check(!MonitorScope.VerifiedChild(child with { HandleParentPid = null }, parent), "snapshot parent alone cannot prove descendant after a reuse race");
Check(MonitorScope.VerifiedChild(child, parent), "observed parent PID plus comparable creation times permits descendant edge");
Check(!MonitorScope.VerifiedChild(child with { StartUtcTicks = 100 }, parent), "child predating reused parent PID cannot be attributed");
Check(!MonitorScope.VerifiedChild(child with { StartUtcTicks = null }, parent) && !MonitorScope.VerifiedChild(child, parent with { StartUtcTicks = null }), "missing creation time cannot prove descendant identity");
Check(!MonitorScope.VerifiedChild(child, parent with { Included = false }) && !MonitorScope.VerifiedChild(child with { ParentPid = 99 }, parent),
    "unverified parent or different parent PID does not establish suite membership");
string path = Path.Combine(Path.GetTempPath(), "scope", "ProcessLens.exe");
var scope = new MonitorScope(new[] { path });
Check(scope.Contains(path.ToUpperInvariant()) && !scope.Contains(Path.Combine(Path.GetTempPath(), "another", "ProcessLens.exe")),
    "same executable name at another path cannot prove membership");
Check(!scope.Contains(null), "unreadable executable path is not accepted");
if (OperatingSystem.IsWindows())
{
    // Read-only census and self-counter checks; no processes launched, ended or profiled.
    var native = new NativeSampler(new MonitorScope(Array.Empty<string>())).Capture();
    Check(native.EnumerationComplete, "native process snapshot completes on Windows");
    Check(native.Monitor.Pid == Environment.ProcessId && native.Monitor.StartUtcTicks.HasValue && native.Monitor.WorkingSetBytes > 0,
        "native limited-rights self read yields instance identity and working set");
    Check(!native.Monitor.Included && native.Monitor.Association == "MonitorExcluded", "native meter self read is excluded");
    using var self = System.Diagnostics.Process.GetCurrentProcess();
    Check(native.Monitor.StartUtcTicks == self.StartTime.ToUniversalTime().Ticks, "native creation matches independent .NET timestamp");
}
else Console.WriteLine("NON ESEGUITO: native Windows sampler tests.");

string logFixture = Path.Combine(Path.GetTempPath(), "ProcessLensRamMonitorTest-" + Guid.NewGuid().ToString("N"));
try
{
    string sessionPath;
    using (var session = new LogSession(logFixture, scope, 5, 50))
    {
        sessionPath = session.DirectoryPath;
        var sample = new CaptureResult(DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(1), true, "",
            new[] { first with { Name = "prova, \"perché\"" } }, meter);
        session.Append(sample);
        session.Append(sample with { Processes = new[] { Row(2, null) } });
        session.SaveSummary("TestComplete");
    }
    string[] humanLog = File.ReadAllLines(Path.Combine(sessionPath, "monitor.log"));
    Check(humanLog.Where(line => line.Length > 0).All(line => line.StartsWith("[", StringComparison.Ordinal) &&
        line.Contains("] [RAM Monitor] ", StringComparison.Ordinal)), "R11 every monitor event has a timestamp and component");
    Check(humanLog.Any(line => line.Contains("Working Set privato totale: N/D · parziale", StringComparison.Ordinal)) &&
        humanLog.Any(line => line.Contains("Monitor escluso:", StringComparison.Ordinal)),
        "R11 monitor logs retain partial totals and meter exclusion");
    string[] totalLines = File.ReadAllLines(Path.Combine(sessionPath, "totals.csv"));
    Check(totalLines.Length == 3 && totalLines[2].Split(',')[6] == "\"\"", "incomplete CSV total is empty, not a fabricated zero");
    string processCsv = File.ReadAllText(Path.Combine(sessionPath, "processes.csv"));
    Check(processCsv.Contains("\"prova, \"\"perché\"\"\""), "UTF-8 process CSV preserves commas and quoted text");
    using var summary = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(sessionPath, "summary.json")));
    Check(summary.RootElement.GetProperty("Samples").GetInt32() == 2 &&
        summary.RootElement.GetProperty("PrivateCompleteSamples").GetInt32() == 1 &&
        summary.RootElement.GetProperty("SampledPeakCompletePrivateBytes").GetInt64() == 100,
        "summary counts partial samples separately and never adds the monitor footprint");
}
finally { if (Directory.Exists(logFixture)) Directory.Delete(logFixture, true); }
