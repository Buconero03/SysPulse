using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ProcessLens.Core;
using ProcessLens.Windows;

// Only an owned fixture child is launched; no system app, network probe, shell or elevated command.
if (args.Length > 0 && args[0] == "--child")
{
    string output = args[1], release = args[2];
    var deadline = Stopwatch.StartNew();
    while (!File.Exists(release) && deadline.Elapsed < TimeSpan.FromSeconds(8)) Thread.Sleep(10);
    if (!File.Exists(release)) return 3;
    File.WriteAllText(output + ".tmp", JsonSerializer.Serialize(args.Skip(3).ToArray()));
    File.Move(output + ".tmp", output);
    return 0;
}
static void Check(bool ok, string label)
{ if (!ok) throw new Exception("FAIL new task: " + label); Console.WriteLine("PASS new task: " + label); }
static string Quote(string value)
{
    var b = new StringBuilder("\""); int slash = 0;
    foreach (char c in value)
    {
        if (c == '\\') { slash++; continue; }
        if (c == '"') { b.Append('\\', 2 * slash + 1); b.Append(c); }
        else { b.Append('\\', slash); b.Append(c); }
        slash = 0;
    }
    b.Append('\\', 2 * slash); return b.Append('"').ToString();
}
Check(NewTaskRequest.TryCreate("notepad", "", "", out var simple, out _) && simple!.Program == "notepad",
    "short executable names remain usable without an extension");
const string raw = " --flag=26,0  \"a b\" --token=private & | > %USERNAME% ";
Check(NewTaskRequest.TryCreate(" \"C:\\Program Files\\tool.exe\" ", raw, "\"C:\\Data Files\"", out var request, out _) &&
    request!.Program == @"C:\Program Files\tool.exe" && request.Arguments == raw && request.WorkingDirectory == @"C:\Data Files",
    "quoted executable paths normalize without rewriting argument bytes");
var info = WindowsNewTaskLauncher.CreateStartInfo(request!);
Check(info.FileName == request!.Program && info.Arguments == raw && info.WorkingDirectory == request.WorkingDirectory &&
    !info.UseShellExecute && !info.RedirectStandardOutput && !info.CreateNoWindow && info.Verb.Length == 0,
    "executable and arguments stay separate without injected shell redirect or elevation verb");
foreach (string bad in new[] { "", "  ", "bad\n.exe", "bad\0.exe", "a\"b.exe", "thing.ps1", "script.cmd", "https://example.invalid/a", "C:\\" })
    Check(!NewTaskRequest.TryCreate(bad, "", "", out _, out _), "invalid program rejected before launch: " + bad.Replace("\n", "<LF>").Replace("\0", "<NUL>"));
Check(!NewTaskRequest.TryCreate("tool.exe", "line1\r\nline2", "", out _, out _) &&
    !NewTaskRequest.TryCreate("tool.exe", "", "bad\nfolder", out _, out _), "line breaks cannot split program argument or directory fields");
Check(NewTaskRequest.TryCreate("TOOL.COM", "\t/a", "", out _, out _), "COM executables and actual argument tabs are supported");
int expansionCalls = 0;
string Win32ExpansionFixture(string text)
{
    expansionCalls++;
    int nul = text.IndexOf('\0');
    return nul >= 0 ? text[..nul] : text;
}
Check(Win32ExpansionFixture("tool.exe\0ignored") == "tool.exe",
    "R23 R1 synthetic Win32 null-terminated expansion reproduces the dangerous truncation");
foreach (var probe in new[] {
    (Program: "bad\0.exe", Arguments: "", Directory: "", Label: "program NUL before suffix"),
    (Program: "tool.exe\0ignored", Arguments: "", Directory: "", Label: "program NUL after executable"),
    (Program: "\0tool.exe", Arguments: "", Directory: "", Label: "program initial NUL"),
    (Program: "tool.exe", Arguments: "", Directory: "C:\\Data\0ignored", Label: "directory embedded NUL"),
    (Program: "tool.exe", Arguments: "--token=TOP_SECRET\0ignored", Directory: "", Label: "argument embedded NUL"),
    (Program: "tool.exe\n", Arguments: "", Directory: "", Label: "program trailing LF before trimming"),
    (Program: "tool.exe", Arguments: "", Directory: "C:\\Data\r", Label: "directory trailing CR before trimming")
})
{
    expansionCalls = 0;
    bool accepted = NewTaskRequest.TryCreateCore(probe.Program, probe.Arguments, probe.Directory,
        out var parsed, out string message, Win32ExpansionFixture);
    Check(!accepted && parsed is null && expansionCalls == 0 && !message.Contains("TOP_SECRET"),
        "R23 R1 raw input rejected before native expansion: " + probe.Label);
    Check(!NewTaskRequest.TryCreate(probe.Program, probe.Arguments, probe.Directory, out _, out _, expandEnvironment: false),
        "R23 R1 direct launcher revalidation rejects controls without expansion: " + probe.Label);
}
expansionCalls = 0;
Check(NewTaskRequest.TryCreateCore(" tool.exe ", raw, " C:\\Data ", out var beforeNative, out _, Win32ExpansionFixture) &&
    expansionCalls == 2 && beforeNative!.Program == "tool.exe" && beforeNative.Arguments == raw && beforeNative.WorkingDirectory == @"C:\Data",
    "R23 R1 valid inputs still expand once per path and preserve exact arguments");
