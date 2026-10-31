using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ProcessLens.Core;
using ProcessLens.Windows;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL native capture: " + label); Console.WriteLine("PASS native capture: " + label); }
void Reject(Action action, string label) { try { action(); } catch (InvalidDataException) { Check(true, label); return; } throw new Exception("FAIL rejection: " + label); }
Check(Marshal.SizeOf<PktmonMetadata>() == 48 && Marshal.OffsetOf<PktmonMetadata>(nameof(PktmonMetadata.Timestamp)).ToInt32() == 40 &&
    Marshal.OffsetOf<PktmonMetadata>(nameof(PktmonMetadata.DropReason)).ToInt32() == 24, "independent Windows metadata size and aligned offsets");
Check(Marshal.SizeOf<PktmonStreamConfiguration>() == (IntPtr.Size == 8 ? 32 : 16) &&
    Marshal.OffsetOf<PktmonStreamConfiguration>(nameof(PktmonStreamConfiguration.BufferSizeMultiplier)).ToInt32() == 3 * IntPtr.Size,
    "stream configuration pointer and UINT16 layout");
Check(Marshal.SizeOf<PktmonDataDescriptor>() == (IntPtr.Size == 8 ? 32 : 28) &&
    Marshal.OffsetOf<PktmonDataDescriptor>(nameof(PktmonDataDescriptor.PacketLength)).ToInt32() == IntPtr.Size + 12,
    "stream descriptor independent size and packet offset");
