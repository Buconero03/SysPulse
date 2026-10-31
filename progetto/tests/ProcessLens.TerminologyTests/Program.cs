﻿using ProcessLens.Core;
using ProcessLens.Semantics;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL terminology: " + message);
    Console.WriteLine("PASS terminology: " + message);
}

Check(ProcessTerminology.EvidenceStrength(100) == "Corrispondenza molto alta", "highest score describes rule correspondence");
Check(ProcessTerminology.EvidenceStrength(68) == "Corrispondenza media", "medium score is not called certain");
Check(ProcessTerminology.EvidenceStrength(0) == "Corrispondenza insufficiente", "unknown is not certainty");
Check(ProcessTerminology.FunctionLabel(95) == "Ruolo identificato",
    "high classifier uses a concise, qualified label");
Check(ProcessTerminology.FunctionLabel(68) == "Ruolo stimato", "partial role has clear label");
Check(ProcessTerminology.FunctionLabel(20) == "Funzione sconosciuta", "insufficient score admits uncertainty");
Check(ProcessTerminology.CategoryLabel("Background") == "Secondario",
    "internal background category does not imply inactivity");
Check(ProcessTerminology.CategoryLabel("WSL") == "WSL", "other internal categories retained");

var unknown = new ProcessIntelligenceAnalyzer().Analyze(
    new ProcessSnapshot { Pid = 12, ParentPid = 1, Name = "unknown.exe" });
Check(unknown.DetectionLabel == "Funzione sconosciuta", "unidentified process remains unidentified");
Check(unknown.DetectionBasis.Contains("classificazione", StringComparison.OrdinalIgnoreCase),
    "classification clearly distinguished from direct measurement");
Check(!unknown.DetectionBasis.Contains("knowledge:", StringComparison.OrdinalIgnoreCase),
    "internal rule identifiers remain hidden from summary");
Check(!unknown.DetectionBasis.Contains("94%"), "percentages do not appear as statistical confidence");

var browser = new ProcessIntelligenceAnalyzer().Analyze(new ProcessSnapshot
{
    Pid = 24, Name = "chrome.exe", ParentPid = 19, ParentName = "chrome.exe",
    CommandLine = "chrome.exe --type=renderer"
});
Check(browser.DetectionBasis.Contains("attività in corso", StringComparison.OrdinalIgnoreCase),
    "function label does not claim moment-by-moment activity");
Check(browser.RoleConfidencePercent >= 60, "renderer recognized by rules rather than fabricated evidence");
Check(browser.Evidence.Count > 0, "raw evidence retained in diagnostics");
var nodeUtility = new ProcessIntelligenceAnalyzer().Analyze(new ProcessSnapshot
{
    Pid = 14344, ParentPid = 13200, Name = "example.exe", ParentName = "example.exe",
    CommandLine = "example.exe --type=utility --utility-sub-type=node.mojom.NodeService"
});
Check(nodeUtility.Role == "Processo ausiliario Node.js", "Electron Node utility uses a professional term");
Check(nodeUtility.Summary == "Parametri: possibile processo ausiliario Node.js", "Node utility summary is concise");
Check(!nodeUtility.WhyRunning.Contains("richiede questo servizio interno", StringComparison.OrdinalIgnoreCase),
    "reason for execution not invented from command line");
Check(nodeUtility.DetectionBasis.Contains("Parametri di avvio", StringComparison.OrdinalIgnoreCase),
    "classification cites its evidence type");
Check(nodeUtility.DetectionBasis.Contains("attività in corso", StringComparison.OrdinalIgnoreCase),
    "role classification does not prove live activity");
Check(nodeUtility.Evidence.Any(x => x.Contains("--utility-sub-type=node.mojom.NodeService", StringComparison.OrdinalIgnoreCase)),
    "raw technical evidence preserved separately");


