using ProcessLens.Core;
using ProcessLens.Semantics;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("TEST FAILED: " + name);
    Console.WriteLine("PASS " + name);
    passed++;
}

var parent = new ProcessAncestorSnapshot(
    100, 40, "ChatGPT.exe", new DateTime(2026, 10, 8, 9, 0, 0),
    "ChatGPT.exe --some-private-token=SHOULD_NOT_BE_DISPLAYED", null, true);

ProcessSnapshot Make(string name, string? cmd, IReadOnlyList<ProcessAncestorSnapshot>? ancestors = null) => new()
{
    Pid = 200,
    ParentPid = 100,
    ParentName = "ChatGPT.exe",
    Name = name,
    CommandLine = cmd,
    StartTime = new DateTime(2026, 10, 8, 9, 1, 0),
    AncestorDetails = ancestors ?? [parent]
};

var push = ProcessOriginAnalyzer.Analyze(Make("git.exe", "\"C:\\Program Files\\Git\\cmd\\git.exe\" push origin main"));
Check(push.Operation.Contains("push", StringComparison.OrdinalIgnoreCase), "git push recognized from observed command");
Check(push.Chain.Contains("ChatGPT.exe") && push.Chain.Contains("git.exe"), "live ancestry shown");
Check(!push.Chain.Contains("SHOULD_NOT_BE_DISPLAYED"), "full ancestor command suppressed");
Check(push.Limitations.Contains("Esito e avanzamento sconosciuti", StringComparison.OrdinalIgnoreCase), "success not claimed");

var stat = ProcessOriginAnalyzer.Analyze(Make("git.exe", "git -C \"D:\\Some Projects\\Repo\" status"));
Check(stat.Operation.Contains("stato del repository", StringComparison.OrdinalIgnoreCase), "git status recognized");
Check(stat.Operation.Contains("D:\\Some Projects\\Repo"), "-C directory recognized, not claimed as root");

var unknown = ProcessOriginAnalyzer.Analyze(Make("git.exe", null));
Check(!unknown.Operation.Contains("push", StringComparison.OrdinalIgnoreCase), "no push inferred without command");

var unverified = parent with { VerifiedByStartTime = false, Name = "git.exe", CommandLine = "git push" };
var noUnverified = ProcessOriginAnalyzer.Analyze(Make("git-remote-https.exe", "git-remote-https origin https://example.com", [unverified]));
Check(!noUnverified.Operation.Contains("Invio delle modifiche"), "unverified ancestor command ignored");

var verified = unverified with { VerifiedByStartTime = true };
var inherited = ProcessOriginAnalyzer.Analyze(Make("git-remote-https.exe", "git-remote-https origin https://example.com", [verified]));
Check(inherited.Operation.Contains("push", StringComparison.OrdinalIgnoreCase), "verified git ancestor provides operation");

var generic = ProcessOriginAnalyzer.Analyze(Make("python.exe", "python run.py"));
Check(generic.Operation.Contains("Operazione sconosciuta", StringComparison.OrdinalIgnoreCase), "generic process does not invent operation");
Check(push.ParentSummary.Contains("orari di avvio", StringComparison.OrdinalIgnoreCase), "verified origin explains timestamp evidence");
Check(noUnverified.ParentSummary.Contains("Orari di avvio insufficienti", StringComparison.OrdinalIgnoreCase), "unverified parent relationship is qualified");
Check(push.Chain.Contains("08/10/2026 09:00:00"), "ancestor start timestamp shown in origin chain");
Check(push.Chain.Contains("08/10/2026 09:01:00"), "selected process start timestamp shown in origin chain");

var now = DateTime.UtcNow;
var start = now.AddHours(-2).AddMinutes(-9);
Check(ProcessStartFormatter.FormatElapsed(start, now) == "2 h 9 min", "elapsed time is formatted using UTC");
Check(ProcessStartFormatter.FormatElapsed(null, now) == "Non disponibile", "unknown start is not invented");
Check(ProcessStartFormatter.FormatStart(null) == "Non disponibile", "unknown start has explicit fallback");
Console.WriteLine($"RESULT: {passed} tests passed");
