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
            {
                evidence.Add($"Analisi DLL/moduli: {p.ModuleNames.Count} nomi rilevati (non equivalgono alle funzioni chiamate).");
                string? moduleSummary = DescribeLoadedSubsystems(p.ModuleNames);
                if (moduleSummary is not null)
                    activity.Add(moduleSummary);
            }
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
        string originEn = closest is null
            ? $"Windows reports PPID {p.ParentPid}, but no verified parent instance is available in the current snapshot."
            : closest.VerifiedByStartTime
                ? $"Observed parent process is {Clean(closest.Name)} (PID {closest.Pid}); creation timestamps are consistent."
                : $"Possible parent {Clean(closest.Name)} (PID {closest.Pid}), but temporal parentage is not verifiable.";
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

    private static string BuildEnglishBehaviorSection(
        ProcessSnapshot p,
        ProcessInsight i,
        bool hasRoleEvidence,
        string? operationIt,
        string originEn)
    {
        var idEn = new List<string> { $"Process: {i.FriendlyName} ({p.Name}, PID {p.Pid})." };
        if (!string.IsNullOrWhiteSpace(p.ProductName))
            idEn.Add($"Declared file product: {Clean(p.ProductName)}.");
        if (!string.IsNullOrWhiteSpace(p.CompanyName))
            idEn.Add($"Declared publisher: {Clean(p.CompanyName)}.");
        if (p.ServiceDisplayNames.Count > 0)
            idEn.Add($"Hosted Windows services: {string.Join(", ", p.ServiceDisplayNames.Take(5).Select(Clean))}.");

        string roleEnText = SysPulseLocalization.Translate(Clean(i.Role), AppLanguage.English);
        string whatEnText = SysPulseLocalization.Translate(Clean(i.WhatIsIt), AppLanguage.English);
        string roleEn = hasRoleEvidence
            ? $"Estimated role: {roleEnText}. {whatEnText}"
            : $"Role to verify: {roleEnText}. {whatEnText}";

        string opEn = operationIt is null
            ? "Internal operation unknown. Process name, CPU and RAM do not identify internal functions or files being processed."
            : TranslateOperationToEnglish(operationIt) + " Command recognized. Outcome and progress are not measured.";

        var actEn = new List<string>();
        if (p.CpuSampleAvailable)
        {
            string loadEn = p.CpuPercent switch { >= 10 => "high", >= 2 => "measurable", >= 0.2 => "light", _ => "very low" };
            actEn.Add($"CPU in sampled interval: {p.CpuPercent:F1}% of total PC capacity ({loadEn}).");
        }
        else actEn.Add("CPU in sampled interval: sample not available.");

        if (p.PrivateWorkingSetAvailable)
            actEn.Add($"Private Working Set: {p.PrivateWorkingSetMb:F1} MiB.");
        else if (p.WorkingSetAvailable)
            actEn.Add($"Total Resident RAM: {p.WorkingSetMb:F1} MiB, including shareable pages.");

        if (p.IsDeepInspected)
        {
            if (p.TcpListeningCount > 0)
            {
                string ports = p.ListeningTcpPorts.Count > 0 ? $" (ports: {string.Join(", ", p.ListeningTcpPorts.Take(12))})" : "";
                actEn.Add($"This PID owns {p.TcpListeningCount} TCP socket(s) in LISTEN state{ports}.");
            }
            if (p.TcpEstablishedCount > 0)
                actEn.Add($"This PID owns {p.TcpEstablishedCount} TCP connection(s) in ESTABLISHED state.");
            if (p.UdpEndpointCount > 0)
                actEn.Add($"This PID owns {p.UdpEndpointCount} UDP endpoint(s).");
            bool direct = p.TcpListeningCount > 0 || p.TcpEstablishedCount > 0 || p.UdpEndpointCount > 0;
            bool family = p.NetworkFamilyTcpListeningCount > 0 || p.NetworkFamilyTcpEstablishedCount > 0 || p.NetworkFamilyUdpEndpointCount > 0;
            if (!direct && family && p.NetworkFamilyProcessCount > 1)
                actEn.Add("Network sockets were observed in related processes of the same executable, NOT directly on this PID.");
            actEn.Add(p.HasVisibleWindow
                ? "Windows associates at least one visible top-level window with this PID."
                : "No visible main window is associated with this process.");
        }
        else
            actEn.Add("Network, windows and DLLs: deep inspection not yet executed.");

        if (p.ChildCount > 0)
            actEn.Add($"Child processes: {p.ChildCount}. Same-name children: {p.SameNameChildCount}.");

        return
            "ENGLISH PROCESS ANALYSIS\n" +
            "WHAT IT IS\n" + string.Join(" ", idEn) + "\n\n" +
            "WHAT IT DOES (ESTIMATED ROLE)\n" + roleEn + "\n\n" +
            "IDENTIFIABLE OPERATION\n" + opEn + "\n\n" +
            "DETECTED RESOURCES & STATE\n" + string.Join("\n", actEn.Select(s => "• " + s)) + "\n\n" +
            "PARENT PROCESS & ORIGIN\n" + originEn + "\n\n" +
            "LIMITATIONS\n" +
            "Stack, heap and file contents: not read. Digital signature: not verified. File metadata: unauthenticated. " +
            "Estimated role; executing code not inspected." +
            (p.TcpListeningCount > 0 || p.TcpEstablishedCount > 0
                ? " TCP LISTEN/ESTABLISHED: accepted requests and transferred bytes are not measured."
                : "");
    }

    private static string? DescribeLoadedSubsystems(IReadOnlyList<string> modules)
    {
        var tags = new List<string>();
        if (modules.Any(m => m.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("d3d12.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("opengl32.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("vulkan-1.dll", StringComparison.OrdinalIgnoreCase)))
            tags.Add("grafica/GPU (DirectX/OpenGL/Vulkan)");
        if (modules.Any(m => m.Equals("ws2_32.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("wininet.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("mswsock.dll", StringComparison.OrdinalIgnoreCase)))
            tags.Add("stack di rete Windows (WinSock/WinHTTP)");
        if (modules.Any(m => m.Equals("crypt32.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("bcrypt.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("ncrypt.dll", StringComparison.OrdinalIgnoreCase)))
            tags.Add("crittografia/certificati (Crypt32/BCrypt)");
        if (modules.Any(m => m.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("clr.dll", StringComparison.OrdinalIgnoreCase)))
            tags.Add("runtime .NET CLR");
        if (modules.Any(m => m.StartsWith("sqlite", StringComparison.OrdinalIgnoreCase) ||
                             m.Equals("winsqlite3.dll", StringComparison.OrdinalIgnoreCase)))
            tags.Add("motore database locale SQLite");

        return tags.Count == 0
            ? null
            : $"Librerie caricate compatibili con sottosistemi: {string.Join(", ", tags)} (la presenza della DLL non prova chiamate attive).";
    }

    private static string TranslateOperationToEnglish(string opIt)
    {
        return opIt
            .Replace("L'interprete Python è stato avviato con il modulo ", "Python interpreter started with module ", StringComparison.Ordinal)
            .Replace("Python è stato avviato con lo script ", "Python started with script ", StringComparison.Ordinal)
            .Replace("Python esegue codice fornito sulla riga di comando, il cui contenuto non viene esposto", "Python executes inline command-line code (content redacted)", StringComparison.Ordinal)
            .Replace("Node.js è stato avviato con l'entrypoint ", "Node.js started with entrypoint ", StringComparison.Ordinal)
            .Replace("Il runtime .NET è stato avviato con l'assembly ", ".NET runtime started with assembly ", StringComparison.Ordinal)
            .Replace("dotnet watch esegue un'applicazione e può riavviarla alla modifica dei sorgenti", "dotnet watch runs an application and reloads on source changes", StringComparison.Ordinal)
            .Replace("PowerShell è stato avviato con lo script ", "PowerShell started with script ", StringComparison.Ordinal)
            .Replace("PowerShell ha ricevuto un comando codificato; non è stato eseguito o decodificato dall'analizzatore", "PowerShell received an encoded command (not decoded or executed by analyzer)", StringComparison.Ordinal)
            .Replace("PowerShell esegue un comando passato come parametro, senza esporne il contenuto nel report", "PowerShell executes a parameter command (content redacted)", StringComparison.Ordinal)
            .Replace("curl è stato invocato per inviare dati a un endpoint remoto", "curl invoked to upload data to a remote endpoint", StringComparison.Ordinal)
            .Replace("curl è stato invocato per richiedere intestazioni HTTP", "curl invoked to request HTTP headers", StringComparison.Ordinal)
            .Replace("curl è stato invocato come client di rete; il contenuto scambiato e l'esito non sono osservabili", "curl invoked as a network client; payload and outcome are not observable", StringComparison.Ordinal)
            .Replace("ffmpeg è stato avviato per elaborare contenuti multimediali forniti come input", "ffmpeg started to process input media streams", StringComparison.Ordinal)
            .Replace("Host servizi Windows avviato per il gruppo ", "Windows Service Host started for group ", StringComparison.Ordinal)
            .Replace("Host componente COM isolato avviato per AppID ", "Isolated COM Surrogate started for AppID ", StringComparison.Ordinal)
            .Replace("rundll32 è stato avviato con la libreria ", "rundll32 started with library ", StringComparison.Ordinal)
            .Replace("La JVM è stata avviata con l'archivio ", "JVM started with JAR archive ", StringComparison.Ordinal);
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
        if (name is "svchost" or "svchost.exe")
        {
            var svcGroup = Regex.Match(cmd, @"(?i)(?:^|\s)-k\s+([a-zA-Z0-9_.\-]{1,64})");
            var svcName = Regex.Match(cmd, @"(?i)(?:^|\s)-s\s+([a-zA-Z0-9_.\-]{1,64})");
            if (svcGroup.Success)
            {
                evidence.Add("Parametri svchost -k/-s osservati nella command line.");
                string suffix = svcName.Success ? $" (servizio specifico: {svcName.Groups[1].Value})" : "";
                return $"Host servizi Windows avviato per il gruppo {svcGroup.Groups[1].Value}{suffix}";
            }
        }
        if (name is "dllhost" or "dllhost.exe")
        {
            var clsid = Regex.Match(cmd, @"(?i)/processid:\{?([0-9a-fA-F\-]{8,36})\}?");
            if (clsid.Success)
            {
                evidence.Add("Parametro COM /Processid osservato nella command line.");
                return $"Host componente COM isolato avviato per AppID {{{clsid.Groups[1].Value}}}";
            }
        }
        if (name is "rundll32" or "rundll32.exe")
        {
            var dll = FileNameFromCommand(cmd, @"\.dll");
            if (dll is not null)
            {
                evidence.Add("Libreria DLL target osservata nella command line di rundll32.");
                return $"rundll32 è stato avviato con la libreria {dll}";
            }
        }
        if (name is "java" or "java.exe" or "javaw" or "javaw.exe")
        {
            var jar = FileNameFromCommand(cmd, @"\.jar");
            if (jar is not null)
            {
                evidence.Add("Archivio .jar osservato nella command line JVM.");
                return $"La JVM è stata avviata con l'archivio {jar}";
            }
        }
        if (name is "msbuild" or "msbuild.exe")
        {
            var proj = FileNameFromCommand(cmd, @"\.(?:sln|csproj|vbproj|vcxproj)");
            if (proj is not null)
            {
                evidence.Add("File di progetto/soluzione osservato nella command line MSBuild.");
                return $"MSBuild è stato avviato per il progetto {proj}";
            }
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
