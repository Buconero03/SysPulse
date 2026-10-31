using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml;
using ProcessLens.Core;

namespace ProcessLens.Windows;

internal interface INetworkEvidenceApi
{
    NetworkAuditSetting ReadAudit(Guid category, string name);
    IntPtr Query(string xpath);
    bool Next(IntPtr query, out IntPtr record, out int error);
    string Render(IntPtr record);
    void Close(IntPtr handle);
}

// Local, read-only manual history. No WFP filter, audit-policy change, driver or subscription.
public sealed class WindowsNativeNetworkEvidence : INativeNetworkEvidenceQuerySource
{
    internal static readonly Guid ConnectionAudit = new("0cce9226-69ae-11d9-bed3-505054503030");
    internal static readonly Guid LogonAudit = new("0cce9215-69ae-11d9-bed3-505054503030");
    internal static readonly Guid PacketDropAudit = new("0cce9225-69ae-11d9-bed3-505054503030");
    internal static string QueryText(DateTime since, DateTime until) => QueryText(since, until, new());
    internal static string QueryText(DateTime since, DateTime until, NetworkEventReadOptions options)
    {
        options.Validate();
        string ids = "EventID=5157 or EventID=5155 or EventID=5159" + (options.IncludePacketDrops ? " or EventID=5152" : "");
        return "*[System[Provider[@Name='Microsoft-Windows-Security-Auditing'] and " +
            $"TimeCreated[@SystemTime >= '{since:O}' and @SystemTime <= '{until:O}']] and " +
            "(System[(" + ids + ")] or (System[EventID=4625] and EventData[Data[@Name='LogonType']='3' or Data[@Name='LogonType']='10']))]";
    }
    private readonly INetworkEvidenceApi _api;
    public WindowsNativeNetworkEvidence() : this(new NetworkEvidenceApi()) { }
    internal WindowsNativeNetworkEvidence(INetworkEvidenceApi api) { _api = api; }
    public NativeNetworkSnapshot Capture(CancellationToken cancellationToken) => Capture(new(), cancellationToken);
    public NativeNetworkSnapshot Capture(NetworkEventReadOptions options, CancellationToken cancellationToken)
    {
        options.Validate();
        DateTime until = DateTime.UtcNow, since = until - TimeSpan.FromMinutes(options.Minutes);
        var audit = new List<NetworkAuditSetting>();
        var rows = new List<NativeNetworkEvent>();
        var seen = new HashSet<long>();
        bool available = false, partial = false;
        string note = "Fonte: Windows Event Log API · Security locale · 5157/5155/5159 e 4625 tipi 3/10" + (options.IncludePacketDrops ? ", più 5152. " : ". ") +
            "Massimo 256 eventi letti; nessuna modifica ai criteri di audit o al firewall.";
        IntPtr query = IntPtr.Zero;
        int chars = 0, scanned = 0, malformed = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            audit.Add(_api.ReadAudit(ConnectionAudit, "Connessioni e porte locali / WFP"));
            cancellationToken.ThrowIfCancellationRequested();
            audit.Add(_api.ReadAudit(LogonAudit, "Accessi / Logon"));
            if (options.IncludePacketDrops) audit.Add(_api.ReadAudit(PacketDropAudit, "Pacchetti scartati / WFP"));
            cancellationToken.ThrowIfCancellationRequested();
            query = _api.Query(QueryText(since, until, options));
            if (query == IntPtr.Zero) throw new InvalidDataException("Handle della query eventi non valido.");
            var watch = Stopwatch.StartNew();
            bool exhausted = false;
            while (scanned < NativeNetworkEvidence.MaximumEvents && watch.Elapsed < TimeSpan.FromSeconds(3))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_api.Next(query, out IntPtr record, out int error))
                {
                    if (record != IntPtr.Zero) _api.Close(record);
                    if (error == 259) { available = true; exhausted = true; break; } // ERROR_NO_MORE_ITEMS only.
                    throw new Win32Exception(error, "EvtNext non riuscita · Win32 " + error);
                }
                available = true; scanned++;
                try
                {
                    if (record == IntPtr.Zero) throw new InvalidDataException("Handle evento nullo.");
                    string xml = _api.Render(record);
                    if (xml.Length > NativeNetworkEvidence.MaximumTotalXmlChars - chars)
                    { partial = true; note += " Limite testo raggiunto."; break; }
                    chars += xml.Length;
                    try
                    {
                        var item = NativeNetworkEvidence.Decode(xml, since, until);
                        if (item is not null && (options.IncludePacketDrops || item.EventId != 5152) && (!item.RecordId.HasValue || seen.Add(item.RecordId.Value))) rows.Add(item);
                    }
                    catch (Exception ex) when (ex is XmlException or InvalidDataException)
                    { malformed++; partial = true; }
                }
                finally { if (record != IntPtr.Zero) _api.Close(record); }
            }
            if (!exhausted) { partial = true; note += " Lettura limitata per numero, tempo o testo."; }
            if (malformed > 0) note += $" {malformed} eventi non interpretabili.";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException or DllNotFoundException or EntryPointNotFoundException)
        {
            partial = true;
            note += " " + (ex is Win32Exception native ? "Win32 " + native.NativeErrorCode + " · " : "") + ex.Message + (ex is Win32Exception { NativeErrorCode: 5 } ? " Lettura non autorizzata: può richiedere privilegi amministrativi o diritti sul registro Security." : "");
        }
        finally { if (query != IntPtr.Zero) _api.Close(query); }
        return new(until, since, available, partial, audit, rows.OrderByDescending(e => e.Utc).ThenByDescending(e => e.RecordId).ToArray(), note)
            { Options = options, ScannedEvents = scanned, MalformedEvents = malformed, XmlCharacters = chars };
    }
}

