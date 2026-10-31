using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ProcessLens.Core;
using ProcessLens.Windows;

static void Check(bool ok, string text)
{
    if (!ok) throw new Exception("FAIL network devices: " + text);
    Console.WriteLine("PASS network devices: " + text);
}
const string mac = "02-11-22-33-44-55";
string scope = NetworkDeviceRules.NetworkKey("adapter-A", new[] { ("192.168.1.1", "02-AA-BB-CC-DD-EE") });
string otherScope = NetworkDeviceRules.NetworkKey("adapter-A", new[] { ("192.168.1.1", "02-AA-BB-CC-DD-EF") });
Check(scope.Length == 64 && scope != otherScope, "gateway changes cannot inherit recognition");
Check(NetworkDeviceRules.NetworkKey("adapter-A", new[] { ("fe80::1%5", "02-AA-BB-CC-DD-EE") }) ==
    NetworkDeviceRules.NetworkKey("adapter-A", new[] { ("fe80::1%9", "02-AA-BB-CC-DD-EE") }),
    "IPv6 scope index changes do not replace the stable adapter and gateway fingerprint");
Check(NetworkDeviceRules.NetworkKey("adapter-A", new[] { ("192.168.1.1", "") }) == "",
    "missing gateway MAC cannot establish a recognition scope");
Check(NetworkDeviceRules.NormalizeMac("02:11:22:33:44:55") == mac &&
    NetworkDeviceRules.NormalizeMac("021122334455") == mac, "MAC normalization retains locally administered addresses");
foreach (string invalid in new[] { "", "00-00-00-00-00-00", "FF-FF-FF-FF-FF-FF", "01-00-5E-00-00-01", "garbage", "02-11" })
    Check(NetworkDeviceRules.NormalizeMac(invalid) == "", "invalid or multicast MAC rejected: " + invalid);
foreach (string ip in new[] { "127.0.0.1", "224.0.0.1", "255.255.255.255", "0.0.0.0", "::", "::%5", "::1", "ff02::1", "invalid" })
    Check(!NetworkDeviceRules.IsUnicastAddress(ip), "non-device address excluded: " + ip);

var a = new NetworkNeighbor("adapter-A", "Wi-Fi", scope, "192.168.1.2", mac, NeighborState.Reachable, false);
var capture = new NetworkDeviceSnapshot(DateTime.UnixEpoch, true, false, "", 1,
    new[] { a, a with { Address = "fe80::2%5", State = NeighborState.Stale } });
var empty = new RecognitionCatalog(Array.Empty<DeviceRecognition>(), "");
var known = new RecognitionCatalog(new[] { new DeviceRecognition(scope, mac, "Telefono 🧪") }, "");
foreach (string cultureName in new[] { "it-IT", "en-US" })
{
    var saved = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        var view = NetworkDeviceRules.Present(capture, empty);
        Check(view.Rows.Count == 1 && view.Rows[0].Addresses.Contains("192.168.1.2") &&
            view.Rows[0].Addresses.Contains("fe80::2%5"), "IPv4 and IPv6 share one MAC row [" + cultureName + "]");
        Check(view.Color == "#A16207" && !view.Rows[0].Recognized && view.Rows[0].Name == "Nome non disponibile",
            "unknown device has no invented identity or intrusion claim [" + cultureName + "]");
        var green = NetworkDeviceRules.Present(capture, known);
        Check(green.Color == "#047857" && green.Rows[0].Recognized && green.Rows[0].Name == "Telefono 🧪",
            "green means user-recognized observed entries only [" + cultureName + "]");
        Check(NetworkDeviceRules.Present(capture with { Partial = true, Note = "lettura parziale" }, known).Color == "#64748B",
            "partial acquisition cannot become green [" + cultureName + "]");
    }
    finally { CultureInfo.CurrentCulture = saved; }
}
var stale = NetworkDeviceRules.Present(capture with { Neighbors = new[] { a with { State = NeighborState.Stale } } }, empty);
Check(stale.Rows.Single().Presence == "In cache · presenza non confermata", "stale cache is not current connectivity");
var permanent = NetworkDeviceRules.Present(capture with { Neighbors = new[] { a with { State = NeighborState.Permanent } } }, empty);
Check(permanent.Rows.Single().Presence.Contains("presenza non confermata"), "permanent entry is not confirmed presence");
var noMac = NetworkDeviceRules.Present(capture with { Neighbors = new[] { a with { Mac = "" } } }, empty);
Check(noMac.Color == "#64748B" && noMac.Summary.Contains("0 dispositivi") && noMac.Summary.Contains("1 indirizzo senza MAC") &&
    !noMac.Rows[0].CanRecognize, "unresolved address retained but not counted as a device");
Check(NetworkDeviceRules.Present(capture with { Neighbors = Array.Empty<NetworkNeighbor>() }, known).Color == "#64748B",
    "empty cache cannot certify a safe or empty network");
