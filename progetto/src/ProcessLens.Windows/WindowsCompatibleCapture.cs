using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ProcessLens.Core;
namespace ProcessLens.Windows;

public sealed record LocalIpInterface(string Id, string Name, string Address);
public interface ILocalIpCaptureSocket : IDisposable
{
    void Bind(IPAddress address);
    void SetReceiveMode(int mode);
    bool Poll(int microseconds);
    int Receive(byte[] buffer);
}
// Native Winsock only. RCVALL_IPLEVEL (3) deliberately avoids NIC promiscuous mode.
internal sealed class LocalIpCaptureSocket : ILocalIpCaptureSocket
{
    private readonly Socket _socket;
    public LocalIpCaptureSocket()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Cattura IP locale disponibile solo su Windows.");
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
        try { _socket.Blocking = false; _socket.ReceiveBufferSize = 262144; }
        catch { _socket.Dispose(); throw; }
    }
    public void Bind(IPAddress address) => _socket.Bind(new IPEndPoint(address, 0));
    public void SetReceiveMode(int mode)
    { byte[] input = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(input, mode); _socket.IOControl(IOControlCode.ReceiveAll, input, null); }
    public bool Poll(int microseconds) => _socket.Poll(microseconds, SelectMode.SelectRead);
    public int Receive(byte[] buffer) => _socket.Receive(buffer, SocketFlags.None);
    public void Dispose() => _socket.Dispose();
}
public sealed class WindowsCompatibleCapture : INativeCaptureClient
{
    private readonly INativeCaptureClient _pktmon;
    private readonly Func<IReadOnlyList<LocalIpInterface>> _interfaces;
    private readonly Func<ILocalIpCaptureSocket> _open;
    private readonly Func<TimeSpan> _elapsed;
    private readonly Stopwatch _watch = new();
    private readonly Dictionary<string, LocalIpInterface> _local = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NativeCaptureSource> _packet = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    public WindowsCompatibleCapture() : this(new WindowsNativeCapture(), Enumerate, () => new LocalIpCaptureSocket()) { }
    public WindowsCompatibleCapture(INativeCaptureClient pktmon, Func<IReadOnlyList<LocalIpInterface>> interfaces,
        Func<ILocalIpCaptureSocket> open, Func<TimeSpan>? elapsed = null)
    { _pktmon = pktmon; _interfaces = interfaces; _open = open; _elapsed = elapsed ?? (() => _watch.Elapsed); }
    internal static bool ValidAddress(string address) => IPAddress.TryParse(address, out var ip) &&
        ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) &&
        !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.Broadcast) && ip.GetAddressBytes()[0] is > 0 and < 224;
    private static IReadOnlyList<LocalIpInterface> Enumerate()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Lettura delle schede disponibile solo su Windows.");
        var result = new List<LocalIpInterface>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                if (ValidAddress(unicast.Address.ToString()))
                { result.Add(new(adapter.Id, NativeCaptureEvidence.Clean(adapter.Name), unicast.Address.ToString())); if (result.Count > 256) throw new InvalidDataException("Troppe associazioni IPv4 locali."); }
        }
        return result;
    }
    public NativeCaptureDiscovery Discover(CancellationToken cancellation)
    {
        lock (_gate)
        {
            Release(); cancellation.ThrowIfCancellationRequested();
            var choices = new List<NativeCaptureSource>(); var notes = new List<string>(); string token = Guid.NewGuid().ToString("N");
            try
            {
                var all = _interfaces();
                if (all.Count > 256) throw new InvalidDataException("Troppe associazioni IPv4 locali.");
                foreach (var binding in all.Where(b => ValidAddress(b.Address) && !string.IsNullOrWhiteSpace(b.Id)).Distinct())
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (all.Count(b => b.Address == binding.Address) != 1) continue; // Binding by IP would be ambiguous.
                    if (choices.Count >= 128) { notes.Add("Elenco IPv4 limitato a 128 sorgenti."); break; }
                    string key = "ip:" + token + ":" + choices.Count; _local.Add(key, binding);
                    choices.Add(new(key, Label(binding)));
                }
                notes.Add($"IP locale Windows: {choices.Count} sorgenti IPv4. Socket aperto solo dopo Cattura; richiede amministratore. Non dipende dalle esportazioni PktMon.");
            }
            catch (OperationCanceledException) { Release(); throw; }
            catch (Exception ex) { notes.Add("Elenco IPv4 non disponibile: " + ex.Message); }
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var packet = _pktmon.Discover(cancellation); notes.Add(packet.Note);
                foreach (var item in packet.Available ? packet.Sources.Take(256 - choices.Count) : Array.Empty<NativeCaptureSource>())
                { string key = "pkt:" + token + ":" + choices.Count; _packet.Add(key, item); choices.Add(new(key, "PktMon · " + item.Name)); }
            }
            catch (OperationCanceledException) { Release(); throw; }
            catch (Exception ex) { notes.Add("PktMon non disponibile: " + ex.Message); }
            return new(choices.Count > 0, choices, string.Join("\n", notes));
        }
    }
    private static string Label(LocalIpInterface b) => "Windows · IP locale · " + NativeCaptureEvidence.Clean(b.Name) + " · " + b.Address;
    private void Verify(LocalIpInterface b)
    {
        var current = _interfaces();
        if (current.Count > 256 || current.Count(i => i.Address == b.Address) != 1 || !current.Any(i => i.Id == b.Id && i.Address == b.Address))
            throw new InvalidDataException("Scheda o indirizzo IPv4 cambiato. Elenca di nuovo le sorgenti.");
    }
    public NativeCaptureResult Capture(NativeCaptureSource source, CancellationToken cancellation)
    {
        lock (_gate)
        {
            if (_packet.TryGetValue(source.Key, out var packet) && source.Name == "PktMon · " + packet.Name)
                return _pktmon.Capture(packet, cancellation);
            if (!_local.TryGetValue(source.Key, out var binding) || source.Name != Label(binding))
                return new(false, false, [], 0, 0, "Sorgente scaduta o non verificata. Elenca di nuovo le sorgenti.") { LossCountsAvailable = false };
            cancellation.ThrowIfCancellationRequested();
            var rows = new List<NativePacketRow>(); ILocalIpCaptureSocket? socket = null;
            bool available = false, partial = false, enabled = false; DateTimeOffset? started = null;
            string note = "Winsock SIO_RCVALL / IPLEVEL · solo IPv4 sulla scheda selezionata. Massimo 20 s e 2.048 osservazioni. " +
                "Ora: ricezione nel programma, non timestamp del driver. Perdite del socket non misurate. Nessun ARP, frame Wi-Fi o attribuzione al PID.";
            try
            {
                Verify(binding); var local = IPAddress.Parse(binding.Address); socket = _open(); socket.Bind(local);
                Verify(binding); cancellation.ThrowIfCancellationRequested();
                socket.SetReceiveMode(3); enabled = true; available = true; started = DateTimeOffset.UtcNow; _watch.Restart();
                byte[] buffer = new byte[65535];
                try
                {
                    while (_elapsed() < TimeSpan.FromSeconds(NativeCaptureEvidence.DurationSeconds) && rows.Count < NativeCaptureEvidence.MaximumRows)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (!socket.Poll(100000)) continue;
                        cancellation.ThrowIfCancellationRequested();
                        int length;
                        try { length = socket.Receive(buffer); }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock) { continue; }
                        if (length < 20 || length > buffer.Length || buffer[0] >> 4 != 4)
                            throw new InvalidDataException("Datagramma IPv4 non valido o incompleto.");
                        var data = buffer.AsSpan(0, length); int header = (data[0] & 15) * 4;
                        int total = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
                        if (header < 20 || total < header || total > length) throw new InvalidDataException("Lunghezza IPv4 non coerente.");
                        ushort direction = data.Slice(12, 4).SequenceEqual(local.GetAddressBytes()) ? (ushort)2 :
                            data.Slice(16, 4).SequenceEqual(local.GetAddressBytes()) ? (ushort)1 : (ushort)0;
                        rows.Add(NativeCaptureEvidence.Decode(data[..Math.Min(total, NativeCaptureEvidence.SnapshotBytes)], 3, direction, 0, 0,
                            DateTime.UtcNow.ToFileTimeUtc()) with { HasComponent = false, HasDropReason = false });
                    }
                }
                finally { Array.Clear(buffer); }
                if (rows.Count == NativeCaptureEvidence.MaximumRows) { partial = true; note += " Limite osservazioni raggiunto."; }
            }
            catch (OperationCanceledException) { partial = true; note += " Lettura interrotta su richiesta."; }
            catch (Exception ex)
            {
                partial = true; note += " " + (ex is SocketException socketError ? "Winsock " + socketError.ErrorCode + " · " : "") + ex.Message;
                if (ex is SocketException { SocketErrorCode: SocketError.AccessDenied }) note += " Avvia ProcessLens come amministratore; verifica eventuali restrizioni del sistema.";
            }
            finally
            {
                try { if (enabled && socket is not null) socket.SetReceiveMode(0); }
                catch (Exception ex) { partial = true; note += " Ripristino socket: " + ex.Message; }
                finally { socket?.Dispose(); }
            }
            return new(available, partial, rows, 0, 0, note) { SourceName = source.Name, StartedUtc = started,
                EndedUtc = available ? DateTimeOffset.UtcNow : null, LossCountsAvailable = false };
        }
    }
    public NativeWifiResult ReadWifi(CancellationToken cancellation) => _pktmon.ReadWifi(cancellation);
    public void Release() { lock (_gate) { _local.Clear(); _packet.Clear(); _pktmon.Release(); } }
}
