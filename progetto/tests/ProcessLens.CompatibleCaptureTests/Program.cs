using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ProcessLens.Core;
using ProcessLens.Windows;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL compatible capture: " + label); Console.WriteLine("PASS compatible capture: " + label); }
LocalIpInterface binding = new("adapter-A", "Wi-Fi", "192.0.2.1");
IReadOnlyList<LocalIpInterface> interfaces = new[] { binding };
foreach (string address in new[] { "0.0.0.0", "127.0.0.1", "255.255.255.255", "224.0.0.1", "::1", "2001:db8::1", "garbage" })
 Check(!WindowsCompatibleCapture.ValidAddress(address), "invalid wildcard loopback multicast or non-IPv4 excluded: " + address);
Check(WindowsCompatibleCapture.ValidAddress("192.0.2.1") && WindowsCompatibleCapture.ValidAddress("169.254.1.2"), "explicit unicast and link-local IPv4 retained");
foreach (string culture in new[] { "it-IT", "en-US" })
{
 CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
 var pkt = new PacketStub(); var socket = new SocketStub(); int enumerations = 0, opened = 0;
 var client = new WindowsCompatibleCapture(pkt, () => { enumerations++; return interfaces; }, () => { opened++; return socket; }, () => socket.Polls >= 1 ? TimeSpan.FromSeconds(21) : TimeSpan.Zero);
 Check(enumerations == 0 && opened == 0 && pkt.Discoveries == 0, "constructor is lazy [" + culture + "]");
 var discovery = client.Discover(default); var source = discovery.Sources.Single();
 Check(discovery.Available && source.Name.Contains("IP locale") && discovery.Note.Contains("missing PacketMonitorInitialize") && opened == 0,
   "native IP sources remain visible when PktMon exports are missing [" + culture + "]");
 var result = client.Capture(source, default); var row = result.Rows.Single();
 Check(result.Available && !result.Partial && row.Source == "192.0.2.1:12345" && row.Destination == "198.51.100.2:443" && row.Direction == "Out", "owned raw socket feeds actual bounded decoder [" + culture + "]");
 Check(socket.Modes.SequenceEqual(new[] { 3, 0 }) && socket.Bound?.ToString() == "192.0.2.1" && socket.Disposed == 1,
   "IPLEVEL enabled and disabled only on own explicitly bound socket [" + culture + "]");
 Check(socket.LastPoll == 100000 && socket.BufferLength == 65535, "finite poll and full IPv4 datagram buffer [" + culture + "]");
 Check(!result.LossCountsAvailable && row.ComponentText == "N/D" && row.DropReasonText == "N/D" && result.Note.Contains("ricezione nel programma"),
   "unmeasured drops components and driver timestamps are not fabricated [" + culture + "]");
 string report = NativeCaptureEvidence.Report(result);
 Check(report.Contains("Perdite segnalate: N/D") && report.Contains(source.Name) && result.StartedUtc is not null && result.EndedUtc >= result.StartedUtc,
   "copy retains actual source interval and unavailable loss counts [" + culture + "]");
 interfaces = new[] { binding with { Id = "replacement-B" } }; int before = opened;
 var stale = client.Capture(source, default);
 Check(!stale.Available && stale.Note.Contains("cambiato") && opened == before, "IP reused on another adapter cannot open a socket [" + culture + "]");
 interfaces = new[] { binding }; client.Discover(default);
 Check(!client.Capture(source, default).Available, "rediscovery invalidates old opaque local source [" + culture + "]");
 client.Release(); Check(!client.Capture(source, default).Available, "release invalidates local source without background capture [" + culture + "]");
 var audit = new NativeNetworkSnapshot(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-15), true, false,
   new[] { new NetworkAuditSetting("WFP", false, ""), new NetworkAuditSetting("Logon", true, "") }, [], "");
 Check(NetworkEvidenceStatus.Summary(audit).Contains("Copertura incompleta") && NetworkEvidenceStatus.Action(audit).Contains("WFP") && NetworkEvidenceStatus.Action(audit).Contains("disabilitato"),
   "screenshot state puts disabled WFP coverage before empty counts [" + culture + "]");
 Check(NetworkEvidenceStatus.Summary(audit with { Audit = new[] { new NetworkAuditSetting("WFP", true, ""), new NetworkAuditSetting("Logon", true, "") } }).StartsWith("Nessun evento trovato"),
   "enabled empty audit is explained as no matching events [" + culture + "]");
 Check(NetworkEvidenceStatus.Summary(audit with { Available = false }).Contains("N/D") && !NetworkEvidenceStatus.Summary(audit with { Available = false }).Contains("0 eventi"),
   "failed Security acquisition cannot become a zero count [" + culture + "]");
 Check(NetworkEvidenceStatus.Summary(audit with { Audit = new[] { new NetworkAuditSetting("WFP", null, "Win32 5") } }).Contains("non verificata"),
   "unknown audit policy stays distinct from disabled [" + culture + "]");
}
interfaces = new[] { binding, binding with { Id = "adapter-B" } };
Check(new WindowsCompatibleCapture(new PacketStub(), () => interfaces, () => throw new Exception()).Discover(default).Sources.Count == 0,
 "duplicate local IPv4 across adapters is not a selectable ambiguous bind");