byte[] ip = Convert.FromHexString("450000280000000040060000C0000201C6336402303901BB00000000000000005000000000000000");
byte[] ethernet = Convert.FromHexString("00112233445566778899AABB0800").Concat(ip).ToArray();
long stamp = new DateTime(2026, 10, 21, 12, 34, 56, DateTimeKind.Utc).ToFileTimeUtc();
foreach (string culture in new[] { "it-IT", "en-US" })
{
 CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
 var row = NativeCaptureEvidence.Decode(ethernet, 1, 4, 22, 0, stamp);
 Check(row.Protocol == "TCP/IPv4" && row.Source == "192.0.2.1:12345" && row.Destination == "198.51.100.2:443" && row.Direction == "Tx" && row.Component == 22,
    "independent Ethernet IPv4 TCP fixture [" + culture + "]");
 Check(row.Time.Length == 12 && !row.Time.Contains(',') && row.Bytes == ethernet.Length, "native FILETIME formatted with invariant timestamp [" + culture + "]");
 byte[] fragment = ip.ToArray(); fragment[6] = 0x20;
 Check(!NativeCaptureEvidence.Decode(fragment, 3, 1, 1, 0, stamp).Source.Contains(':'), "fragment does not acquire transport ports [" + culture + "]");
 byte[] shortIp = ip.ToArray(); shortIp[0] = 0x4f;
 Check(NativeCaptureEvidence.Decode(shortIp, 3, 0, 0, 0, stamp).Source == "N/D", "invalid IPv4 header cannot acquire endpoints [" + culture + "]");
 byte[] shortTotal = ip.ToArray(); shortTotal[3] = 20;
 Check(NativeCaptureEvidence.Decode(shortTotal, 3, 1, 1, 0, stamp).Source == "192.0.2.1", "IPv4 total length bounds transport parsing [" + culture + "]");
 var unknown = NativeCaptureEvidence.Decode(new byte[] { 0x45, 0, 0 }, 2, 99, 0, 0, -1);
 Check(unknown.Protocol.Contains("non decodificato") && unknown.Source == "N/D" && unknown.Time == "N/D" && unknown.Direction == "N/D", "raw 802.11 and unknown direction remain undecoded [" + culture + "]");
 byte[] vlan = ethernet[..12].Concat(Convert.FromHexString("810000010800")).Concat(ip).ToArray();
 Check(NativeCaptureEvidence.Decode(vlan, 1, 1, 1, 0, stamp).Destination.EndsWith(":443"), "VLAN header handled without guessing IP offset [" + culture + "]");
 byte[] v6 = new byte[44]; v6[0] = 0x60; v6[5] = 4; v6[6] = 17; v6[23] = 1; v6[39] = 2; v6[40] = 0x12; v6[41] = 0x34; v6[43] = 53;
 Check(NativeCaptureEvidence.Decode(v6, 3, 2, 1, 0, stamp).Destination == "[::2]:53", "IPv6 direct UDP header fixture [" + culture + "]");
 v6[6] = 44;
 Check(NativeCaptureEvidence.Decode(v6, 3, 2, 1, 0, stamp).Destination == "::2", "IPv6 extensions and fragments do not acquire ports [" + culture + "]");
 // Hard-coded Windows SDK offsets, independent of production constants and managed interop structs.
 byte[] bss = new byte[368]; BinaryPrimitives.WriteUInt32LittleEndian(bss, 368); BinaryPrimitives.WriteUInt32LittleEndian(bss.AsSpan(4), 1);
 byte[] name = Encoding.UTF8.GetBytes("Rete 🧪\r\n[FAKE]"); BinaryPrimitives.WriteUInt32LittleEndian(bss.AsSpan(8), (uint)name.Length); name.CopyTo(bss, 12);
 Convert.FromHexString("021122334455").CopyTo(bss, 48); BinaryPrimitives.WriteUInt32LittleEndian(bss.AsSpan(56), 1);
 BinaryPrimitives.WriteInt32LittleEndian(bss.AsSpan(64), -47); BinaryPrimitives.WriteUInt32LittleEndian(bss.AsSpan(100), 2412000);
 var wifi = NativeCaptureEvidence.ParseBss(bss, "Wi-Fi").Single();
 Check(wifi.Bssid == "02-11-22-33-44-55" && wifi.Rssi == -47 && wifi.Frequency == "2412 MHz" && wifi.BssType == "Punto di accesso",
    "independent BSS binary offsets preserve radio measurements [" + culture + "]");
 Check(wifi.Ssid.Contains("🧪") && !wifi.Ssid.Contains('\n') && !wifi.Ssid.Contains('\r'), "SSID control bytes cannot inject report events [" + culture + "]");
 bss[12] = 0xff;
 Check(NativeCaptureEvidence.ParseBss(bss, "Wi-Fi").Single().Ssid.StartsWith("SSID hex:"), "non UTF8 SSID retains exact bytes as hex [" + culture + "]");
 BinaryPrimitives.WriteUInt32LittleEndian(bss.AsSpan(8), 0);
 Check(NativeCaptureEvidence.ParseBss(bss, "Wi-Fi").Single().Ssid == "SSID non trasmesso", "empty SSID is not invented [" + culture + "]");
 BinaryPrimitives.WriteUInt32LittleEndian(bss.AsSpan(8), 33); Reject(() => NativeCaptureEvidence.ParseBss(bss, "Wi-Fi"), "oversized SSID rejected [" + culture + "]");
 bss[8] = 0; bss[4] = 2; Reject(() => NativeCaptureEvidence.ParseBss(bss, "Wi-Fi"), "BSS count cannot exceed actual buffer [" + culture + "]");
 bss[4] = 1; bss[56] = 3; Reject(() => NativeCaptureEvidence.ParseBss(bss, "Wi-Fi"), "unknown BSS type is not an invented access point [" + culture + "]");
 var report = NativeCaptureEvidence.Report(new NativeCaptureResult(false, true, Array.Empty<NativePacketRow>(), 0, 0, "access denied"));
 Check(report.Contains("Osservazioni: N/D") && report.Contains("access denied") && report.Contains("non all’intera rete"), "unavailable capture preserves error and coverage [" + culture + "]");
 Check(NativeCaptureEvidence.Report(new NativeWifiResult(true, false, new[] { wifi }, "cache")).Contains("non client connessi"), "BSS report distinguishes access points and clients [" + culture + "]");
}
Reject(() => NativeCaptureEvidence.ParseBss(new byte[7], "fixture"), "truncated BSS list rejected");
Check(NativeCaptureEvidence.Clean("a\u2028b\t\nc") == "a b  c", "Unicode line separator and control text normalized");
if (IntPtr.Size == 8)
{
 IntPtr list = Marshal.AllocHGlobal(256);
 try
 {
  Marshal.Copy(new byte[256], 0, list, 256); Marshal.WriteInt32(list, 1); IntPtr spec = IntPtr.Add(list, 32); Marshal.WriteIntPtr(list, 8, spec); Marshal.WriteInt32(spec, 1);
  Marshal.Copy(Encoding.Unicode.GetBytes("Scheda fixture\0"), 0, IntPtr.Add(spec, 4), 30);
  Check(PktmonSessionApi.ParseSources(list, 256).Single().Specification == spec, "source keeps the exact owned native specification pointer");
  Marshal.WriteIntPtr(list, 8, IntPtr.Add(list, 300)); Reject(() => PktmonSessionApi.ParseSources(list, 256), "out-of-buffer source pointer rejected before dereference");
  Marshal.WriteIntPtr(list, 8, IntPtr.Zero); Reject(() => PktmonSessionApi.ParseSources(list, 256), "null source pointer rejected");
  Marshal.WriteInt32(list, 257); Reject(() => PktmonSessionApi.ParseSources(list, 256), "source count capped before pointer array read");
 }
 finally { Marshal.FreeHGlobal(list); }
}
var fixture = new FixtureApi();
var client = new WindowsNativeCapture(() => fixture, (_, duration) => { Check(duration.TotalSeconds == 20, "capture wait always receives the twenty second budget"); return WaitHandle.WaitTimeout; });
Check(fixture.Calls.Count == 0, "construction is lazy");
var discovery = client.Discover(default); var choice = discovery.Sources.Single();
Check(discovery.Available && fixture.Calls.Count == 0, "discovery does not create or activate a session");
var sampled = client.Capture(choice, default);
Check(sampled.Available && !sampled.Partial && sampled.Rows.Single().Source == "192.0.2.1:12345", "owned API boundary feeds real collector and decoder");
Check(fixture.Configuration.BufferSizeMultiplier == 1 && fixture.Configuration.TruncationSize == 128, "native stream uses bounded ring multiplier and snap length");
Check(fixture.Calls.SequenceEqual(new[] { "session", "source", "stream", "attach", "active", "inactive", "close-stream", "close-session" }), "owned session configured before activation and released in order");
Check(fixture.LastName.StartsWith("ProcessLens-") && fixture.LastName.Length < 256, "session uses a fresh private name");
var prior = choice; client.Discover(default);
Check(!client.Capture(prior, default).Available, "rediscovery invalidates stale source tokens");
Check(!client.Capture(choice with { Key = "forged" }, default).Available, "unknown selection cannot create a capture session");
choice = client.Discover(default).Sources.Single();
Check(!client.Capture(choice with { Name = "different" }, default).Available, "source label mismatch fails closed");
client.Release(); Check(!client.Capture(choice, default).Available && fixture.Disposals > 0, "release invalidates selection and closes native discovery");
foreach (string failure in new[] { "session", "source", "stream", "attach", "active" })
{
 var f = new FixtureApi { FailAt = failure }; var c = new WindowsNativeCapture(() => f, (_, _) => WaitHandle.WaitTimeout);
 var d = c.Discover(default); var result = c.Capture(d.Sources.Single(), default);
 Check(!result.Available && result.Partial && result.Note.Contains("fixture " + failure), "native error preserved at " + failure);
 Check(f.Calls.Contains("close-session") == (failure != "session") && f.Calls.Contains("close-stream") == (failure is "attach" or "active"), "resource rollback after " + failure);
 c.Release();
}
foreach (string mode in new[] { "overflow", "bad-offset", "fatal", "loss", "unexpected-stop" })
{
 var f = new FixtureApi { Mode = mode }; var c = new WindowsNativeCapture(() => f, (_, _) => WaitHandle.WaitTimeout);
 var result = c.Capture(c.Discover(default).Sources.Single(), default);
 Check(result.Available && result.Partial && result.Rows.Count <= 2048, "bounded partial acquisition: " + mode);
 if (mode == "loss") Check(result.MissedWrites == 3 && result.MissedReads == 2, "native loss counts preserved without asserting complete capture");
 if (mode == "overflow") Check(result.Rows.Count == 2048, "capture observations never exceed 2048");
 c.Release();
}
var unavailable = new WindowsNativeCapture(() => throw new NotSupportedException("missing export"));
Check(!unavailable.Discover(default).Available && unavailable.Discover(default).Note.Contains("missing export"), "missing API exports are unavailable with a reason");
using var cancel = new CancellationTokenSource(); cancel.Cancel();
try { new WindowsNativeCapture(() => throw new Exception("must not initialize")).Discover(cancel.Token); throw new Exception("FAIL cancellation"); }
catch (OperationCanceledException) { Check(true, "cancellation before discovery does not load native code"); }
Console.WriteLine("Native capture regressions complete; synthetic buffers and owned interop fixtures only. No live packet capture or radio changes.");

