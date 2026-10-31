using System.Globalization;
using System.Reflection;
using ProcessLens.Core;
using ProcessLens.Windows;

if (args.Length >= 2 && args[0] == "--capture-fixture")
{
    Console.WriteLine("OWNED FIXTURE"); Console.Out.Flush();
    if (args[1] == "wait") Thread.Sleep(10000);
    else if (args[1] == "overflow") { Console.Write(new string('x', PacketCapturePolicy.MaximumTextChars + 65536)); Console.Out.Flush(); }
    else if (args.Length > 2) Console.WriteLine(args[2]);
    return;
}
static void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL packet capture: " + label);
    Console.WriteLine("PASS packet capture: " + label);
}
static bool Throws(Action action)
{
    try { action(); return false; } catch (ArgumentException) { return true; }
}
const string device = @"\Device\NPF_{00112233-4455-6677-8899-AABBCCDDEEFF}";
var adapter = new CaptureInterface(device, "Wi-Fi");
Check(PacketCapturePolicy.LocalInterface(device) && PacketCapturePolicy.LocalInterface(@"\Device\NPF_Loopback"), "local Npcap interface identifiers accepted");
foreach (string invalid in new[] { "1", "-i anything", "rpcap://host/interface", "TCP@host:9", "-", "Wi-Fi", @"\Device\NPF_{not-a-guid}" })
    Check(!PacketCapturePolicy.LocalInterface(invalid), "remote pipe index or injected interface rejected: " + invalid);
var interfaces = PacketCapturePolicy.ParseInterfaces("1. " + device + " (Wi-Fi)\n2. rpcap://host/interface (remote)\n3. " + device + " (duplicate)");
Check(interfaces.Count == 1 && interfaces[0].Name == device, "interface parser retains exact local identity and removes duplicates");
Check(PacketCapturePolicy.HasMonitorLinkType("Data link types:\n  IEEE802_11_RADIO (802.11 plus radiotap header)\n") &&
    !PacketCapturePolicy.HasMonitorLinkType("Error: no support for IEEE802_11_RADIO") &&
    !PacketCapturePolicy.HasMonitorLinkType("EN10MB (Ethernet)"), "monitor check requires an actual declared 802.11 link type");
string root = Path.Combine(Path.GetTempPath(), "ProcessLens-CaptureTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string path = Path.Combine(root, "space & literal ; file.pcapng");
    var normal = PacketCapturePolicy.CaptureArguments(adapter, false, path);
    Check(normal.Contains("-p") && !normal.Contains("-I") && normal[^1] == path &&
        string.Join("|", normal).Contains("-a|duration:20|-a|filesize:10000|-c|10000|-s|256|-B|2"), "normal capture is explicitly selected bounded and not monitor mode");
    Check(PacketCapturePolicy.CaptureArguments(adapter, true, path).Contains("-I"), "monitor mode is an explicit separate argument");
    Check(Throws(() => PacketCapturePolicy.CaptureArguments(adapter with { Name = "1" }, false, path)) &&
        Throws(() => PacketCapturePolicy.CaptureArguments(adapter, false, @"\\server\share\test.pcapng")), "unsafe or ambiguous capture destinations and interfaces fail closed");
    string beacon = "1\t02:aa:bb:cc:dd:ee\tff:ff:ff:ff:ff:ff\t02:aa:bb:cc:dd:ee\t0\t8\t486f6d65\t2412\t-40\n";
    string data = "2\t02:11:22:33:44:55\t02:aa:bb:cc:dd:ee\t02:aa:bb:cc:dd:ee\t2\t0\t\t\t\n";
    foreach (string name in new[] { "it-IT", "en-US" })
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            var evidence = PacketEvidenceParser.Parse(beacon + data + "3\t\t\t\t\t\t\t\t\n");
            Check(evidence.Packets == 3 && evidence.WifiFrames == 2 && evidence.Rows.Any(r => r.Kind == "Rete annunciata" && r.Ssid == "Home" && r.Frequency == "2412 MHz"), "beacon metadata is observed without inventing a phone hotspot [" + name + "]");
            Check(evidence.Rows.Any(r => r.Mac == "02-11-22-33-44-55" && r.Signal == "N/D" && r.Frequency == "N/D") &&
                !evidence.Rows.Any(r => r.Mac.StartsWith("FF-")), "missing radio values stay unavailable and broadcast MACs are not devices [" + name + "]");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
    var ordinary = PacketEvidenceParser.Parse("1\t\t\t\t\t\t\t\t\n");
    Check(ordinary.Packets == 1 && ordinary.WifiFrames == 0 && ordinary.Note.Contains("solo intestazioni Ethernet"), "no decoded Wi-Fi does not establish absence of wireless clients");
    Check(PacketEvidenceParser.Parse("bad\tline\n").Limited, "malformed output remains explicitly incomplete");
    Check(PacketEvidenceParser.Parse(beacon.Replace("486f6d65", "410a42")).Rows.Any(r => r.Ssid == @"A\u000AB"), "SSID control characters cannot create fake log lines");
    Check(PacketEvidenceParser.Parse(beacon.Replace("\t0\t8\t", "\t0\t4\t")).Rows.All(r => r.Ssid == "N/D"), "probe request SSID does not become an advertised access point");
    foreach (string exe in new[] { "dumpcap.exe", "tshark.exe", "Wireshark.exe" }) File.WriteAllText(Path.Combine(root, exe), "fixture, never executed");
    var fake = new FixtureRunner(beacon);
    var client = new WiresharkTools(fake);
    var discovery = await client.DiscoverAsync(Path.Combine(root, "dumpcap.exe"), CancellationToken.None);
    var tools = discovery.Tools!;
    Check(discovery.Interfaces.Single().Name == device && fake.Calls[0].Args.SequenceEqual(new[] { "-D" }), "discovery only lists interfaces through the injected runner");
    bool blocked = false;
    try { await client.CaptureAsync(tools, adapter, true, path, CancellationToken.None); } catch (InvalidOperationException) { blocked = true; }
    Check(blocked && !File.Exists(path), "unverified monitor cannot create a file or launch capture");
    Check((await client.CheckMonitorAsync(tools, adapter, CancellationToken.None)).Supported, "monitor capability is tied to the selected tool and interface");
    var captured = await client.CaptureAsync(tools, adapter, true, path, CancellationToken.None);
    Check(captured.Complete && fake.Calls.Last().Args.Contains("-I") && File.Exists(path), "capture reserves a new file and applies explicit monitor arguments");
    blocked = false;
    try { await client.CaptureAsync(tools, adapter, false, path, CancellationToken.None); } catch (IOException) { blocked = true; }
    Check(blocked, "existing capture files cannot be overwritten");
    var result = await client.AnalyzeAsync(tools, path, CancellationToken.None);
    Check(result.WifiFrames == 1 && result.Note.Contains("SHA-256") && fake.Calls.Last().Args.Contains("-n") &&
        fake.Calls.Last().Args.Contains("2048") && fake.Calls.Last().Exe.EndsWith("tshark.exe"), "analysis is offline bounded disables names and identifies the file hash");
    fake.Fail = true;
    blocked = false;
    try { await client.AnalyzeAsync(tools, path, CancellationToken.None); } catch (IOException ex) { blocked = ex.Message.Contains("fixture error"); }
    Check(blocked, "failed decoder output cannot become a valid empty-network report");
    using (var tooLarge = File.Create(Path.Combine(root, "large.pcapng"))) tooLarge.SetLength(PacketCapturePolicy.MaximumFileBytes + 1L);
    blocked = false;
    try { await client.AnalyzeAsync(tools, Path.Combine(root, "large.pcapng"), CancellationToken.None); } catch (IOException) { blocked = true; }
    Check(blocked, "oversized imported capture is rejected before a decoder is launched");
}
finally { Directory.Delete(root, true); }