// v0.19.16 R5: the user-facing summary must explain the evidence instead of showing
// three abstract confidence tiers. Keep internal heuristic scoring in the audit only.
var pythonSnapshot = new ProcessSnapshot
{
    Pid = 28740, ParentPid = 29684, Name = "python.exe", ParentName = "conda.exe",
    CommandLine = "python.exe script.py"
};
var pythonInsight = new ProcessIntelligenceAnalyzer().Analyze(pythonSnapshot);
var readableIdentification = ProcessDetailPresentation.IdentificationExplanation(pythonSnapshot, pythonInsight);
Check(readableIdentification.Count == 3 && readableIdentification[0].Value.Contains("python.exe")
    && readableIdentification[0].Value.Contains("28740"), "UI identity comes from actual Windows snapshot");
Check(readableIdentification[1].Value.Contains("conda.exe")
    && readableIdentification[1].Value.Contains("non conferma l’appartenenza"),
    "UI association explains why conda is proposed without claiming ownership");
Check(readableIdentification[2].Value.Contains("script.py")
    && readableIdentification[2].Value.Contains("Istruzione in esecuzione non rilevata")
    && !readableIdentification.Any(f => f.Value.Contains("Indizi forti") || f.Value.Contains("95%")),
    "UI role distinguishes classification from observed activity and avoids numeric confidence");

// v0.19.10: presentation tests use pure Core code; no WPF or Windows runtime needed.
var originalCommand = "\"C:\\Program Files\\Chrome\\chrome.exe\" --type=renderer --lang=it-IT --renderer-client-id=101";
var shownCommand = ProcessDetailPresentation.FormatCommandLine(originalCommand);
Check(shownCommand.Contains("\n--type=renderer\n--lang=it-IT\n--renderer-client-id=101"),
    "command line options are separated for display");
Check(originalCommand.Contains(" --lang="), "original command line remains unchanged");
Check(shownCommand.Replace("\n", " ") == originalCommand, "visual line breaks preserve each command argument byte");
Check(ProcessDetailPresentation.FormatCommandLine("example.exe \"quoted --text=untouched\" --type=utility")
    .Contains("\"quoted --text=untouched\"\n--type=utility"), "quoted content not broken into false parameters");
Check(ProcessDetailPresentation.FormatCommandLine(null) == "Accesso non disponibile", "missing command line is explicit");
var facts = ProcessDetailPresentation.ParseTechnicalFacts("Nome eseguibile: chrome.exe\nCPU media/picco 30s: 2% / 3%\nPID - TCP listen/established: 0/1");
Check(facts.Count == 3 && facts[0].Group == "Identità" && facts[1].Group == "Risorse" && facts[2].Group == "Relazioni",
    "technical facts are organized without losing original values");
Check(ProcessDetailPresentation.EvidenceGroup("Origine: PPID verificato") == "Avvio e relazioni",
    "origin evidence has its own section");
Check(ProcessDetailPresentation.EvidenceGroup("Ruolo: renderer") == "Funzione e ambiente di esecuzione",
    "role evidence is separated from observed behavior");

// v0.19.11: classification UI shows meaningful fields; raw rule IDs stay in the diagnostic view.
var example = new ProcessSnapshot
{
    Pid = 4208, Name = "chrome.exe", ParentPid = 100, ParentName = "chrome.exe",
    CommandLine = "chrome.exe --type=renderer --secret-password=do-not-display"
};
var exampleInsight = new ProcessIntelligenceAnalyzer().Analyze(example);
Check(exampleInsight.Evidence.Count(e => e.Contains("intensità degli indizi", StringComparison.OrdinalIgnoreCase)) == 0,
    "repetitive confidence phrases removed from evidence");
Check(exampleInsight.Evidence.Any(e => e.StartsWith("Funzione del processo:", StringComparison.Ordinal)),
    "technical role evidence kept under an explicit label");
var exampleFacts = ProcessDetailPresentation.ClassificationFacts(example, exampleInsight);
Check(exampleFacts.Any(x => x.Label == "File eseguibile" && x.Value == "chrome.exe"),
    "classification facts include actual executable identity");
