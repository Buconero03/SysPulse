using ProcessLens.Core;
using ProcessLens.Semantics;

static void Require(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL " + label);
    Console.WriteLine("PASS " + label);
}

static ProcessQueryResult Analyze(ProcessSnapshot p)
{
    var insight = new ProcessIntelligenceAnalyzer().Analyze(p);
    return new ProcessQueryEngine().Ask(p, insight, "Cosa sta facendo?", null);
}

var baseline = new ProcessSnapshot { Pid = 100, ParentPid = 30, Name = "unknown.exe" };
var baselineInsight = new ProcessIntelligenceAnalyzer().Analyze(baseline);
var semanticText = baselineInsight.DetectionBasis;
// La sintesi usa etichette brevi, mentre i dettagli tecnici conservano identita e ruolo.
// Verifichiamo entrambi i livelli senza dipendere da diciture eliminate nella v0.19.9.
Require(baselineInsight.Evidence.Any(e => e.StartsWith("Identità:", StringComparison.Ordinal)),
    "UI text: identity evidence retained in technical details");
Require(baselineInsight.Evidence.Any(e => e.StartsWith("Funzione del processo:", StringComparison.Ordinal)),
    "UI text: role evidence retained in technical details");
Require(semanticText.Contains("Metodo: regole locali.", StringComparison.Ordinal),
    "UI text: clear classification evidence level");
Require(semanticText.Contains("Fonte:", StringComparison.Ordinal),
    "UI text: classification evidence source declared");
Require(semanticText.Contains("attività in corso", StringComparison.OrdinalIgnoreCase),
    "UI text: live activity not inferred from role classification");
Require(!semanticText.Contains("%", StringComparison.Ordinal),
    "UI text: heuristic scores not presented as calibrated percentages");
Require(!semanticText.Contains("ruolo certa", StringComparison.OrdinalIgnoreCase),
    "UI text: no ungrammatical certainty label");
Require(!semanticText.Contains("knowledge:", StringComparison.OrdinalIgnoreCase),
    "UI text: internal rule ID excluded from the summary");

var unknown = Analyze(baseline);
Require(unknown.Title.Contains("approfondita", StringComparison.OrdinalIgnoreCase), "query uses deep explanation");
Require(unknown.Answer.Contains("Operazione interna sconosciuta"), "unknown process avoids invented operation");
Require(unknown.Answer.Contains("LIMITI"), "limitations explicit");

var py = Analyze(baseline with
{
    Pid = 101,
    Name = "python.exe",
    CommandLine = "python.exe -m http.server 8765 --token=ULTRA_SECRET",
    IsDeepInspected = true,
    TcpListeningCount = 1,
    ListeningTcpPorts = new int[] { 8765 },
    PrivateWorkingSetAvailable = true,
    PrivateWorkingSetBytes = 25 * 1024 * 1024
});
Require(py.Answer.Contains("http.server"), "python -m module explained");
// Il campione collega direttamente al PID una socket LISTEN sulla porta 8765.
// Lo stato della socket non prova che richieste siano state accettate o dati trasferiti.
Require(py.Answer.Contains("Il PID ha 1 socket TCP nello stato LISTEN", StringComparison.Ordinal) &&
        py.Answer.Contains("porte: 8765", StringComparison.Ordinal),
    "network listening state and port attributed directly");
Require(py.Answer.Contains("TCP LISTEN/ESTABLISHED: richieste accettate e byte trasferiti non misurati.",
        StringComparison.Ordinal),
    "network LISTEN does not imply accepted requests or traffic");
Require(!py.Answer.Contains("ULTRA_SECRET"), "command line secret not exposed");

var git = Analyze(baseline with { Pid = 102, Name = "git.exe", CommandLine = "git -C D:\\Work status --password=ULTRA_SECRET" });
Require(git.Answer.Contains("Lettura dello stato", StringComparison.OrdinalIgnoreCase), "git status identified");
Require(!git.Answer.Contains("ULTRA_SECRET"), "git args secret redacted");
Require(!git.Answer.Contains("D:\\Work"), "git repository path not printed");

var family = Analyze(baseline with
{
    Pid = 103, Name = "helper.exe", IsDeepInspected = true,
    NetworkFamilyProcessCount = 3,
    NetworkFamilyTcpListeningCount = 2
});
Require(family.Answer.Contains("NON direttamente su questo PID"), "related process socket not claimed direct");

