using System.Text.RegularExpressions;
using ProcessLens.Core;

namespace ProcessLens.Semantics;

/// <summary>
/// On-demand explanation of the selected process from observed Windows facts.
/// Never executes the target executable, uploads process metadata or treats
/// file metadata/parent PID/connection count as evidence of internal work.
/// </summary>
public static class ProcessBehaviorExplainer
{
    public static ProcessQueryResult Explain(ProcessSnapshot p, ProcessInsight i)
    {
        var evidence = new List<string>();
        var identity = new List<string>();
        identity.Add($"Processo: {i.FriendlyName} ({p.Name}, PID {p.Pid}).");
        if (!string.IsNullOrWhiteSpace(p.ProductName))
        {
            identity.Add($"Prodotto dichiarato dal file: {Clean(p.ProductName)}.");
            evidence.Add("ProductName: metadato del file; autenticità non verificata.");
        }
        if (!string.IsNullOrWhiteSpace(p.CompanyName))
        {
            identity.Add($"Produttore dichiarato: {Clean(p.CompanyName)}.");
            evidence.Add("CompanyName: metadato del file; firma digitale non verificata.");
        }
        if (!string.IsNullOrWhiteSpace(p.FileDescription) && string.IsNullOrWhiteSpace(p.ProductName))
        {
            identity.Add($"Descrizione del file: {Clean(p.FileDescription)}.");
            evidence.Add("FileDescription: metadato del file; funzione e autenticità non verificate.");
        }
        if (p.ServiceDisplayNames.Count > 0)
        {
            identity.Add($"Servizi Windows ospitati: {string.Join(", ", p.ServiceDisplayNames.Take(5).Select(Clean))}.");
            evidence.Add("Associazione PID-servizio rilevata dal Service Control Manager.");
        }

        bool hasRoleEvidence = i.RoleConfidencePercent >= 70;
        string role = hasRoleEvidence
            ? $"Ruolo stimato: {Clean(i.Role)}. {Clean(i.WhatIsIt)}"
            : $"Ruolo da verificare: {Clean(i.Role)}. {Clean(i.WhatIsIt)}";
        evidence.Add($"Classificazione locale; criteri nei dettagli tecnici.");

        string? operation = IdentifyOperation(p, evidence);
        string operationText = operation is null
            ? "Operazione interna sconosciuta. Nome, CPU e RAM non identificano funzioni o file elaborati."
            : operation + " Comando riconosciuto. Esito e avanzamento non rilevati.";

        var activity = new List<string>();
        if (p.CpuSampleAvailable)
        {
            string load = p.CpuPercent switch { >= 10 => "elevata", >= 2 => "misurabile", >= 0.2 => "leggera", _ => "molto bassa" };
            activity.Add($"CPU nell’intervallo campionato: {p.CpuPercent:F1}% della capacità totale del PC ({load}).");
            evidence.Add("CPU: contatore Windows del PID. Funzioni eseguite non rilevate.");
        }
        else activity.Add("CPU nell’intervallo campionato: campione non disponibile.");
        if (p.PrivateWorkingSetAvailable)
            activity.Add($"Working Set privato: {p.PrivateWorkingSetMb:F1} MiB.");
        else if (p.WorkingSetAvailable)
            activity.Add($"RAM residente complessiva: {p.WorkingSetMb:F1} MiB, incluse pagine condivisibili.");

        if (p.IsDeepInspected)
        {
            if (p.TcpListeningCount > 0)
            {
                string ports = p.ListeningTcpPorts.Count > 0 ? $" (porte: {string.Join(", ", p.ListeningTcpPorts.Take(12))})" : "";
                activity.Add($"Il PID ha {p.TcpListeningCount} socket TCP nello stato LISTEN{ports}.");
                evidence.Add("Tabella TCP Windows/IP Helper: socket LISTEN intestate al PID selezionato.");
            }
            if (p.TcpEstablishedCount > 0)
            {
                activity.Add($"Il PID ha {p.TcpEstablishedCount} connessioni TCP nello stato ESTABLISHED.");
                evidence.Add("Tabella TCP Windows/IP Helper: ESTABLISHED intestate al PID selezionato.");
            }
            if (p.UdpEndpointCount > 0)
            {
                activity.Add($"Il PID possiede {p.UdpEndpointCount} endpoint UDP.");
                evidence.Add("Tabella UDP Windows/IP Helper: endpoint intestati al PID selezionato.");
            }
            bool direct = p.TcpListeningCount > 0 || p.TcpEstablishedCount > 0 || p.UdpEndpointCount > 0;
            bool family = p.NetworkFamilyTcpListeningCount > 0 || p.NetworkFamilyTcpEstablishedCount > 0 || p.NetworkFamilyUdpEndpointCount > 0;
            if (!direct && family && p.NetworkFamilyProcessCount > 1)
                activity.Add("La rete è stata osservata in processi correlati dello stesso eseguibile, NON direttamente su questo PID.");
            if (p.HasVisibleWindow)
            {
                activity.Add("Windows associa al PID almeno una finestra visibile.");
                evidence.Add("EnumWindows: finestra top-level associata al PID.");
            }
            else
                activity.Add("Nessuna finestra principale visibile associata a questo processo.");
            if (p.ModuleNames.Count > 0)
                evidence.Add($"Analisi DLL/moduli: {p.ModuleNames.Count} nomi rilevati (non equivalgono alle funzioni chiamate).");
            if (!string.IsNullOrWhiteSpace(p.DeepInspectionNote) && !p.DeepInspectionNote.Equals("Analisi Windows completata", StringComparison.OrdinalIgnoreCase))
                evidence.Add("Limiti di acquisizione: " + Clean(p.DeepInspectionNote));
        }
        else
            activity.Add("Rete, finestre e DLL: analisi approfondita non eseguita.");

        if (p.ChildCount > 0)
        {
            activity.Add($"Sottoprocessi: {p.ChildCount}. Con lo stesso nome: {p.SameNameChildCount}.");
            evidence.Add("Snapshot albero processi: figli conteggiati al momento della lettura.");
        }

        var closest = p.AncestorDetails.FirstOrDefault();
        string origin = closest is null
            ? $"Windows riporta il PPID {p.ParentPid}, ma non è disponibile un'istanza padre verificata nella lettura attuale."
            : closest.VerifiedByStartTime
                ? $"Il processo padre osservato è {Clean(closest.Name)} (PID {closest.Pid}); gli orari di avvio sono coerenti. Questo non dimostra l'intenzione dell'utente."
                : $"Possibile padre {Clean(closest.Name)} (PID {closest.Pid}), ma la parentela non è verificabile temporalmente.";
        evidence.Add(closest is null ? "PPID senza verifica dell'istanza." : "Process tree Windows: " + (closest.VerifiedByStartTime ? "timestamp padre-figlio compatibili." : "parentela non verificata."));

        string confidence = operation is not null && hasRoleEvidence
            ? "TIPO SUGGERITO E PARAMETRI RICONOSCIUTI; ATTIVITÀ INTERNA NON VERIFICATA"
            : hasRoleEvidence ? "TIPO SUGGERITO DALLE REGOLE; COMPITO INTERNO NON VERIFICATO" : "IDENTIFICAZIONE PARZIALE — LIMITI ESPLICITI";

        string answer =
            "COS'È\n" + string.Join(" ", identity) + "\n\n" +
            "A COSA SERVE\n" + role + "\n\n" +
            "OPERAZIONE IDENTIFICABILE\n" + operationText + "\n\n" +
            "RISORSE E STATO RILEVATO\n" + string.Join("\n", activity.Select(s => "• " + s)) + "\n\n" +
            "PROCESSO PADRE E ORIGINE\n" + origin + "\n\n" +
            "LIMITI\n" +
            "Stack, heap e contenuti dei file: non letti. " +
            "Firma digitale: non verificata. Metadati del file: non autenticati. " +
            "Ruolo stimato; codice in esecuzione non rilevato." +
            (p.TcpListeningCount > 0 || p.TcpEstablishedCount > 0
                ? " TCP LISTEN/ESTABLISHED: richieste accettate e byte trasferiti non misurati."
                : "");

        return new ProcessQueryResult("Analisi approfondita — cosa fa questo processo", answer, confidence,
            evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray(), null);
    }