Check(exampleFacts.Any(x => x.Label == "Parametro riconosciuto" && x.Value == "--type=renderer"),
    "classification explains the concrete renderer parameter");
Check(exampleFacts.All(x => !x.Value.Contains("knowledge:", StringComparison.OrdinalIgnoreCase)
                           && !x.Value.Contains("secret-password", StringComparison.OrdinalIgnoreCase)),
    "classification summary excludes internal rule identifiers and sensitive arguments");
Check(exampleFacts.Any(x => x.Label == "Limite dell'analisi"),
    "classification explicitly states what has not been observed");
var unknownFacts = ProcessDetailPresentation.ClassificationFacts(
    new ProcessSnapshot { Pid = 21, ParentPid = 0, Name = "unknown.exe" }, unknown);
Check(unknownFacts.Any(x => x.Label == "Tipo suggerito" && x.Value == "Funzione sconosciuta"),
    "unknown process cannot acquire an invented function");
Check(ProcessDetailPresentation.ClosingRiskLabel("BASSO / MEDIO") == "Effetti variabili",
    "risk badge does not present a false precise grade");
var winProcess = new ProcessIntelligenceAnalyzer().Analyze(new ProcessSnapshot
{
    Pid = 25, ParentPid = 0, Name = "powershell.exe", ProductName = "Sistema operativo Microsoft® Windows®",
    FileDescription = "Windows PowerShell"
});
Check(winProcess.FriendlyName == "Windows PowerShell", "generic Windows product name does not hide executable identity");


// v0.19.14: diagnostic evidence is organized into spaced, optional groups.
// Every unmodified original entry stays in the raw report for technical inspection.
var originalEvidence = exampleInsight.Evidence.ToArray();
var auditFacts = ProcessDetailPresentation.ClassificationAuditFacts(exampleInsight);
Check(auditFacts.Any(f => f.Group == "Valutazione interna" &&
    f.Label == "Punteggio assegnato dalle regole" && f.Value.Contains("/100") &&
    f.Value.Contains("non è una probabilità", StringComparison.Ordinal)),
    "classification audit separates rule score from statistical probabilities");
Check(auditFacts.Any(f => f.Group == "Funzione e ambiente di esecuzione" &&
    f.Label == "Funzione del processo"),
    "classification audit has a distinct role evidence section");
Check(originalEvidence.SequenceEqual(exampleInsight.Evidence),
    "classification audit never mutates original evidence");
var abbreviatedAudit = ProcessDetailPresentation.ClassificationAuditFacts(exampleInsight, maxEvidence: 0);
Check(abbreviatedAudit.Any(f => f.Label == "Altri dati disponibili") &&
    abbreviatedAudit.All(f => f.Group == "Valutazione interna" ||
                              f.Label == "Altri dati disponibili"),
    "bounded audit rendering points to original evidence without fabricating details");
Check(ProcessDetailPresentation.ClassificationAuditFacts(exampleInsight)
    .All(f => !f.Label.Contains("intensità degli indizi", StringComparison.OrdinalIgnoreCase)),
    "audit labels avoid internal classifier wording");


// Metadata separated into readable rows without altering the original source.
var verboseEvidence = exampleInsight with
{
    Evidence = new[]
    {
        "Identità: percorso eseguibile disponibile; descrizione file = Chrome; prodotto = Chrome; publisher metadata = Google LLC",
        "Funzione del processo: knowledge:role.chromium.renderer; Chromium dichiara --type=renderer"
    }
};
var separated = ProcessDetailPresentation.ClassificationAuditFacts(verboseEvidence);
Check(separated.Any(f => f.Label == "Descrizione del file eseguibile" && f.Value == "Chrome"),
    "long metadata evidence renders as separately labelled fields");
Check(separated.Any(f => f.Label == "Editore dichiarato" && f.Value == "Google LLC"),
    "publisher metadata is displayed with a precise label");