internal sealed class NetworkEvidenceApi : INetworkEvidenceApi
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AuditPolicyInfo { public Guid Subcategory; public uint Flags; public Guid Category; }
    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool AuditQuerySystemPolicy(ref Guid subcategory, uint count, out IntPtr policy);
    [DllImport("advapi32.dll", ExactSpelling = true)] private static extern void AuditFree(IntPtr buffer);
    [DllImport("wevtapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr EvtQuery(IntPtr session, string path, string query, uint flags);
    [DllImport("wevtapi.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EvtNext(IntPtr query, uint size, [Out] IntPtr[] events, uint timeout, uint flags, out uint returned);
    [DllImport("wevtapi.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EvtRender(IntPtr context, IntPtr record, uint flags, uint bytes,
        IntPtr buffer, out uint used, out uint properties);
    [DllImport("wevtapi.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EvtClose(IntPtr handle);
    public NetworkAuditSetting ReadAudit(Guid category, string name)
    {
        IntPtr policy = IntPtr.Zero;
        try
        {
            if (!AuditQuerySystemPolicy(ref category, 1, out policy))
                return new(name, null, "AuditQuerySystemPolicy · Win32 " + Marshal.GetLastWin32Error());
            if (policy == IntPtr.Zero) return new(name, null, "Risultato audit vuoto");
            var info = Marshal.PtrToStructure<AuditPolicyInfo>(policy);
            if (info.Subcategory != category) return new(name, null, "Sottocategoria audit inattesa");
            return new(name, FailureEnabled(info.Flags), "Criterio di sistema attuale; eccezioni per utente non lette");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        { return new(name, null, ex.Message); }
        finally { if (policy != IntPtr.Zero) AuditFree(policy); }
    }
    internal static bool? FailureEnabled(uint flags) => (flags & ~7u) != 0 || (flags & 4u) != 0 && (flags & 3u) != 0
        ? null : (flags & 2u) != 0;
    public IntPtr Query(string xpath)
    {
        IntPtr query = EvtQuery(IntPtr.Zero, "Security", xpath, 0x201); // Channel path + reverse direction.
        if (query == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        return query;
    }
    public bool Next(IntPtr query, out IntPtr record, out int error)
    {
        var events = new IntPtr[1];
        bool ok = EvtNext(query, 1, events, 250, 0, out uint count);
        error = ok ? 0 : Marshal.GetLastWin32Error(); record = events[0];
        if (ok && (count != 1 || record == IntPtr.Zero))
        { if (record != IntPtr.Zero) EvtClose(record); record = IntPtr.Zero; throw new InvalidDataException("Conteggio EvtNext inatteso."); }
        return ok;
    }
    public string Render(IntPtr record)
    {
        bool first = EvtRender(IntPtr.Zero, record, 1, 0, IntPtr.Zero, out uint needed, out _);
        int error = first ? 0 : Marshal.GetLastWin32Error();
        if (first || error != 122 || needed < 2 || needed > NativeNetworkEvidence.MaximumXmlChars * 2u || (needed & 1) != 0)
            throw new InvalidDataException("Dimensione XML evento non valida o EvtRender non disponibile · Win32 " + error);
        IntPtr buffer = Marshal.AllocHGlobal(checked((int)needed));
        try
        {
            if (!EvtRender(IntPtr.Zero, record, 1, needed, buffer, out uint used, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (used < 2 || used > needed || (used & 1) != 0 || Marshal.ReadInt16(buffer, (int)used - 2) != 0)
                throw new InvalidDataException("XML evento troncato.");
            return Marshal.PtrToStringUni(buffer, checked((int)used / 2 - 1)) ?? "";
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public void Close(IntPtr handle) { if (handle != IntPtr.Zero) EvtClose(handle); }
}
