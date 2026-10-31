using System.Globalization;
using ProcessLens.App;
using ProcessLens.Core;

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL presentation: " + label);
    Console.WriteLine("PASS presentation: " + label);
}

var originalCulture = CultureInfo.CurrentCulture;
var originalUiCulture = CultureInfo.CurrentUICulture;
try
{
    foreach (string cultureName in new[] { "it-IT", "en-US" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        string decimalMark = cultureName == "it-IT" ? "," : ".";
        const string space = "\u00a0";
        void Equal(string actual, string expected, string label) =>
            Check(actual == expected, label + " [" + cultureName + "]: " + actual);

        const ulong gib = 1073741824;
        Equal(ProcessDisplayFormatter.SystemRam(16 * gib, 6 * gib),
            $"10{decimalMark}0 / 16{decimalMark}0{space}GiB · 62{decimalMark}5{space}%",
            "R19 system RAM uses physical used and total with a consistent percentage");
        Equal(ProcessDisplayFormatter.SystemRam(8 * gib, 8 * gib),
            $"0{decimalMark}0 / 8{decimalMark}0{space}GiB · 0{decimalMark}0{space}%",
            "R19 genuinely available entire RAM is measured zero usage");
        Equal(ProcessDisplayFormatter.SystemRam(8 * gib, 0),
            $"8{decimalMark}0 / 8{decimalMark}0{space}GiB · 100{decimalMark}0{space}%",
            "R19 no available RAM is full usage");
        foreach (var invalidRam in new (ulong? Total, ulong? Available)[] {
            (null, null), (null, 0), (16 * gib, null), (0, 0), (gib, 2 * gib) })
            Equal(ProcessDisplayFormatter.SystemRam(invalidRam.Total, invalidRam.Available), "N/D",
                "R19 missing or inconsistent RAM cannot become zero");
        Check(!ProcessDisplayFormatter.SystemRam(ulong.MaxValue, 0).Contains("N/D"),
            "R19 large unsigned RAM counters do not overflow [" + cultureName + "]");

        Equal(ProcessDisplayFormatter.Percent(26), "26" + decimalMark + "0" + space + "%", "percent with separated unit");
        Equal(ProcessDisplayFormatter.Seconds(16.5), "16" + decimalMark + "5" + space + "s", "sample interval with culture and unit");
        Equal(ProcessDisplayFormatter.CpuDuration(636.0625), "10" + space + "min 36" + decimalMark + "063" + space + "s", "screenshot CPU time, rounded display");
        Equal(ProcessDisplayFormatter.CpuDuration(0), "0" + space + "ms", "genuinely measured zero");
        Equal(ProcessDisplayFormatter.CpuDuration(0.0001), "< 1" + space + "ms", "positive sub-millisecond is not zero");
        Equal(ProcessDisplayFormatter.CpuDuration(0.001), "1" + space + "ms", "one millisecond");
        Equal(ProcessDisplayFormatter.CpuDuration(0.01), "10" + space + "ms", "ten milliseconds, no leading zero");
        Equal(ProcessDisplayFormatter.CpuDuration(0.0625), "63" + space + "ms", "sub-second rounding");
        Equal(ProcessDisplayFormatter.CpuDuration(1), "1" + space + "s", "one second, no tick suffix");
        Equal(ProcessDisplayFormatter.CpuDuration(16.5), "16" + decimalMark + "5" + space + "s", "fractional seconds");
        Equal(ProcessDisplayFormatter.CpuDuration(0.9996), "1" + space + "s", "carry into a second");
        Equal(ProcessDisplayFormatter.CpuDuration(59.9996), "1" + space + "min 0" + space + "s", "carry into a minute");
        Equal(ProcessDisplayFormatter.CpuDuration(3599.9996), "1" + space + "h 0" + space + "s", "carry into an hour");
        Equal(ProcessDisplayFormatter.CpuDuration(86399.9996), "1" + space + "g 0" + space + "s", "carry into a day");
        Equal(ProcessDisplayFormatter.CpuDuration(90061.125), "1" + space + "g 1" + space + "h 1" + space + "min 1" + decimalMark + "125" + space + "s", "multi-day CPU time");
        foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
            Equal(ProcessDisplayFormatter.CpuDuration(invalid), "N/D", "invalid duration is not zero");
        foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity })
        {
            Equal(ProcessDisplayFormatter.Seconds(invalid), "N/D", "invalid interval is unavailable");
            Equal(ProcessDisplayFormatter.Percent(invalid), "N/D", "invalid CPU percentage is unavailable");
        }

        var process = new ProcessSnapshot { Pid = 9608, ParentPid = 100, Name = "example.exe" };
        var pending = ProcessNetworkPresentation.Describe(process);
        Check(pending.Contains("in attesa") && !pending.Contains("LISTEN): 0"), "pending inspection has no invented zero [" + cultureName + "]");
        var partial = process with { IsDeepInspected = true, DeepInspectionNote = "TCP non enumerabile: AccessDenied" };
        var partialText = ProcessNetworkPresentation.Describe(partial);
        Check(partialText.Contains("AccessDenied") && partialText.Contains("zero non garantisce") &&
            partialText.Contains("byte trasferiti non sono misurati"), "partial acquisition retains error and limits [" + cultureName + "]");
        var listener = partial with
        {
            TcpListeningCount = 1, TcpEstablishedCount = 2, UdpEndpointCount = 3,
            PublicRemoteConnectionCount = 2, ListeningTcpPorts = new[] { 8080, 8443 },
            NetworkFamilyProcessCount = 7, NetworkFamilyProcessesWithSockets = 1,
            NetworkFamilyPublicRemoteConnectionCount = 5,
            RelatedNetworkProcesses = new[] { new RelatedNetworkProcessSnapshot(12576, "helper.exe",
                "Servizio di rete Chromium/Electron (dichiarato)", 0, 5, 1, 5, Array.Empty<NetworkEndpointSnapshot>()) }
        };
        var network = ProcessNetworkPresentation.Describe(listener);
        Check(network.Contains("PID 9608\nTCP in ascolto (LISTEN): 1\nConnessioni TCP stabilite (ESTABLISHED): 2\nEndpoint UDP: 3\n") &&
            network.Contains("Porte TCP in ascolto: 8080, 8443"), "direct counters and ports have separate lines [" + cultureName + "]");
        Check(network.Contains("\n\nProcessi correlati per nome e albero\nProcessi con socket: 1 su 7\n") &&
            network.Contains("totale del gruppo: 5"), "group counters separated from selected PID [" + cultureName + "]");
        Check(network.Contains("\n\nRuolo di rete suggerito dai parametri: PID 12576\n") &&
            network.Contains("Connessioni TCP stabilite: 5\nEndpoint UDP: 1"), "role and related socket owner preserved [" + cultureName + "]");
        Check(!network.Contains("rete delegata") && !network.Contains("nessuna connessione"), "layout does not invent delegation or absence [" + cultureName + "]");
        var fallback = listener with
        {
            DeepInspectionNote = null,
            RelatedNetworkProcesses = new[] { new RelatedNetworkProcessSnapshot(12577, "other.exe", "Ruolo non determinato",
                0, 6, 0, 4, Array.Empty<NetworkEndpointSnapshot>()) }
        };
        var fallbackText = ProcessNetworkPresentation.Describe(fallback);
        Check(fallbackText.Contains("\nPID 12577 (Ruolo non determinato)") && !fallbackText.Contains("Esito e limiti di acquisizione"),
            "related-process fallback and missing note [" + cultureName + "]");
        Check(listener.TcpListeningCount == 1 && listener.NetworkFamilyPublicRemoteConnectionCount == 5 &&
            listener.DeepInspectionNote == "TCP non enumerabile: AccessDenied", "presentation preserves source counters and errors [" + cultureName + "]");
    }
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
    CultureInfo.CurrentUICulture = originalUiCulture;
}
Console.WriteLine("Presentation regressions complete (it-IT + en-US).");