Check(NetworkDeviceRules.Present(capture with { Available = false, Note = "errore di lettura" }, known) is
    { Color: "#64748B", Rows.Count: 0, Summary: "Dispositivi: N/D", Note: "errore di lettura" },
    "failed acquisition preserves its error and unavailable count");
var changed = NetworkDeviceRules.Present(capture with { Neighbors = new[] { a with { NetworkKey = otherScope } } }, known);
Check(!changed.Rows[0].Recognized, "same MAC on another scope remains unrecognized");
var missingScope = NetworkDeviceRules.Present(capture with { Neighbors = new[] { a with { NetworkKey = "" } } }, known);
Check(missingScope.Color == "#64748B" && !missingScope.Rows[0].CanRecognize && missingScope.Summary.Contains("riconoscimento incompleto"),
    "missing scope does not show an invented zero unknown devices");
var unreadable = NetworkDeviceRules.Present(capture, new(Array.Empty<DeviceRecognition>(), "file non leggibile"));
Check(unreadable.Color == "#64748B" && unreadable.Note.Contains("file non leggibile") && !unreadable.Rows[0].CanRecognize,
    "registry failure is visible and blocks recognition");
var twoAdapters = NetworkDeviceRules.Present(capture with { Neighbors = new[] { a, a with { InterfaceId = "adapter-B", InterfaceName = "Ethernet" } } }, empty);
Check(twoAdapters.Rows.Count == 2, "same MAC on distinct adapters is not merged");

var tracker = new NetworkDeviceTracker();
Check(tracker.Observe(capture, NetworkDeviceRules.Present(capture, empty)) == 0, "first complete capture establishes baseline without a new-device alert");
var b = a with { Address = "192.168.1.3", Mac = "02-11-22-33-44-66" };
var next = capture with { Neighbors = new[] { a, b } };
Check(tracker.Observe(next with { Partial = true }, NetworkDeviceRules.Present(next, empty)) == 0,
    "partial reads cannot change novelty history");
Check(tracker.Observe(next, NetworkDeviceRules.Present(next, empty)) == 1 &&
    tracker.Observe(next, NetworkDeviceRules.Present(next, empty)) == 0, "new address alerts once across repeated refreshes");
Check(tracker.Observe(capture, NetworkDeviceRules.Present(capture, empty)) == 0 &&
    tracker.Observe(next, NetworkDeviceRules.Present(next, empty)) == 0, "temporary absence does not create another new-device alert");
var otherNetwork = next with { Neighbors = new[] { b with { NetworkKey = otherScope } } };
Check(tracker.Observe(otherNetwork, NetworkDeviceRules.Present(otherNetwork, empty)) == 0, "another network gets its own initial baseline");
var knownNew = new RecognitionCatalog(new[] { new DeviceRecognition(scope, b.Mac, "PC scelto") }, "");
var recognizedTracker = new NetworkDeviceTracker();
recognizedTracker.Observe(capture, NetworkDeviceRules.Present(capture, empty));
Check(recognizedTracker.Observe(next, NetworkDeviceRules.Present(next, knownNew)) == 0, "a user-recognized device does not raise an unknown-device alert");