Check(separated.Any(f => f.Label == "Identificativo della regola interna" &&
    f.Value == "knowledge:role.chromium.renderer"),
    "internal rule identifier stays in opt-in technical evidence");
Check(verboseEvidence.Evidence[0].Contains("; descrizione file = Chrome"),
    "original semicolon-separated evidence remains unmodified");

Check(exampleFacts.Any(x => x.Label == "Dati utilizzati"),
    "data source has a dedicated UI field");
Check(exampleFacts.Count(x => x.Label == "Limite dell'analisi") == 1,
    "classification limitations do not repeat within the same facts panel");
Console.WriteLine("Terminology and evidence boundary tests passed, including v0.19.15 controlled vocabulary.");


// v0.19.16: Windows TCP state and process-name equality cannot prove live I/O or temporal parentage.
var listenProcess = new ProcessSnapshot
{
    Pid = 500, ParentPid = 0, Name = "listener-sample.exe", IsDeepInspected = true,
    TcpListeningCount = 1, ListeningTcpPorts = new[] { 8080 }
};
var listenInsight = new ProcessIntelligenceAnalyzer().Analyze(listenProcess);
Check(listenInsight.WhatIsIt.Contains("LISTEN", StringComparison.Ordinal) &&
      !listenInsight.WhatIsIt.Contains("dimostra che il processo accetta", StringComparison.OrdinalIgnoreCase),
    "TCP LISTEN reports socket state, not accepted client requests");
Check(listenInsight.WhatIsIt.Contains("non misura richieste già accettate né byte trasferiti", StringComparison.OrdinalIgnoreCase),
    "TCP LISTEN explicitly declares accepted requests and byte transfers are not measured");
// The knowledge JSON takes precedence over the generic fallback. Both must preserve this limit.
Check(listenInsight.Evidence.Any(e => e.Contains("socket TCP LISTEN", StringComparison.OrdinalIgnoreCase)),
    "TCP LISTEN retains the directly observed socket as technical evidence");
var listenExplanation = ProcessBehaviorExplainer.Explain(listenProcess, listenInsight);
Check(listenExplanation.Answer.Contains("LISTEN", StringComparison.Ordinal) &&
      listenExplanation.Answer.Contains("richieste accettate e byte trasferiti non misurati", StringComparison.OrdinalIgnoreCase),
    "TCP LISTEN user report separates socket state from unmeasured traffic");

var establishedProcess = new ProcessSnapshot
{
    Pid = 501, ParentPid = 0, Name = "network-sample.exe", IsDeepInspected = true,
    TcpEstablishedCount = 2
};
var establishedInsight = new ProcessIntelligenceAnalyzer().Analyze(establishedProcess);
Check(establishedInsight.WhatIsIt.Contains("ESTABLISHED", StringComparison.Ordinal) &&
      !establishedInsight.WhyRunning.Contains("scambia dati", StringComparison.OrdinalIgnoreCase),
    "TCP ESTABLISHED does not infer byte transfer");
var establishedExplanation = ProcessBehaviorExplainer.Explain(establishedProcess, establishedInsight);
Check(establishedExplanation.Answer.Contains("ESTABLISHED", StringComparison.Ordinal) &&
      establishedExplanation.Answer.Contains("byte trasferiti", StringComparison.OrdinalIgnoreCase),
    "network explanation separates observations from unmeasured traffic");

var homonymProcess = new ProcessSnapshot
{
    Pid = 502, ParentPid = 400, ParentName = "helper-sample.exe", Name = "helper-sample.exe"
};
var homonymInsight = new ProcessIntelligenceAnalyzer().Analyze(homonymProcess);
Check(!homonymInsight.WhatIsIt.Contains("dimostra", StringComparison.OrdinalIgnoreCase) &&
      homonymInsight.WhatIsIt.Contains("omonimia", StringComparison.OrdinalIgnoreCase),
    "matching parent name does not establish verified process instance");
