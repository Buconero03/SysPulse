using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;
using ProcessLens.Core;
using ProcessLens.Windows;

internal static class Program
{
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception("FAIL native network: " + label); Console.WriteLine("PASS native network: " + label); }
    static void Reject(Action action, string label)
    { try { action(); } catch (Exception e) when (e is InvalidDataException or XmlException) { Check(true, label); return; } throw new Exception("FAIL native network: " + label); }
    static readonly DateTime Until = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";
    static string Xml(int id = 5157, long record = 42, DateTime? stamp = null, params (string Key, string Value)[] fields) =>
        new XElement(Ns + "Event", new XElement(Ns + "System",
            new XElement(Ns + "Provider", new XAttribute("Name", "Microsoft-Windows-Security-Auditing")),
            new XElement(Ns + "EventID", id), new XElement(Ns + "EventRecordID", record),
            new XElement(Ns + "TimeCreated", new XAttribute("SystemTime", (stamp ?? Until.AddMinutes(-1)).ToString("O")))),
            new XElement(Ns + "EventData", fields.Select(f => new XElement(Ns + "Data", new XAttribute("Name", f.Key), f.Value))))
            .ToString(SaveOptions.DisableFormatting);
    static NativeNetworkEvent? Decode(string s) => NativeNetworkEvidence.Decode(s, Until.AddMinutes(-15), Until);
    static string Block(long record = 42, DateTime? time = null) => Xml(5157, record, time,
        ("Direction", "%%14592"), ("SourceAddress", "192.0.2.8"), ("SourcePort", "42000"),
        ("DestAddress", "2001:db8::1"), ("DestPort", "3389"), ("Protocol", "6"),
        ("ProcessID", "120"), ("Application", @"\device\harddiskvolume1\private\app.exe"));
    public static void Main()
    {
        foreach (string culture in new[] { "it-IT", "en-US" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var block = Decode(Block())!;
            Check(block.EventId == 5157 && block.Direction == "Ingresso" && block.Protocol == "TCP" &&
                block.SourceAddress == "192.0.2.8" && block.DestinationAddress == "2001:db8::1" && block.DestinationPort == "3389",
                "5157 fields decoded by name in both address families " + culture);
            Check(block.Pid == 120 && block.Application == "app.exe" && block.RecordId == 42 && block.Utc == Until.AddMinutes(-1),
                "reported historical identity and UTC retained without image directory " + culture);
            var logon = Decode(Xml(4625, 43, null, ("LogonType", "10"), ("IpAddress", "198.51.100.9"),
                ("IpPort", "4444"), ("ProcessId", "0x1f4"), ("ProcessName", @"C:\secret\winlogon.exe"),
                ("Status", "0xC000006D"), ("TargetUserName", "PRIVATE_ACCOUNT_SECRET")))!;
            Check(logon.Pid == 500 && logon.Event.Contains("interattivo remoto") && logon.DestinationAddress == "Questo PC" &&
                logon.Protocol == "N/D" && logon.DestinationPort == "N/D" && logon.Status == "0xC000006D",
                "4625 remote logon is failure rather than successful access or invented RDP port " + culture);
            Check(!logon.Details.Contains("PRIVATE_ACCOUNT_SECRET") && !logon.Details.Contains(@"C:\secret") &&
                logon.Details.Contains("riutilizzato") && logon.Details.Contains("non dimostra"),
                "private account and image directory omitted; old PID and attribution limits explicit " + culture);
            Check(Decode(Xml(4625, 43, null, ("LogonType", "2"))) is null && Decode(Xml(4625, 43, null, ("LogonType", "5"))) is null,
                "interactive local and service logon failures do not become network events " + culture);
            var missing = Decode(Xml(4625, 44, null, ("LogonType", "3"), ("IpAddress", "-"), ("IpPort", "-")))!;
            Check(missing.SourceAddress == "N/D" && missing.SourcePort == "N/D" && missing.Pid is null,
                "network logon with missing source and PID remains unavailable " + culture);
            var unavailable = new NativeNetworkSnapshot(Until, Until.AddMinutes(-15), false, true, [], [], "Win32 5");
            string report = NativeNetworkEvidence.Report(unavailable);
            Check(report.Contains("conteggi N/D") && report.Contains("Win32 5") && report.Contains("ascolto passivo") && !report.Contains("blocchi WFP: 0"),
                "failed acquisition has unavailable counters and original reason " + culture);
        }
        var outgoing = Decode(Block().Replace("%%14592", "%%14593"))!;
        Check(outgoing.Direction == "Uscita", "outbound blocks retain their direction instead of inventing an incoming attacker");
        var unknown = Decode(Xml(5157, 1, null, ("Direction", "unexpected"), ("Protocol", ""), ("SourceAddress", "garbage"),
            ("DestPort", "65536"), ("ProcessID", "4294967295")))!;
        Check(unknown.Direction == "N/D" && unknown.Protocol == "N/D" && unknown.SourceAddress == "N/D" &&
            unknown.DestinationPort == "N/D" && unknown.Pid is null, "invalid address port protocol and oversized PID stay unavailable");
        Check(Decode(Block(time: Until.AddMinutes(-16))) is null && Decode(Block(time: Until.AddSeconds(1))) is null &&
            Decode(Block(time: Until.AddMinutes(-15))) is not null && Decode(Block(time: Until)) is not null,
            "exact 15-minute bounds retain endpoints and exclude stale or future records");
        Reject(() => Decode(Block().Replace("Microsoft-Windows-Security-Auditing", "FakeProvider")), "wrong provider rejected");
        Reject(() => Decode("<Event/>"), "wrong namespace rejected");
        Reject(() => Decode(Block().Replace("<EventID>5157</EventID>", "<EventID>bad</EventID>")), "invalid event ID rejected");
        Reject(() => Decode(Block().Replace("SystemTime=\"2026", "SystemTime=\"bad2026")), "invalid event timestamp rejected");
        Reject(() => Decode(Xml(5157, 1, null, ("SourceAddress", "192.0.2.1"), ("SourceAddress", "192.0.2.2"))), "duplicate named fields rejected");
        Reject(() => Decode("<!DOCTYPE Event [<!ENTITY x SYSTEM 'file:///private'>]><Event>&x;</Event>"), "DTD and external entities rejected without IO");
        Reject(() => Decode(new string('x', NativeNetworkEvidence.MaximumXmlChars + 1)), "oversized XML rejected before parsing");
        var safe = Decode(Xml(5157, 1, null, ("Application", "evil\nFAKE_LOG.exe"), ("Protocol", "1\nFAKE_EVENT")))!;
        Check(!safe.Application.Contains('\n') && !safe.Protocol.Contains('\n'), "event data cannot inject visual log lines");
        Check(Marshal.SizeOf<NetworkEvidenceApi.AuditPolicyInfo>() == 36 &&
            Marshal.OffsetOf<NetworkEvidenceApi.AuditPolicyInfo>("Flags").ToInt32() == 16 &&
            Marshal.OffsetOf<NetworkEvidenceApi.AuditPolicyInfo>("Category").ToInt32() == 20,
            "audit structure matches independent Windows SDK offsets 0 16 20 and stride 36");
        Check(NetworkEvidenceApi.FailureEnabled(0) == false && NetworkEvidenceApi.FailureEnabled(1) == false &&
            NetworkEvidenceApi.FailureEnabled(2) == true && NetworkEvidenceApi.FailureEnabled(3) == true &&
            NetworkEvidenceApi.FailureEnabled(4) == false && NetworkEvidenceApi.FailureEnabled(6) is null && NetworkEvidenceApi.FailureEnabled(8) is null,
            "audit failure flag handles disabled success-only combined and invalid masks");
        Check(WindowsNativeNetworkEvidence.ConnectionAudit == new Guid("0cce9226-69ae-11d9-bed3-505054503030") &&
            WindowsNativeNetworkEvidence.LogonAudit == new Guid("0cce9215-69ae-11d9-bed3-505054503030"), "audit categories are published WFP connection and Logon GUIDs");
        var empty = new Fake([]); var snapshot = new WindowsNativeNetworkEvidence(empty).Capture(default);
        Check(snapshot.Available && !snapshot.Partial && snapshot.Events.Count == 0 && empty.Closed.SequenceEqual(new[] { 1 }),
            "normal empty query closes its handle and reports no sampled events rather than a safe verdict");
        Check(empty.Threads.Distinct().Count() == 1 && empty.QueryText.Contains("@SystemTime >= '") &&
            empty.QueryText.Contains("@SystemTime <= '") && !empty.QueryText.Contains("5152"),
            "native query stays on one owner thread with absolute bounds and no per-packet logging");
        string now = Block(time: DateTime.UtcNow.AddMinutes(-1));
        var valid = new Fake([now, now]); var good = new WindowsNativeNetworkEvidence(valid).Capture(default);
        Check(good.Events.Count == 1 && good.Available && !good.Partial && valid.Closed.Order().SequenceEqual(new[] { 1, 2, 3 }),
            "record IDs deduplicate repeated records and every owned event handle closes");
        var denied = new Fake([]) { QueryError = 5 }; var noAccess = new WindowsNativeNetworkEvidence(denied).Capture(default);
        Check(!noAccess.Available && noAccess.Note.Contains("Win32Exception") == false && noAccess.Note.Contains("Win32 5") && noAccess.Note.Contains("non autorizzata") &&
            noAccess.Audit.Count == 2 && denied.Closed.Count == 0, "denied channel preserves unavailable state and audit observations");
        var nextError = new Fake([]) { NextError = 5 }; var failedNext = new WindowsNativeNetworkEvidence(nextError).Capture(default);
        Check(!failedNext.Available && failedNext.Partial && failedNext.Note.Contains("Win32 5") && nextError.Closed.SequenceEqual(new[] { 1 }),
            "first read failure is unavailable rather than a successful empty log");
        var lateError = new Fake([now]) { NextError = 15011 }; var partial = new WindowsNativeNetworkEvidence(lateError).Capture(default);
        Check(partial.Available && partial.Partial && partial.Events.Count == 1 && partial.Note.Contains("15011") && lateError.Closed.Count == 2,
            "mid-query error preserves prior events its reason and all handle cleanup");
        var malformed = new Fake(["<bad>", now]); var bad = new WindowsNativeNetworkEvidence(malformed).Capture(default);
        Check(bad.Available && bad.Partial && bad.Events.Count == 1 && bad.Note.Contains("non interpretabili"),
            "malformed event cannot become a complete empty acquisition");
        var budget = new Fake(Enumerable.Range(0, 300).Select(i => Block(i + 1, DateTime.UtcNow.AddMinutes(-1))).ToArray());
        var capped = new WindowsNativeNetworkEvidence(budget).Capture(default);
        Check(capped.Partial && capped.Events.Count == 256 && budget.NextCalls == 256 && budget.Closed.Count == 257,
            "fixed event budget bounds retained history and closes the last event and query");
        string large = now.Replace("</Event>", "<UserData>" + new string('x', 30000) + "</UserData></Event>");
        var textBudget = new Fake(Enumerable.Repeat(large, 60).ToArray());
        var limitedText = new WindowsNativeNetworkEvidence(textBudget).Capture(default);
        Check(limitedText.Partial && limitedText.Note.Contains("Limite testo") && textBudget.NextCalls < 40 &&
            textBudget.Closed.Count == textBudget.NextCalls + 1,
            "aggregate XML text budget stops before retaining or parsing unbounded event payloads");
        var render = new Fake([now]) { RenderError = true }; var renderFail = new WindowsNativeNetworkEvidence(render).Capture(default);
        Check(renderFail.Partial && render.Closed.Order().SequenceEqual(new[] { 1, 2 }), "render failure releases owned event and query handles");
        using var cancelled = new CancellationTokenSource();
        var cancel = new Fake([now]) { Cancel = cancelled };
        try { new WindowsNativeNetworkEvidence(cancel).Capture(cancelled.Token); throw new Exception("cancel ignored"); }
        catch (OperationCanceledException) { Check(cancel.Closed.Order().SequenceEqual(new[] { 1, 2 }), "cancellation propagates and releases owned handles"); }
        var disabled = new NetworkAuditSetting("WFP", false, ""); var unknownAudit = new NetworkAuditSetting("Logon", null, "Win32 5");
        Check(disabled.Text.Contains("disabilitata") && unknownAudit.Text.Contains("N/D") && unknownAudit.Text.Contains("Win32 5"),
            "disabled and unknown audit configuration remain distinct");
        R34NetworkReadChecks.Run();
        Console.WriteLine("Native network evidence tests complete; owned fake API only, no live query, capture, setting or firewall change.");
    }
    sealed class Fake(string[] rows) : INetworkEvidenceApi
    {
        public List<int> Closed = []; public List<int> Threads = []; public string QueryText = "";
        public int QueryError, NextError, NextCalls; public bool RenderError; public CancellationTokenSource? Cancel;
        int index;
        public NetworkAuditSetting ReadAudit(Guid category, string name) => new(name, false, "fixture disabled");
        public IntPtr Query(string query)
        { Threads.Add(Environment.CurrentManagedThreadId); QueryText = query; if (QueryError != 0) throw new Win32Exception(QueryError); return new(1); }
        public bool Next(IntPtr query, out IntPtr record, out int error)
        { Threads.Add(Environment.CurrentManagedThreadId); NextCalls++; if (index >= rows.Length) { record = IntPtr.Zero; error = NextError == 0 ? 259 : NextError; return false; }
          record = new(++index + 1); error = 0; Cancel?.Cancel(); return true; }
        public string Render(IntPtr record)
        { Threads.Add(Environment.CurrentManagedThreadId); if (RenderError) throw new InvalidDataException("render fixture error"); return rows[record.ToInt32() - 2]; }
        public void Close(IntPtr handle) => Closed.Add(handle.ToInt32());
    }
}