Check(!NewTaskRequest.TryCreateCore("tool.exe", "", "", out _, out _, text => text == "tool.exe" ? "bad\0.exe" : text),
    "R23 R1 controls introduced by an expansion result remain rejected");
Check(NewTaskRequest.TryCreate("tool.exe", new string('a', 32755), "", out _, out _) &&
    !NewTaskRequest.TryCreate("tool.exe", new string('a', 32756), "", out _, out _), "combined Windows command-line length has an exact upper boundary");
string? previous = Environment.GetEnvironmentVariable("PROCESSLENS_TEST_TOOL");
try
{
    Environment.SetEnvironmentVariable("PROCESSLENS_TEST_TOOL", @"C:\Tools\tool.exe");
    Check(NewTaskRequest.TryCreate("%PROCESSLENS_TEST_TOOL%", "%PROCESSLENS_TEST_TOOL%", "", out var env, out _) &&
        env!.Program == @"C:\Tools\tool.exe" && env.Arguments == "%PROCESSLENS_TEST_TOOL%",
        "environment variables expand in executable field only, not in arguments");
}
finally { Environment.SetEnvironmentVariable("PROCESSLENS_TEST_TOOL", previous); }
foreach (int code in new[] { 2, 3, 5, 193, 216, 740, 1223, 999 })
{
    var failure = WindowsNewTaskLauncher.Failure(new Win32Exception(code, "TOP_SECRET --password=private"));
    Check(!failure.Started && failure.Pid is null && failure.Message.Contains(code.ToString()) &&
        !failure.Message.Contains("TOP_SECRET") && !failure.Message.Contains("password"), "Windows error retained without command secrets: " + code);
}
Check(!WindowsNewTaskLauncher.Failure(new InvalidOperationException("TOP_SECRET")).Message.Contains("TOP_SECRET"),
    "unexpected launcher exceptions cannot expose argument text");
var launcher = new WindowsNewTaskLauncher();
string temp = Path.Combine(Path.GetTempPath(), "processlens owned task " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var missing = launcher.Start(new("not-a-real-processlens-program.exe", "--password=TOP_SECRET", Path.Combine(temp, "missing")));
    Check(!missing.Started && missing.Pid is null && !missing.Message.Contains("TOP_SECRET"), "missing initial folder cannot launch or invent a PID");
    var invalid = launcher.Start(new("wrong.ps1", "", temp));
    Check(!invalid.Started && invalid.Pid is null, "public launcher revalidates requests independently of the UI");
    string host = Environment.ProcessPath ?? throw new Exception("Fixture host unavailable");
    string[] expected = { "alpha beta", "caffè 26,0", "a&b|c>", "$HOME", "%USERNAME%", "a\"b", @"C:\tail\" };
    string output = Path.Combine(temp, "child result.json"), release = Path.Combine(temp, "allow child.txt");
    var childArgs = new List<string>();
    if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) childArgs.Add(Assembly.GetExecutingAssembly().Location);
    childArgs.AddRange(new[] { "--child", output, release }); childArgs.AddRange(expected);
    Check(NewTaskRequest.TryCreate(host, string.Join(" ", childArgs.Select(Quote)), temp, out var fixture, out _), "owned fixture request can be represented without a shell");
    var started = launcher.Start(fixture!);
    Check(started.Started && started.Pid is > 0 && !File.Exists(output), "successful launch returns actual PID before the owned child is released");
    File.WriteAllText(release, "go");
    var clock = Stopwatch.StartNew();
    while (!File.Exists(output) && clock.Elapsed < TimeSpan.FromSeconds(8)) Thread.Sleep(10);
    Check(File.Exists(output) && JsonSerializer.Deserialize<string[]>(File.ReadAllText(output))!.SequenceEqual(expected),
        "real owned child survives parent handle disposal and receives exact quoted Unicode and metacharacter arguments");
    var absent = launcher.Start(new(Path.Combine(temp, "missing.exe"), "--password=TOP_SECRET", temp));
    Check(!absent.Started && absent.Pid is null && !absent.Message.Contains("TOP_SECRET"), "failed process creation has no false success or secret argument log");
}
finally { Directory.Delete(temp, true); }
Console.WriteLine("New task portable tests complete; owned fixture child only, no user program launched.");
return 0;