Check(homonymInsight.WhyRunning.Contains("verificare", StringComparison.OrdinalIgnoreCase),
    "homonymous parent requires PID plus start-time verification");

var gpuProcess = new ProcessSnapshot
{
    Pid = 503, ParentPid = 0, Name = "chrome.exe", CommandLine = "chrome.exe --type=gpu-process"
};
var gpuInsight = new ProcessIntelligenceAnalyzer().Analyze(gpuProcess);
Check(gpuInsight.Role.Contains("GPU", StringComparison.OrdinalIgnoreCase) &&
      gpuInsight.WhatIsIt.Contains("--type=gpu-process", StringComparison.Ordinal),
    "Chromium GPU label cites precise start parameter");
Check(gpuInsight.WhatIsIt.Contains("non specifica", StringComparison.OrdinalIgnoreCase),
    "GPU process role cannot prove current graphics operation");

// R7: exercise name/metadata rules and runtime fallback paths independently of CPU/RAM.
var analyzerR7 = new ProcessIntelligenceAnalyzer();
foreach (var snapshot in new[]
{
    new ProcessSnapshot { Pid = 701, ParentPid = 10, ParentName = "editor.exe", Name = "updater.exe" },
    new ProcessSnapshot { Pid = 702, ParentPid = 10, ParentName = "editor.exe", Name = "crashpad.exe" },
    new ProcessSnapshot { Pid = 703, ParentPid = 10, ParentName = "editor.exe", Name = "python.exe", CommandLine = "python.exe -m example" },
    new ProcessSnapshot { Pid = 704, ParentPid = 10, ParentName = "editor.exe", Name = "python.exe", CommandLine = "python.exe --multiprocessing-fork" },
    new ProcessSnapshot { Pid = 705, ParentPid = 10, ParentName = "editor.exe", Name = "dotnet.exe", CommandLine = "dotnet.exe VBCSCompiler.dll" },
    new ProcessSnapshot { Pid = 706, ParentPid = 10, ParentName = "editor.exe", Name = "node.exe", CommandLine = "node.exe --tool=tsserver" },
    new ProcessSnapshot { Pid = 707, ParentPid = 10, ParentName = "editor.exe", Name = "java.exe", CommandLine = "java.exe -jar example.jar" },
    new ProcessSnapshot { Pid = 708, ParentPid = 10, ParentName = "editor.exe", Name = "pwsh.exe" }
})
{
    var classified = analyzerR7.Analyze(snapshot);
    Check(!classified.WhyRunning.Contains("lo ha avviato") && !classified.WhyRunning.Contains("Avviato da") &&
          !classified.WhyRunning.Contains("lo usa per") && !classified.WhyRunning.Contains("Rimane pronto"),
        "R7 role does not invent launch cause: " + snapshot.Pid);
    if (snapshot.Pid == 704)
        Check(classified.WhyRunning.Contains("Catena di avvio non verificata", StringComparison.Ordinal) &&
              classified.WhyRunning.Contains("Autore e motivo dell’avvio sconosciuti", StringComparison.Ordinal),
            "R7 launch limitation explicit: " + snapshot.Pid);
    else
        Check((classified.WhyRunning.Contains("non", StringComparison.OrdinalIgnoreCase) || classified.WhyRunning.Contains("sconosciut", StringComparison.OrdinalIgnoreCase)),
            "R7 launch limitation explicit: " + snapshot.Pid);
}
var coordinatorR7 = analyzerR7.Analyze(new ProcessSnapshot
{
    Pid = 709, ParentPid = 0, Name = "desktop.exe", IsDeepInspected = true,
    HasVisibleWindow = true, ChildCount = 2, SameNameChildCount = 2
});
Check(!coordinatorR7.Summary.Contains("Coordina") &&
    (coordinatorR7.WhatIsIt.Contains("Coordinamento non misurato") || !coordinatorR7.Role.Contains("principale")),
    "R7 visible window and children do not prove coordination");