sealed class FixtureApi : IPktmonSessionApi
{
 public IReadOnlyList<PktmonSource> Sources { get; } = new[] { new PktmonSource((IntPtr)77, "Scheda fixture") };
 public readonly List<string> Calls = new(); public string FailAt = "", Mode = "", LastName = ""; public int Disposals;
 public PktmonStreamConfiguration Configuration;
 private void Call(string name) { Calls.Add(name); if (FailAt == name) throw new InvalidOperationException("fixture " + name); }
 public IntPtr CreateSession(string name) { Call("session"); LastName = name; return (IntPtr)101; }
 public void AddSource(IntPtr session, IntPtr spec) { if (session != (IntPtr)101 || spec != (IntPtr)77) throw new Exception("foreign handle"); Call("source"); }
 public IntPtr CreateStream(PktmonStreamConfiguration configuration) { Call("stream"); Configuration = configuration; return (IntPtr)102; }
 public void Attach(IntPtr session, IntPtr stream) { if (session != (IntPtr)101 || stream != (IntPtr)102) throw new Exception("foreign output"); Call("attach"); }
 public void SetActive(IntPtr session, bool active)
 {
  if (session != (IntPtr)101) throw new Exception("foreign session"); Call(active ? "active" : "inactive"); if (!active) return;
  var callback = Marshal.GetDelegateForFunctionPointer<PktmonDataCallback>(Configuration.DataCallback);
  var events = Marshal.GetDelegateForFunctionPointer<PktmonEventCallback>(Configuration.EventCallback);
  if (Mode is "fatal" or "unexpected-stop") { IntPtr info = Marshal.AllocHGlobal(16); try { Marshal.Copy(new byte[16], 0, info, 16); Marshal.WriteInt32(info, 4, 55); events(IntPtr.Zero, info, Mode == "fatal" ? 2 : 1); } finally { Marshal.FreeHGlobal(info); } return; }
  byte[] bytes = new byte[88];
  // Independent metadata fixture: direction 4 at 12, raw IP type 3 at 14, component 22 at 16, FILETIME at 40.
  bytes[12] = 4; bytes[14] = 3; bytes[16] = 22;
  BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(40), new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc());
  Convert.FromHexString("450000280000000040060000C0000201C6336402303901BB00000000000000005000000000000000").CopyTo(bytes, 48);
  IntPtr body = Marshal.AllocHGlobal(bytes.Length), descriptor = Marshal.AllocHGlobal(32);
  try
  {
   Marshal.Copy(bytes, 0, body, bytes.Length);
   var d = new PktmonDataDescriptor { Data = body, DataSize = (uint)bytes.Length, PacketOffset = Mode == "bad-offset" ? uint.MaxValue : 48, PacketLength = 40, MissedWrites = Mode == "loss" ? 3u : 0u, MissedReads = Mode == "loss" ? 2u : 0u };
   Marshal.StructureToPtr(d, descriptor, false);
   for (int i = 0; i < (Mode == "overflow" ? 2050 : 1); i++) callback(IntPtr.Zero, descriptor);
  }
  finally { Marshal.FreeHGlobal(descriptor); Marshal.FreeHGlobal(body); }
 }
 public void CloseStream(IntPtr stream) { if (stream != (IntPtr)102) throw new Exception("foreign stream"); Call("close-stream"); }
 public void CloseSession(IntPtr session) { if (session != (IntPtr)101) throw new Exception("foreign session"); Call("close-session"); }
 public void Dispose() { Disposals++; }
}
