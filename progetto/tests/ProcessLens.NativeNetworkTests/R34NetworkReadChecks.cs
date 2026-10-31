using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ProcessLens.Core;
using ProcessLens.Windows;

internal static class R34NetworkReadChecks
{
    static readonly DateTime End = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";
    static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL R34 network: " + label); Console.WriteLine("PASS R34 network: " + label); }
    static string Xml(int id, params (string Key, string Value)[] fields) => new XElement(Ns+"Event",
        new XElement(Ns+"System", new XElement(Ns+"Provider", new XAttribute("Name", "Microsoft-Windows-Security-Auditing")),
            new XElement(Ns+"EventID", id), new XElement(Ns+"EventRecordID", 70),
            new XElement(Ns+"TimeCreated", new XAttribute("SystemTime", End.AddMinutes(-1).ToString("O")))),
        new XElement(Ns+"EventData", fields.Select(f => new XElement(Ns+"Data", new XAttribute("Name", f.Key), f.Value))))
        .ToString(SaveOptions.DisableFormatting);
    static NativeNetworkEvent Decode(string xml) => NativeNetworkEvidence.Decode(xml, End.AddMinutes(-15), End)!;
    public static void Run()
    {
        foreach (string culture in new[] { "it-IT", "en-US" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (int id in new[] { 5155, 5159 })
            {
                var e = Decode(Xml(id, ("ProcessId", "0x64"), ("Application", @"C:\private\listener.exe"),
                    ("SourceAddress", "0.0.0.0"), ("SourcePort", "5555"), ("Protocol", "6"), ("FilterRTID", "812"), ("LayerRTID", "40")));
                Check(e.Pid == 100 && e.Application == "listener.exe" && e.SourceAddress == "0.0.0.0" && e.SourcePort == "5555" &&
                    e.Direction == "Operazione locale" && e.DestinationAddress == "N/D" && e.DestinationPort == "N/D", $"{id} exact SDK fields, local endpoint and no invented remote peer [{culture}]");
                Check(e.FilterId == "812" && e.LayerId == "40" && e.Details.Contains("SIGNIFICATO") && e.Details.Contains("DATI REGISTRATI") &&
                    e.Details.Contains("COME VERIFICARE") && !e.Details.Contains(@"C:\private"), $"{id} separates interpretation, original values and follow-up [{culture}]");
            }
            var packet = Decode(Xml(5152, ("ProcessId", "42"), ("Direction", "%%14592"), ("SourceAddress", "198.51.100.5"),
                ("SourcePort", "53"), ("DestAddress", "192.0.2.1"), ("DestPort", "55000"), ("Protocol", "17")));
            Check(packet.Pid == 42 && packet.Protocol == "UDP" && packet.DestinationPort == "55000" && packet.Meaning.Contains("stessa connessione"), "packet drops do not count unique devices or connections " + culture);
            var sample = new NativeNetworkSnapshot(End, End.AddHours(-1), true, false, [], [packet], "fixture")
                { Options = new(60, true), ScannedEvents = 2, XmlCharacters = 1500 };
            Check(NetworkEvidenceStatus.Summary(sample).Contains("60 min") && NetworkReadPresentation.EventCounts(sample).Contains("pacchetti scartati: 1") &&
                NativeNetworkEvidence.Report(sample).Contains("Record esaminati: 2"), "actual interval and event-family counts retained " + culture);
            Check(NetworkReadPresentation.EventCounts(sample with { Available = false }).Contains("N/D") &&
                NetworkReadPresentation.EventCounts(sample with { Options = new() }).Contains("non richiesti"), "missing versus unrequested counters distinct " + culture);
            var log = new NetworkActivityJournal(() => new DateTimeOffset(End)); int changed = 0; log.Changed += () => changed++;
            log.Add("Test\nsource", "ERRORE", "44,0 MiB\n[FAKE]\u2028entry");
            Check(log.Report.Contains("2026-10-27T12:00:00.0000000+00:00") && log.Report.Contains("44,0 MiB [FAKE] entry") && !log.Report.Contains("\n[FAKE]"),
                "invariant UTC and input cannot forge log lines " + culture);
            for (int i=0; i<200; i++) log.Add("Test", "ESITO", new string('x',5000));
            Check(log.Count == 128 && log.Discarded == 73 && changed == 201 && log.Report.Length < 180000 && !log.Report.Contains(new string('x',1201)),
                "bounded log history and messages with accurate discarded count " + culture);
            Check(NetworkReadPresentation.DeviceEmpty(null).Contains("Nessuna lettura") && NetworkReadPresentation.DeviceEmpty(null, true).Contains("rileggere"), "initial and invalidated cache distinct " + culture);
            var fail = new NetworkDeviceSnapshot(End, false, true, "denied", 0, []);
            Check(NetworkReadPresentation.DeviceCounts(fail, NetworkDeviceRules.Present(fail,new([],""))).Contains("Voci con MAC: N/D") &&
                NetworkReadPresentation.DeviceEmpty(fail).Contains("N/D"), "failed device read cannot become zero devices " + culture);
            Check(NetworkReadPresentation.DeviceEmpty(fail with { Available=true }).Contains("Non significa che la rete sia vuota"), "successful empty cache does not assert empty network " + culture);
            Check(NetworkReadPresentation.PacketDetails(new("12:00:00","TCP","192.0.2.1:8","192.0.2.2:9","In",128,1,0)).Contains("non è un PID") &&
                NetworkReadPresentation.WifiDetails(new("fixture","SSID","02-11-22-33-44-55",-60,2412000,"Punto di accesso")).Contains("non è l'ora di rilevamento"),
                "packet bytes ownership and radio cache age explained " + culture);
            var unsafeId = Decode(Xml(5159,("FilterRTID","12\u2028FAKE"),("LayerRTID","40\nFORGED")));
            Check(!unsafeId.FilterId.Contains('\u2028') && !unsafeId.LayerId.Contains('\n'), "technical fields cannot forge detail paragraphs " + culture);
        }
        var narrowApi=new Fake(); var narrow=new WindowsNativeNetworkEvidence(narrowApi).Capture(default);
        Check(narrow.Audit.Count==2 && !narrowApi.QueryText.Contains("5152") && narrowApi.QueryText.Contains("5155") && narrowApi.QueryText.Contains("5159"), "default expands port failures without high-volume packet events");
        var wideApi=new Fake();var wide=new WindowsNativeNetworkEvidence(wideApi).Capture(new(60,true),default);
        Check(wide.Audit.Count==3 && wideApi.QueryText.Contains("EventID=5152") && wide.SinceUtc==wide.CapturedUtc.AddHours(-1), "explicit extended query uses packet-drop audit and exact 60-minute interval");
        Check(wideApi.Categories.Contains(WindowsNativeNetworkEvidence.PacketDropAudit) && narrow.ScannedEvents==0 && narrow.XmlCharacters==0,
            "only requested audit categories read and empty enumeration diagnostics retained");
        foreach(int invalid in new[]{-1,0,16,61,int.MaxValue}) {
            var api=new Fake();try { new WindowsNativeNetworkEvidence(api).Capture(new(invalid),default);throw new Exception("invalid options accepted"); }
            catch(ArgumentOutOfRangeException){Check(api.Queries==0 && api.Categories.Count==0,"invalid interval rejected before native API: "+invalid);}
        }
        // Standard XPath cannot reproduce Event Log's timestamp comparisons. Test the category/type part independently;
        // exact timestamp bounds remain covered by the original decoder tests and query text assertions.
        string query=System.Text.RegularExpressions.Regex.Replace(WindowsNativeNetworkEvidence.QueryText(End.AddMinutes(-15),End,new()),@" and TimeCreated\[[^\]]+\]", "");
        bool Match(string xml){var doc=new XmlDocument();doc.LoadXml(xml.Replace(" xmlns=\""+Ns+"\"",""));return doc.SelectSingleNode(query)!=null;}
        Check(Match(Xml(5155)) && Match(Xml(5159)) && Match(Xml(5157)) && !Match(Xml(5152)) &&
            Match(Xml(4625,("LogonType","3"))) && Match(Xml(4625,("LogonType","10"))) && !Match(Xml(4625,("LogonType","2"))),
            "independent XPath category/type selection; native EvtQuery still requires Windows");
    }
    sealed class Fake : INetworkEvidenceApi
    {
        public string QueryText=""; public int Queries; public List<Guid> Categories=[];
        public NetworkAuditSetting ReadAudit(Guid id,string name){Categories.Add(id);return new(name,true,"");}
        public IntPtr Query(string query){Queries++;QueryText=query;return new(1);}
        public bool Next(IntPtr query,out IntPtr record,out int error){record=IntPtr.Zero;error=259;return false;}
        public string Render(IntPtr record)=>throw new Exception("not expected");
        public void Close(IntPtr handle){}
    }
}