var ps = Analyze(baseline with
{
    Pid = 104, Name = "pwsh.exe",
    CommandLine = "pwsh.exe -EncodedCommand ULTRA_SECRET"
});
Require(ps.Answer.Contains("codificato"), "encoded Powershell command detected");
Require(!ps.Answer.Contains("ULTRA_SECRET"), "encoded payload not printed");

var quoted = Analyze(baseline with
{
    Pid = 105, Name = "python.exe",
    CommandLine = "python.exe \"D:\\My Work\\scan.py\" --password ULTRA_SECRET"
});
Require(quoted.Answer.Contains("scan.py"), "quoted python script basename identified");
Require(!quoted.Answer.Contains("D:\\My Work"), "python script directory not exposed");
Require(!quoted.Answer.Contains("ULTRA_SECRET"), "python credentials not exposed");

// Il rapporto RAM usa misure reali e sezioni leggibili, non una diagnosi inventata.
var memoryInput = baseline with
{
    Pid = 106, Name = "renderer.exe", PrivateWorkingSetAvailable = true,
    PrivateWorkingSetBytes = 88L * 1024 * 1024,
    WorkingSetAvailable = true, WorkingSetBytes = 162L * 1024 * 1024,
    PrivateCommitAvailable = true, PrivateBytes = 127L * 1024 * 1024,
    CpuSampleAvailable = true, CpuPercent = 0.02
};
var memoryInsight = new ProcessIntelligenceAnalyzer().Analyze(memoryInput);
var trend = MemoryTrendAnalyzer.Analyze("Working Set privato", new[]
{
    new MemoryObservation(DateTime.UtcNow.AddMinutes(-4), 106L * 1024 * 1024),
    new MemoryObservation(DateTime.UtcNow.AddMinutes(-2), 96L * 1024 * 1024),
    new MemoryObservation(DateTime.UtcNow, 88L * 1024 * 1024)
});
var vmMap = new MemoryMapSnapshot(true, "Mappa sintetica", 90L * 1024 * 1024,
    250L * 1024 * 1024, 130L * 1024 * 1024, 0, 42, false);
var ram = new ProcessQueryEngine().Ask(memoryInput, memoryInsight,
    "Perché consuma così tanta RAM o memoria?", null, memoryTrend: trend, memoryMap: vmMap);
var physical = new SystemMemoryEvidence(16UL * 1024 * 1024 * 1024, 9UL * 1024 * 1024 * 1024);
var ramContext = new ProcessQueryEngine().Ask(memoryInput, memoryInsight,
    "Perché occupa RAM?", null, memoryTrend: trend, memoryMap: vmMap, systemMemory: physical);
Require(ramContext.Answer.Contains("VALUTAZIONE: È UN CONSUMO ECCESSIVO?"), "assessment section present");
Require(ramContext.Answer.Contains("CONSUMO IN CALO"), "falling trend correctly qualified");
Require(ramContext.Answer.Contains("Il carico sulla RAM del sistema è normale"), "low RAM system pressure distinguished");
var highPressure = new ProcessQueryEngine().Ask(memoryInput, memoryInsight,
    "Perché occupa RAM?", null, memoryTrend: trend,
    systemMemory: new SystemMemoryEvidence(16UL * 1024 * 1024 * 1024, 1UL * 1024 * 1024 * 1024));
Require(highPressure.Answer.Contains("Uso RAM elevato"), "high RAM system pressure reported");
Require(highPressure.Answer.Contains("non identifica un responsabile"), "no unsupported process blame");
Require(ram.Title == "Perché occupa RAM?", "memory route recognized");
Require(ram.Answer.Contains("■ DIAGNOSI IN BREVE"), "memory executive summary");
Require(ram.Answer.Contains("■ COME SI DISTRIBUISCE LA MEMORIA"), "memory physical versus committed explained");
Require(ram.Answer.Contains("■ ANDAMENTO MISURATO NEL TEMPO"), "memory trend given with evidence");
Require(ram.Answer.Contains("■ MAPPA DELLA MEMORIA VIRTUALE"), "virtual map separated from physical pages");
Require(ram.Answer.Contains("■ QUANDO APPROFONDIRE"), "safe next steps");
Require(ram.Answer.Contains("non basta per diagnosticare un memory leak", StringComparison.OrdinalIgnoreCase), "does not fabricate leak");
Require(ram.Answer.Contains("88,0") || ram.Answer.Contains("88.0"), "actual physical private MiB present");
var missing = new ProcessQueryEngine().Ask(baseline, new ProcessIntelligenceAnalyzer().Analyze(baseline),
    "Perché occupa RAM?", null);
Require(missing.Confidence == "DATI INSUFFICIENTI", "unavailable metrics not fabricated");