string temporary = Path.Combine(Path.GetTempPath(), "ProcessLens-NetworkTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    string path = Path.Combine(temporary, "devices.json"); var store = new DeviceRecognitionStore(path);
    Check(store.Load() is { Available: true, Devices.Count: 0 }, "missing local registry is an empty catalog");
    Check(store.Save(known) == "" && store.Load().Devices.Single() == known.Devices.Single(), "recognition and Unicode name survive save and reload");
    Check(!Directory.EnumerateFiles(temporary, "*.tmp-*").Any(), "atomic save leaves no temporary files");
    string original = File.ReadAllText(path);
    Check(store.Save(new(new[] { new DeviceRecognition(scope, mac, "bad\nname") }, "")).Length > 0 &&
        File.ReadAllText(path) == original, "invalid user labels cannot overwrite a valid registry");
    File.WriteAllText(path, "{broken");
    Check(!store.Load().Available && store.Save(known).Length > 0 && File.ReadAllText(path) == "{broken",
        "corrupt registry cannot be silently replaced");
    // R14 R1: exercise real files through both Load and Save; no exception is expected.
    void RejectedWithoutOverwrite(byte[] bytes, string label, string? expectedError = null)
    {
        File.WriteAllBytes(path, bytes);
        var loaded = store.Load();
        Check(!loaded.Available && loaded.Devices.Count == 0 &&
            loaded.Error.StartsWith("Riconoscimenti non disponibili:", StringComparison.Ordinal) &&
            (expectedError is null || loaded.Error.Contains(expectedError, StringComparison.Ordinal)),
            "R14 R1 " + label + " returns an unavailable catalog with its reason");
        Check(store.Save(known) == loaded.Error && File.ReadAllBytes(path).SequenceEqual(bytes) &&
            !Directory.EnumerateFiles(temporary, "*.tmp-*").Any(),
            "R14 R1 " + label + " blocks overwrite and preserves exact bytes without temporary files");
    }
    foreach (string cultureName in new[] { "it-IT", "en-US" })
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            RejectedWithoutOverwrite(new byte[DeviceRecognitionStore.MaximumBytes + 1],
                "oversized registry [" + cultureName + "]", "File troppo grande.");
            RejectedWithoutOverwrite(Encoding.UTF8.GetBytes("{\"Version\":2,\"Devices\":[]}"),
                "unknown schema [" + cultureName + "]", "Formato non valido.");
            RejectedWithoutOverwrite(Encoding.UTF8.GetBytes("{\"Version\":1,\"Devices\":null}"),
                "null device list [" + cultureName + "]", "Formato non valido.");
            RejectedWithoutOverwrite(Encoding.UTF8.GetBytes("{\"Version\":1,\"Devices\":[null]}"),
                "null device entry [" + cultureName + "]", "Formato non valido.");
            RejectedWithoutOverwrite(JsonSerializer.SerializeToUtf8Bytes(new { Version = 1,
                Devices = new[] { known.Devices[0], known.Devices[0] } }),
                "duplicate identities [" + cultureName + "]", "Formato non valido.");
            RejectedWithoutOverwrite(JsonSerializer.SerializeToUtf8Bytes(new { Version = 1,
                Devices = Enumerable.Repeat(known.Devices[0], DeviceRecognitionStore.MaximumDevices + 1).ToArray() }),
                "too many devices [" + cultureName + "]", "Formato non valido.");
            RejectedWithoutOverwrite(Array.Empty<byte>(), "empty JSON [" + cultureName + "]");
            // Exactly the byte limit remains valid; trailing JSON whitespace is legal.
            byte[] atLimit = Enumerable.Repeat((byte)' ', DeviceRecognitionStore.MaximumBytes).ToArray();
            Encoding.UTF8.GetBytes("{\"Version\":1,\"Devices\":[]}").CopyTo(atLimit, 0);
            File.WriteAllBytes(path, atLimit);
            Check(store.Load() is { Available: true, Devices.Count: 0 } && File.ReadAllBytes(path).SequenceEqual(atLimit),
                "R14 R1 exact byte limit is accepted without rewriting [" + cultureName + "]");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}
finally { Directory.Delete(temporary, true); }

// Independent Windows ABI fixture: offsets below are SDK layout values, not derived from reader declarations.
Check(Marshal.SizeOf<WindowsNetworkDevices.NativeAddress>() == 28 && Marshal.SizeOf<WindowsNetworkDevices.NativeRow>() == 88 &&
    Marshal.OffsetOf<WindowsNetworkDevices.NativeHeader>("First").ToInt32() == 8,
    "native row stride and aligned table header match Windows SDK layout");
IntPtr memory = Marshal.AllocHGlobal(8 + 4096 * 88);
try
{
    Marshal.Copy(new byte[8 + 4096 * 88], 0, memory, 8 + 4096 * 88);
    Marshal.WriteInt32(memory, 2);
    IntPtr first = IntPtr.Add(memory, 8), second = IntPtr.Add(first, 88);
    Marshal.WriteInt16(first, 0, 2); Marshal.Copy(new byte[] { 192, 168, 1, 2 }, 0, IntPtr.Add(first, 4), 4);
    Marshal.WriteInt32(first, 28, 5); Marshal.Copy(new byte[] { 2, 17, 34, 51, 68, 85 }, 0, IntPtr.Add(first, 40), 6);
    Marshal.WriteInt32(first, 72, 6); Marshal.WriteInt32(first, 76, 5); Marshal.WriteByte(first, 80, 2);
    Marshal.WriteInt16(second, 0, 23); Marshal.Copy(new byte[] { 0xfe, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 }, 0, IntPtr.Add(second, 8), 16);
    Marshal.WriteInt32(second, 24, 5); Marshal.WriteInt32(second, 28, 5); Marshal.WriteInt32(second, 76, 4);
    var decoded = WindowsNetworkDevices.ReadTable(memory, out bool truncated);
    Check(!truncated && decoded.Count == 2 && decoded[0].Address.ToString() == "192.168.1.2" && decoded[0].Mac == mac &&
        decoded[0].Index == 5 && decoded[0].State == NeighborState.Unreachable,
        "independent IPv4 fixture decodes offsets and unreachable flag without live acquisition");
    Check(decoded[1].Address.ToString() == "fe80::2%5" && decoded[1].Mac == "" && decoded[1].State == NeighborState.Stale,
        "independent IPv6 fixture retains scope and missing MAC");
    Marshal.WriteInt32(memory, 4097);
    WindowsNetworkDevices.ReadTable(memory, out bool limited);
    Check(limited, "native table row cap reports truncation");
}
finally { Marshal.FreeHGlobal(memory); }
Console.WriteLine("Network device regressions complete; no live network acquisition or probes.");