// Independent process tests launch only this test executable, never a packet tool or target process.
string self = Environment.ProcessPath!;
string[] SelfArgs(string mode, string? value = null)
{
    var values = new List<string>();
    if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) values.Add(Assembly.GetExecutingAssembly().Location);
    values.AddRange(new[] { "--capture-fixture", mode }); if (value is not null) values.Add(value);
    return values.ToArray();
}
var runner = new CaptureCommandRunner();
var echo = await runner.RunAsync(self, SelfArgs("echo", "literal & ; $(not-a-command)"), 10, CancellationToken.None);
Check(echo.Success && echo.Output.Contains("OWNED FIXTURE") && echo.Output.Contains("literal & ; $(not-a-command)"), "real runner passes arguments without a shell to its own fixture");
var timeout = await runner.RunAsync(self, SelfArgs("wait"), 3, CancellationToken.None);
Check(timeout.Interrupted && !timeout.Success, "real runner stops only its own child at timeout");
using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
var cancelled = await runner.RunAsync(self, SelfArgs("wait"), 10, stop.Token);
Check(cancelled.Interrupted, "real runner cancellation releases its own child");
var overflow = await runner.RunAsync(self, SelfArgs("overflow"), 10, CancellationToken.None);
Check(!overflow.Success && overflow.Output.Length <= PacketCapturePolicy.MaximumTextChars, "real runner output budget cannot grow without bound");
Console.WriteLine("Packet capture tests complete; synthetic data and owned test children only, no live capture or driver changes.");

sealed class FixtureRunner(string beacon) : ICaptureCommandRunner
{
    public readonly List<(string Exe, string[] Args)> Calls = new();
    public bool Fail;
    public Task<CaptureCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, int seconds, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Calls.Add((executable, arguments.ToArray()));
        if (Fail) return Task.FromResult(new CaptureCommandResult(1, "", "fixture error", false));
        if (arguments.Contains("-D")) return Task.FromResult(new CaptureCommandResult(0, "1. \\Device\\NPF_{00112233-4455-6677-8899-AABBCCDDEEFF} (Wi-Fi)", "", false));
        if (arguments.Contains("-L")) return Task.FromResult(new CaptureCommandResult(0, "IEEE802_11_RADIO (802.11 plus radiotap)", "", false));
        if (arguments.Contains("-w")) { int index = Array.IndexOf(arguments.ToArray(), "-w"); File.WriteAllText(arguments[index + 1], "synthetic file bytes"); return Task.FromResult(new CaptureCommandResult(0, "", "", false)); }
        return Task.FromResult(new CaptureCommandResult(0, beacon, "", false));
    }
}