Console.WriteLine("Process behavior explanation tests passed.");

// R7: source-aware messages must not convert absent metrics, text or correlations into facts.
var queryEngineR7 = new ProcessQueryEngine();
ProcessQueryResult QueryR7(ProcessSnapshot p, string question, ProcessProgressSample? previous = null,
    MemoryTrendReport? memory = null) => queryEngineR7.Ask(p,
        new ProcessIntelligenceAnalyzer().Analyze(p), question, previous, memoryTrend: memory);
var noCpuHealth = QueryR7(baseline, "È bloccato?");
Require(noCpuHealth.Confidence == "DATI INSUFFICIENTI" &&
        !noCpuHealth.Evidence.Any(e => e.Contains("CPU media")), "R7 health does not treat missing CPU as zero");
var tcpHealth = QueryR7(baseline with { IsDeepInspected = true, TcpEstablishedCount = 1 }, "È bloccato?");
Require(tcpHealth.Confidence == "DATI INSUFFICIENTI" && tcpHealth.Answer.Contains("non permette"),
    "R7 established socket without CPU does not exclude a stall");
var highCpuHealth = QueryR7(baseline with { CpuSampleAvailable = true, CpuPercent = 20 }, "È bloccato?");
Require(highCpuHealth.Confidence == "NON DETERMINATO" && highCpuHealth.Answer.Contains("blocco parziale"),
    "R7 high CPU does not establish process health");
var partialNetwork = QueryR7(baseline with
{
    IsDeepInspected = true, DeepInspectionNote = "TCP non enumerabile: AccessDenied"
}, "Ha porte aperte?");
Require(partialNetwork.Title == "Porte e connessioni" &&
        partialNetwork.Answer.Contains("AccessDenied") && partialNetwork.Answer.Contains("non garantisce assenza"),
    "R7 partial network acquisition preserves error and does not assert absence");
var listenerWithFamily = QueryR7(baseline with
{
    IsDeepInspected = true, TcpListeningCount = 1,
    NetworkFamilyProcessCount = 2, NetworkFamilyProcessesWithSockets = 2,
    RelatedNetworkProcesses = new[] { new RelatedNetworkProcessSnapshot(201, "helper.exe",
        "Servizio di rete Chromium/Electron (dichiarato)", 0, 1, 0, 1, Array.Empty<NetworkEndpointSnapshot>()) }
}, "Ha connessioni di rete?");
Require(listenerWithFamily.Answer.Contains("Socket TCP LISTEN rilevate: 1") &&
        !listenerWithFamily.Answer.Contains("questo PID non possiede socket proprie") &&
        listenerWithFamily.Answer.Contains("non dimostra") && !listenerWithFamily.Answer.Contains("rete delegata"),
    "R7 family sockets do not prove delegation or erase direct listener");
var textualProgress = baseline with { CommandLine = "example.exe --progress=50% --password=ULTRA_SECRET" };
var progressR7 = QueryR7(textualProgress, "A che punto è?");
Require(progressR7.Confidence == "INDICATORE TESTUALE DA VERIFICARE" &&
        progressR7.Answer.Contains("Parametri di avvio") && progressR7.Answer.Contains("obiettivo") &&
        !progressR7.Answer.Contains("ULTRA_SECRET"), "R7 launch text is not verified work progress");
var titledProgress = QueryR7(baseline with { WindowTitles = new[] { "Download file 4/10 SECRET_TITLE" } }, "A che punto è?");
Require(titledProgress.Answer.Contains("Titolo finestra") && !titledProgress.Answer.Contains("SECRET_TITLE"),
    "R7 title indicator source is explicit and private title omitted");
var previousR7 = new ProcessProgressSample(textualProgress.Pid, null, DateTime.UtcNow.AddMinutes(-1),
    40, "command-line:percent", "synthetic previous indicator");
var etaR7 = QueryR7(textualProgress, "Quando finirà?", previousR7);
Require(etaR7.Confidence == "STIMA CONDIZIONATA DA DUE CAMPIONI TESTUALI" &&
        etaR7.Answer.Contains("non è un intervallo di confidenza") && etaR7.Answer.Contains("velocità resti costante"),
    "R7 ETA range is arithmetic scenario rather than calibrated confidence");