Check(!analyzerR7.Analyze(new ProcessSnapshot { Pid = 710, ParentPid = 0, Name = "updater.exe" })
    .Summary.Contains("Controlla o applica"), "R7 updater name does not prove an active update");
Check(!analyzerR7.Analyze(new ProcessSnapshot { Pid = 711, ParentPid = 0, Name = "crashpad.exe" })
    .WhyRunning.Contains("Rimane pronto"), "R7 crash handler name does not prove readiness");
var criticalR7 = analyzerR7.Analyze(new ProcessSnapshot { Pid = 712, ParentPid = 0, Name = "critical.exe", IsCritical = true });
Check(criticalR7.WhyRunning.Contains("Flag critico") && !criticalR7.WhyRunning.Contains("Windows lo avvia"),
    "R7 critical flag does not identify launch cause");
var unknownRuntimeR7 = analyzerR7.Analyze(new ProcessSnapshot { Pid = 713, ParentPid = 0, Name = "unknown.exe" });
Check(unknownRuntimeR7.Framework == "Runtime non determinato", "R7 no known runtime does not prove native execution");
Console.WriteLine("R7 terminology contextual regressions passed.");

// R7 R2: all recognized multiprocessing indicators preserve their evidence and provenance limit.
foreach (string command in new[]
{
    "python.exe --multiprocessing-fork",
    "python.exe -c multiprocessing.spawn",
    "python.exe -c multiprocessing.forkserver"
})
{
    foreach (string? parentName in new string?[] { "editor.exe", null })
    {
        var snapshot = new ProcessSnapshot { Pid = 714, ParentPid = parentName is null ? 0 : 10,
            ParentName = parentName, Name = "python.exe", CommandLine = command };
        var classified = analyzerR7.Analyze(snapshot);
        Check(classified.Role == "Worker multiprocessing Python" &&
              classified.Evidence.Any(e => e.Contains("indicatori multiprocessing nella command line Python")),
            "R7 R2 multiprocessing role and input evidence retained: " + command);
        Check(parentName is null
            ? classified.WhyRunning.Contains("motivo dell’avvio sconosciuto", StringComparison.Ordinal)
            : classified.WhyRunning.Contains("Padre: editor.exe", StringComparison.Ordinal) &&
              classified.WhyRunning.Contains("Catena di avvio non verificata", StringComparison.Ordinal) &&
              classified.WhyRunning.Contains("Autore e motivo dell’avvio sconosciuti", StringComparison.Ordinal),
            "R7 R2 multiprocessing launch limits with/without parent: " + command);
        var changedInput = analyzerR7.Analyze(snapshot with { CpuSampleAvailable = true, CpuPercent = 80,
            PrivateWorkingSetAvailable = true, PrivateWorkingSetBytes = 256L * 1024 * 1024 });
        Check(changedInput.WhyRunning == classified.WhyRunning,
            "R7 R2 CPU and RAM do not establish multiprocessing launch cause: " + command);
    }
}
Console.WriteLine("R7 R2 multiprocessing provenance regressions passed.");

var uxUnknown = new ProcessIntelligenceAnalyzer().Analyze(new ProcessSnapshot { Pid = 901, ParentPid = 0, Name = "unknown-ux.exe" });
Check(uxUnknown.Summary.EndsWith(": Non classificato", StringComparison.Ordinal), "R11 unknown summary is concise without inventing a function");
var uxParent = new ProcessIntelligenceAnalyzer().Analyze(new ProcessSnapshot { Pid = 902, ParentPid = 0, Name = "unknown-ux.exe", ChildCount = 3, SameNameChildCount = 3 });
Check(uxParent.Summary == "3 sottoprocessi", "R11 subprocess summary keeps the observed count");
Check(ProcessDetailPresentation.TechnicalGroup("Working Set privato") == "Risorse" &&
    ProcessDetailPresentation.TechnicalGroup("RAM privata residente") == "Risorse",
    "R11 current and legacy memory labels remain in resource facts");