    private static string? IdentifyOperation(ProcessSnapshot p, List<string> evidence)
    {
        string cmd = p.CommandLine ?? "";
        string name = (p.Name ?? "").ToLowerInvariant();
        if ((name is "git" or "git.exe") || name.StartsWith("git-remote-", StringComparison.Ordinal))
        {
            var git = ProcessOriginAnalyzer.Analyze(p);
            if (git.Operation.Contains("sottocomando non identificabile", StringComparison.OrdinalIgnoreCase) ||
                git.Operation.Contains("non determinata", StringComparison.OrdinalIgnoreCase))
                return null;
            evidence.Add("Sottocomando Git ricavato dalla command line del PID o da un antenato con istanza verificata.");
            // The origin analyzer may include a -C working directory: redact it from this report.
            return git.Operation.Split(" Directory indicata", StringSplitOptions.None)[0].TrimEnd('.');
        }
        if (string.IsNullOrWhiteSpace(cmd)) return null;
        if (name.StartsWith("python", StringComparison.Ordinal))
        {
            var module = Regex.Match(cmd, @"(?i)(?:^|\s)-m\s+([\w.\-]{1,100})(?=\s|$)");
            if (module.Success) { evidence.Add("Opzione Python -m presente nella command line, nome modulo esposto."); return $"L'interprete Python è stato avviato con il modulo {module.Groups[1].Value}"; }
            var py = FileNameFromCommand(cmd, @"\.pyw?");
            if (py is not null) { evidence.Add("Entrypoint .py/.pyw identificato nella command line."); return $"Python è stato avviato con lo script {py}"; }
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)-c(?:\s|$)")) { evidence.Add("Opzione -c Python rilevata; contenuto dello script non mostrato."); return "Python esegue codice fornito sulla riga di comando, il cui contenuto non viene esposto"; }
        }
        if (name is "node" or "node.exe" or "nodejs" or "nodejs.exe")
        {
            var js = FileNameFromCommand(cmd, @"\.(?:js|mjs|cjs|ts)");
            if (js is not null) { evidence.Add("Entrypoint JavaScript/TypeScript osservato nella command line."); return $"Node.js è stato avviato con l'entrypoint {js}"; }
        }
        if (name is "dotnet" or "dotnet.exe")
        {
            var dll = FileNameFromCommand(cmd, @"\.dll");
            if (dll is not null) { evidence.Add("Assembly DLL nella command line dotnet."); return $"Il runtime .NET è stato avviato con l'assembly {dll}"; }
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)watch\s+run(?:\s|$)")) { evidence.Add("Subcomando dotnet watch run osservato."); return "dotnet watch esegue un'applicazione e può riavviarla alla modifica dei sorgenti"; }
        }
        if (name is "powershell" or "powershell.exe" or "pwsh" or "pwsh.exe")
        {
            var script = FileNameFromCommand(cmd, @"\.ps1");
            if (script is not null) { evidence.Add("Script PS1 individuato nella command line."); return $"PowerShell è stato avviato con lo script {script}"; }
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)-(?:encodedcommand|enc)(?:\s|$)")) { evidence.Add("Parametro di codice codificato osservato, non decodificato."); return "PowerShell ha ricevuto un comando codificato; non è stato eseguito o decodificato dall'analizzatore"; }
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)-(?:command|c)(?:\s|$)")) { evidence.Add("Parametro -Command osservato; contenuto non mostrato."); return "PowerShell esegue un comando passato come parametro, senza esporne il contenuto nel report"; }
        }
        if (name is "curl" or "curl.exe")
        {
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)(?:-T|--upload-file)(?:\s|=)")) { evidence.Add("Opzione curl di upload rilevata."); return "curl è stato invocato per inviare dati a un endpoint remoto"; }
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)(?:-I|--head)(?:\s|$)")) { evidence.Add("Opzione curl per header HTTP rilevata."); return "curl è stato invocato per richiedere intestazioni HTTP"; }
            evidence.Add("Eseguibile curl con command line disponibile; URL non mostrati.");
            return "curl è stato invocato come client di rete; il contenuto scambiato e l'esito non sono osservabili";
        }
        if ((name is "ffmpeg" or "ffmpeg.exe") && cmd.Length > 0)
        {
            if (Regex.IsMatch(cmd, @"(?i)(?:^|\s)-i(?:\s|$)")) { evidence.Add("Opzione ffmpeg -i rilevata; percorso sorgente nascosto."); return "ffmpeg è stato avviato per elaborare contenuti multimediali forniti come input"; }
        }
        return null;
    }

    private static string? FileNameFromCommand(string command, string suffix)
    {
        // Capture only an entrypoint basename, never the full path or arbitrary arguments.
        string pattern = "(?i)(?:^|\\s)(?:\\\"([^\\\"]+" + suffix + ")\\\"|([^\\s\\\"]+" + suffix + "))(?=\\s|$)";
        var match = Regex.Match(command, pattern);
        if (!match.Success) return null;
        string value = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Replace('\\', '/');
        string file = value[(value.LastIndexOf('/') + 1)..];
        return file.Length is > 0 and <= 120 ? file : null;
    }

    private static string Clean(string? text)
    {
        // Keep text bounded and single-line, since version info is untrusted input.
        return string.IsNullOrWhiteSpace(text) ? "non disponibile" :
            new string(text.Trim().Where(c => !char.IsControl(c)).Take(140).ToArray());
    }
}