interfaces = new[] { binding };
foreach (string error in new[] { "open", "bind", "enable", "poll", "receive", "disable", "bad-length" })
{
 var socket = new SocketStub { Failure = error }; int opens = 0;
 var c = new WindowsCompatibleCapture(new PacketStub(), () => interfaces, () => { opens++; if (error == "open") throw new SocketException(10013); return socket; }, () => socket.Polls >= 1 ? TimeSpan.FromSeconds(21) : TimeSpan.Zero);
 var s = c.Discover(default).Sources.Single(); var result = c.Capture(s, default);
 Check(result.Partial && result.Note.Contains(error == "open" ? "amministratore" : error == "bad-length" ? "non valido" : "fixture " + error), "failure reason retained: " + error);
 Check(socket.Disposed == (error == "open" ? 0 : 1), "owned socket disposed after " + error);
 c.Release();
}
var cappedSocket = new SocketStub(); var cappedClient = new WindowsCompatibleCapture(new PacketStub(), () => interfaces, () => cappedSocket, () => TimeSpan.Zero);
var capped = cappedClient.Capture(cappedClient.Discover(default).Sources.Single(), default);
Check(capped.Rows.Count == 2048 && capped.Partial && cappedSocket.Disposed == 1 && cappedSocket.Modes.Last() == 0, "real loop caps synthetic observations and restores socket"); cappedClient.Release();
using var cancel = new CancellationTokenSource(); var cancelSocket = new SocketStub { Cancel = cancel };
var cancelClient = new WindowsCompatibleCapture(new PacketStub(), () => interfaces, () => cancelSocket, () => TimeSpan.Zero);
var cancelled = cancelClient.Capture(cancelClient.Discover(default).Sources.Single(), cancel.Token);
Check(cancelled.Available && cancelled.Partial && cancelSocket.Disposed == 1 && cancelSocket.Modes.SequenceEqual(new[] { 3, 0 }), "cancellation during native polling restores only own socket"); cancelClient.Release();
var packet = new PacketStub { Available = true }; int rawOpened = 0;
var combined = new WindowsCompatibleCapture(packet, () => interfaces, () => { rawOpened++; return new SocketStub(); });
var both = combined.Discover(default); var packetChoice = both.Sources.Single(s => s.Key.StartsWith("pkt:"));
Check(both.Sources.Count == 2 && combined.Capture(packetChoice, default).Available && packet.Captures == 1 && rawOpened == 0, "explicit PktMon selection delegates to its original verified source");
Check(!combined.Capture(packetChoice with { Name = "forged" }, default).Available && packet.Captures == 1, "mismatched engine source cannot start another backend"); combined.Release();
Check(NetworkEvidenceStatus.Setup.Contains("/failure:enable") && NetworkEvidenceStatus.Setup.Contains(NetworkEvidenceStatus.WfpGuid) &&
 !NetworkEvidenceStatus.Setup.Contains("/success:enable") && NetworkEvidenceStatus.Setup.Contains("/failure:disable"), "audit help limits configuration to failures and states conditional rollback");
Console.WriteLine("Compatible capture tests complete; synthetic network interfaces, datagrams and owned fake sockets only. No live capture or configuration change.");

sealed class PacketStub : INativeCaptureClient
{
 public int Discoveries, Captures; public bool Available;
 public NativeCaptureDiscovery Discover(CancellationToken cancellation) { Discoveries++; return new(Available, Available ? new[] { new NativeCaptureSource("original", "fixture") } : [], Available ? "PktMon fixture" : "missing PacketMonitorInitialize"); }
 public NativeCaptureResult Capture(NativeCaptureSource source, CancellationToken cancellation) { if (source.Key != "original") throw new Exception("wrong source"); Captures++; return new(true, false, [], 0, 0, "fixture"); }
 public NativeWifiResult ReadWifi(CancellationToken cancellation) => new(false, true, [], "fixture Wi-Fi denied");
 public void Release() { }
}
sealed class SocketStub : ILocalIpCaptureSocket
{
 public string Failure = ""; public int Polls, LastPoll, BufferLength, Disposed; public IPAddress? Bound; public CancellationTokenSource? Cancel;
 public readonly List<int> Modes = new();
 private void Fail(string stage) { if (Failure == stage) throw new IOException("fixture " + stage); }
 public void Bind(IPAddress address) { Fail("bind"); Bound = address; }
 public void SetReceiveMode(int mode) { Modes.Add(mode); Fail(mode == 3 ? "enable" : "disable"); }
 public bool Poll(int microseconds) { Fail("poll"); Polls++; LastPoll = microseconds; Cancel?.Cancel(); return true; }
 public int Receive(byte[] buffer) { Fail("receive"); BufferLength = buffer.Length; if (Failure == "bad-length") return 1; byte[] data = Convert.FromHexString("450000280000000040060000C0000201C6336402303901BB00000000000000005000000000000000"); data.CopyTo(buffer, 0); return data.Length; }
 public void Dispose() { Disposed++; }
}