var oscillating = new MemoryTrendReport("Working Set privato", 3, 120, 100, 100, 100, 300, 0);
var oscillationR7 = QueryR7(memoryInput, "Perché occupa RAM?", memory: oscillating);
Require(oscillationR7.Answer.Contains("VARIAZIONE NETTA LIMITATA") && oscillationR7.Answer.Contains("oscillazioni intermedie") &&
        !oscillationR7.Answer.Contains("TENDENZA STABILE"), "R7 equal endpoints do not prove stable intermediate usage");
Require(QueryR7(baseline with { CpuSampleAvailable = true, CpuPercent = 2, CpuAverage30Seconds = 1,
    CpuPeak30Seconds = 2 }, "Perché usa CPU?").Answer.Contains("fino a 30 s"),
    "R7 CPU window is bounded and may be shorter than thirty seconds");
Console.WriteLine("R7 behavior source and inference regressions passed.");

// R7 R1: the exact UI suggestion must reach Network, including plural ports.
foreach (string question in new[] { "Ha porte aperte?", "Ha una porta aperta?", "Quali PORTE sono aperte?", "Open port?", "Open ports?" })
{
    var answer = QueryR7(baseline with { IsDeepInspected = true, DeepInspectionNote = "TCP non enumerabile: AccessDenied" }, question);
    Require(answer.Title == "Porte e connessioni" && answer.Answer.Contains("AccessDenied") &&
        answer.Answer.Contains("non garantisce assenza"), "R7 R1 port wording reaches network: " + question);
}
foreach (string question in new[] { "Il consumo di RAM comporta problemi?", "Riporta il consumo di RAM", "Supporta il confronto RAM?" })
{
    var answer = QueryR7(baseline, question);
    Require(answer.Title == "Perché occupa RAM?", "R7 R1 embedded porta does not add Network: " + question);
}
Console.WriteLine("R7 R1 network question routing regressions passed.");

// R11: large private allocations are separate facts, not a parenthesized wall of text.
foreach (string cultureName in new[] { "it-IT", "en-US" })
{
    var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
    try
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        var layoutMap = new MemoryMapSnapshot(true, "fixture", (long)(638.3 * 1048576), 10L * 1048576, 5L * 1048576,
            0, 100, false, ReadWritePrivateBytes: (long)(560.8 * 1048576), LargeBlocksCount: 31,
            LargeBlocksBytes: (long)(407.5 * 1048576));
        var layoutTrend = new MemoryTrendReport("Working Set privato", 3, 120, 100, 80, 80, 100, -20);
        var answer = new ProcessQueryEngine().Ask(memoryInput, memoryInsight, "Perché occupa RAM?", null,
            memoryTrend: layoutTrend, memoryMap: layoutMap, systemMemory: physical);
        string summary = answer.Answer.Split("■ VALUTAZIONE:", StringSplitOptions.None)[0];
        Require(summary.Contains("allocazioni private: " + 638.3.ToString("F1") + " MiB") &&
            summary.Contains("\n• Pagine private in lettura/scrittura (RW): " + 560.8.ToString("F1") + " MiB") &&
            summary.Contains("\n• Blocchi ≥4 MiB: 31 · totale " + 407.5.ToString("F1") + " MiB"),
            "R11 summary keeps each allocation number in a separate readable fact [" + cultureName + "]");
        Require(!System.Text.RegularExpressions.Regex.IsMatch(summary, @"\([^)]*\(") &&
            !summary.Contains("heap/strutture dati") && !answer.Answer.Contains("SALDO ") &&
            answer.Answer.Contains("CONSUMO IN CALO: -20") && answer.Answer.Contains("negli ultimi 2"),
            "R11 no nested parentheses or inferred heap objects; trend has explicit duration [" + cultureName + "]");
        Require(answer.Answer.Contains("non è una misura delle pagine fuori RAM") &&
            answer.Answer.Contains("COMPONENTI TIPICI · NON MISURATI"),
            "R11 short wording preserves counter and object-measurement limits [" + cultureName + "]");
    }
    finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
}

foreach (string culture in new[] { "it-IT", "en-US" })
{
    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
    foreach (string path in new[] { @"D:\Secret Folder\scan.py", "/home/private-user/Secret Folder/scan.py" })
    {
        var script = baseline with { Name = "python.exe", CommandLine = "python.exe \"" + path + "\"" };
        var result = new ProcessQueryEngine().Ask(script, new ProcessIntelligenceAnalyzer().Analyze(script), "Cosa sta facendo?", null);
        Require(result.Answer.Contains("scan.py") && !result.Answer.Contains("Secret Folder"), "R25 script summary keeps only basename across path separators [" + culture + "]");
        Require(script.CommandLine.Contains(path), "R25 original script command line is preserved [" + culture + "]");
    }
}
