using System.Text.RegularExpressions;
using ProcessLens.Core;

namespace ProcessLens.Semantics;

internal static partial class RoleClassifier
{
    // Windows paths can also arrive in portable reports/tests; never expose their directories in summaries.
    private static string FileNameForDisplay(string path) => Path.GetFileName(path.Replace('\\', '/'));

    public static RoleAssessment Classify(ProcessSnapshot p, IdentityAssessment id, FrameworkAssessment fw, OwnershipAssessment owner)
    {
        string cmd = p.CommandLine ?? string.Empty;
        string n = p.Name.ToLowerInvariant();

        if (p.IsCritical)
            return R("Processo critico di sistema", "Componente essenziale di Windows",
                "Componente critico del sistema operativo.",
                "Flag critico: Windows. Autore dell’avvio sconosciuto.",
                "Non terminarlo: può causare instabilità grave o arresto del sistema.", "CRITICO", "Windows", 100,
                "flag di processo critico fornito dal sistema");

        if (p.ServiceDisplayNames.Count > 0)
        {
            string services = string.Join(", ", p.ServiceDisplayNames);
            return R("Processo di servizio Windows", $"Ospita: {services}",
                "Ospita servizi registrati in SCM.",
                $"Servizi SCM: {services}. Motivo dell’avvio sconosciuto.",
                "La chiusura può interrompere i servizi associati; usa Gestione servizi quando possibile.", "MEDIO / ALTO", "Background", 99,
                $"associazione diretta PID -> servizio: {services}");
        }

        // Generic knowledge rules are evaluated before product-specific/fallback heuristics.
        // They combine DLL/module fingerprints with command line, windows, network and
        // process-tree evidence. A rule must meet its minimum score before it can classify.
        var knowledgeRole = KnowledgeSignatureEngine.MatchRole(p, fw.Name);
        if (knowledgeRole is not null)
        {
            string summary = knowledgeRole.Summary ?? $"Ruolo tecnico: {knowledgeRole.Label}";
            string meaning = knowledgeRole.Meaning ?? "Ruolo suggerito dalle regole; i segnali possono derivare dalla stessa fonte.";
            string why = knowledgeRole.WhyRunning ?? (p.ParentName is null ? "Origine sconosciuta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.");
            string impact = knowledgeRole.CloseImpact ?? "La terminazione interrompe la funzione associata al processo.";
            string risk = knowledgeRole.CloseRisk ?? "DA VERIFICARE";
            string category = knowledgeRole.Category ?? CategoryFor(p, fw);
            string evidenceText = $"knowledge:{knowledgeRole.Id}; {string.Join("; ", knowledgeRole.Evidence.Take(6))}";
            return R(knowledgeRole.Label, summary, meaning, why, impact, risk, category, knowledgeRole.Score, evidenceText);
        }

        var genericNameRole = ClassifyByGenericNameSignals(p, owner, fw);
        if (genericNameRole is not null)
            return genericNameRole;

        if (fw.Name is "Chromium/Electron" or "Electron" or "Microsoft Edge WebView2")
        {
            string? type = Opt(cmd, "--type");
            string? utility = Opt(cmd, "--utility-sub-type");
            if (Eq(type, "renderer"))
                return R("Elaborazione contenuti web", "Tipo renderer indicato nei parametri Chromium",
                    "Chromium utilizza questo tipo di processo per elaborare documenti e codice delle pagine web separatamente dal processo principale. Il parametro di avvio non identifica da solo la pagina associata.",
                    "Ruolo dal parametro --type=renderer. Autore e motivo dell’avvio sconosciuti.",
                    "La parte dell'interfaccia associata può ricaricarsi o mostrare un errore; spesso il processo viene ricreato.", "BASSO / MEDIO", CategoryFor(p, fw), 100,
                    "--type=renderer nella command line");
            if (Eq(type, "gpu-process"))
                return R("Processo GPU", "Tipo GPU indicato nei parametri Chromium",
                    "Il parametro --type=gpu-process indica il processo grafico Chromium, ma non specifica quale operazione grafica stia eseguendo.",
                    "Ruolo grafico dal parametro --type=gpu-process. Carico GPU e motivo dell’avvio non misurati.",
                    "L'app può perdere temporaneamente accelerazione grafica e può ricreare il processo.", "BASSO / MEDIO", CategoryFor(p, fw), 100,
                    "--type=gpu-process nella command line");
            if (Eq(type, "utility"))
            {
                string role = UtilityRole(utility);
                return R(role, UtilitySummary(utility),
                    role == "Processo ausiliario Node.js"
                        ? "I parametri di avvio indicano un processo ausiliario Node.js. Le singole operazioni JavaScript non sono rilevate."
                        : "Il parametro di avvio suggerisce un processo ausiliario Chromium. Il termine servizio qui indica un componente interno, non necessariamente un servizio SCM di Windows.",
                    p.ParentName is null
                        ? "Motivo dell’avvio sconosciuto."
                        : $"Padre: {p.ParentName}. Motivo dell’avvio sconosciuto.",
                    "La funzione associata può interrompersi e il processo può essere ricreato automaticamente.", "BASSO / MEDIO", CategoryFor(p, fw), utility is null ? 92 : 99,
                    utility is null ? "--type=utility" : $"--type=utility; --utility-sub-type={utility}");
            }
            if (!string.IsNullOrWhiteSpace(type))
                return R($"Sottoprocesso Chromium: {type}", $"Tipo dichiarato nei parametri: {type}",
                    "Il ruolo è dichiarato direttamente dal motore Chromium nella command line.",
                    "Ruolo dal parametro --type. Motivo dell’avvio sconosciuto.",
                    "Può interrompere la funzione associata e venire riavviato dall'app.", "MEDIO", CategoryFor(p, fw), 97,
                    $"--type={type}");

            if (p.IsDeepInspected)
            {
                if (p.HasVisibleWindow && p.SameNameChildCount > 0)
                    return R("Processo principale / browser", $"Possibile processo principale con finestra visibile",
                        "Windows associa una finestra top-level al PID e riporta figli omonimi: segnali compatibili con un processo principale/browser, senza prova dell’attività di coordinamento.",
                        $"La finestra visibile e i figli omonimi sono compatibili con un componente principale di {owner.Owner}; l’attività di coordinamento non è stata misurata.",
                        "Terminarlo può chiudere l'intera applicazione.", "MEDIO", CategoryFor(p, fw), 94,
                        "finestra visibile + figli omonimi osservati da Windows");

                if (p.HasVisibleWindow)
                    return R("Host dell'interfaccia", "Possiede una finestra visibile dell'applicazione",
                        "Windows associa direttamente una finestra top-level a questo PID. Senza --type esplicito non lo chiamiamo renderer: sappiamo però che partecipa direttamente all'interfaccia.",
                        $"Windows associa almeno una finestra visibile a questo PID; non è noto il motivo originario dell’avvio.",
                        "La finestra o una parte dell'interfaccia può chiudersi o essere ricreata.", "MEDIO", CategoryFor(p, fw), 84,
                        "finestra top-level visibile associata al PID");

                if (HasAnyModule(p, "libEGL.dll", "libGLESv2.dll", "d3d11.dll", "dxgi.dll") && !p.HasVisibleWindow)
                    return R("Possibile sottoprocesso grafico", "Carica componenti grafici ma non possiede una finestra top-level",
                        "I moduli grafici caricati sono compatibili con rendering/compositing Chromium, ma senza --type=gpu-process ProcessLens propone un ruolo grafico senza verificarne l’attività.",
                        $"{owner.Owner} può usarlo per lavoro grafico isolato.",
                        "Può causare un riavvio del componente grafico o un temporaneo degrado dell'interfaccia.", "MEDIO", CategoryFor(p, fw), 76,
                        "moduli grafici Chromium/DirectX caricati; nessuna finestra top-level");

                if (p.TcpEstablishedCount > 0)
                    return R("Sottoprocesso con connessioni TCP", $"Ha {p.TcpEstablishedCount} connessioni TCP stabilite",
                        "Windows riporta socket TCP in stato ESTABLISHED per questo PID. Lo stato della connessione non indica traffico dati corrente né identifica un servizio di rete specifico.",
                        $"È associato a {owner.Owner}; Windows rileva connessioni TCP stabilite, ma non ne misura qui i byte trasferiti.",
                        "La chiusura può interrompere richieste o sessioni di rete dell'applicazione.", "MEDIO", CategoryFor(p, fw), 70,
                        $"{p.TcpEstablishedCount} connessioni TCP stabilite attribuite al PID");
            }

            if (string.Equals(p.ParentName, p.Name, StringComparison.OrdinalIgnoreCase))
                return R("Sottoprocesso multiprocesso", "Componente interno; ruolo specifico non dimostrato",
                    "Stesso nome del padre. Parentela e ruolo da verificare con gli orari di avvio.",
                    $"Nome del processo padre riportato: {p.ParentName}. La parentela tra istanze richiede una verifica temporale separata.",
                    "Può interrompere una parte dell'app o essere ricreato; l'effetto preciso dipende dal ruolo non esposto.", "MEDIO", CategoryFor(p, fw), 72,
                    "parent omonimo; nessun --type esplicito");

            if (p.SameNameChildCount > 0)
                return R("Possibile processo principale", $"{p.SameNameChildCount} sottoprocessi",
                    "Sottoprocessi con lo stesso nome. Possibile processo principale; coordinamento non misurato.",
                    $"Possibile processo principale: sottoprocessi con lo stesso nome. Coordinamento non misurato.",
                    "Terminarlo può chiudere l'intera applicazione e i sottoprocessi collegati.", "MEDIO", CategoryFor(p, fw), 88,
                    $"ha {p.SameNameChildCount} figli con lo stesso nome");
        }

        if (fw.Name == ".NET")
            return ClassifyDotNet(p, cmd, owner);
        if (fw.Name == "Python")
            return ClassifyPython(p, cmd, owner);
        if (fw.Name == "Node.js")
            return ClassifyNode(p, cmd, owner);
        if (fw.Name == "Java/JVM")
            return ClassifyJava(p, cmd, owner);
        if (fw.Name == "PowerShell")
            return ClassifyPowerShell(p, cmd, owner);
        if (fw.Name == "WSL")
            return R("Componente WSL", "Collega Windows a un ambiente Linux WSL",
                "Fa parte di Windows Subsystem for Linux.", "Componente WSL. Utilizzatore e lavoro in corso sconosciuti.",
                "Può interrompere la sessione Linux associata.", "MEDIO", "WSL", 98, "runtime WSL identificato");

        if (p.IsDeepInspected && p.TcpListeningCount > 0)
            return R("Server / servizio in ascolto",
                $"Ascolta su {p.TcpListeningCount} porta/e TCP" + (p.ListeningTcpPorts.Count > 0 ? $": {string.Join(", ", p.ListeningTcpPorts)}" : string.Empty),
                "Windows attribuisce al PID una o più socket TCP in stato LISTEN. Questa lettura non misura richieste già accettate né byte trasferiti.",
                "La socket è in ascolto sulla porta indicata; non sono osservate richieste già accettate.",
                "La chiusura può interrompere l’ascolto sulle socket attribuite al PID; il servizio potrebbe essere riavviato o avere altri processi.", "MEDIO", CategoryFor(p, fw), 90,
                "socket TCP LISTEN osservate tramite IP Helper API");

        if (p.IsDeepInspected && p.HasVisibleWindow)
            return R("Processo con interfaccia utente", "Possiede una finestra visibile",
                "Windows associa una finestra top-level visibile direttamente a questo PID. Il ruolo interno oltre alla UI non è deducibile con certezza.",
                $"Windows associa una finestra visibile a questo PID; la ragione dell’avvio non è osservata.",
                "La chiusura può chiudere quella finestra o l'applicazione associata.", "MEDIO", CategoryFor(p, fw), 82,
                "finestra top-level visibile attribuita al PID");

        if (p.IsDeepInspected && p.TcpEstablishedCount > 0)
            return R("Connessioni TCP rilevate", $"Windows rileva {p.TcpEstablishedCount} connessioni TCP stabilite",
                "Windows ha rilevato socket TCP nello stato ESTABLISHED attribuiti a questo PID. Questo non dimostra che stia trasferendo dati adesso.",
                p.ParentName is null ? "Può attendere o usare connessioni di rete: lo stato TCP non prova un trasferimento in questo istante." : $"Padre riportato: {p.ParentName}; le connessioni TCP non provano traffico o motivo dell’avvio.",
                "La chiusura interrompe le connessioni di rete del processo.", "MEDIO", CategoryFor(p, fw), 68,
                $"{p.TcpEstablishedCount} connessioni TCP stabilite attribuite al PID");

        if (string.Equals(p.ParentName, p.Name, StringComparison.OrdinalIgnoreCase))
            return R("Sottoprocesso dell'applicazione", $"Componente interno di {owner.Owner}",
                "Windows riporta un processo padre con lo stesso nome; la sola omonimia non verifica il legame temporale fra le istanze né la funzione di questo PID.",
                $"Processo padre indicato: {p.ParentName}; parentela da verificare mediante PID e orari di avvio.",
                "Può interrompere una funzione interna o essere ricreato. ProcessLens non attribuisce un effetto più specifico senza prove.", "DA VERIFICARE", CategoryFor(p, fw), 58,
                "processo padre omonimo");

        if (p.SameNameChildCount > 0)
            return R("Possibile processo principale", $"{p.SameNameChildCount} sottoprocessi",
                "Nella lettura Windows risultano PID figli con lo stesso nome dell’eseguibile. La loro presenza, da sola, non dimostra attività di coordinamento.",
                $"Può appartenere al gruppo di processi di {owner.Owner}; il ruolo preciso non è verificato.",
                "Terminarlo può chiudere l'applicazione e i suoi figli.", "MEDIO", CategoryFor(p, fw), 75,
                $"{p.SameNameChildCount} figli omonimi osservati");

        if (!string.IsNullOrWhiteSpace(p.PackageFullName))
            return R("Processo applicativo", $"Componente dell'app {owner.Owner}",
                "Windows associa questo PID a un package applicativo, ma il ruolo interno non è dichiarato.",
                "Windows identifica il pacchetto applicativo; il motivo dell’avvio e il lavoro interno non sono determinati.",
                "Può chiudere una parte dell'app. L'effetto preciso non è determinato.", "MEDIO", "Applicazioni", 66,
                "identità package disponibile, ruolo interno non esposto");

        string genericCategory = CategoryFor(p, fw);
        return R("Funzione sconosciuta", $"{id.FriendlyName}: Non classificato",
            "Funzione sconosciuta: nome e contatori non bastano.",
            p.ParentName is null ? "Origine sconosciuta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.",
            "Effetto della chiusura non classificato: evita di terminarlo finché il ruolo non è più chiaro.", "DA VERIFICARE", genericCategory, 35,
            "nessun indicatore di ruolo sufficientemente forte");
    }

    private static RoleAssessment ClassifyDotNet(ProcessSnapshot p, string cmd, OwnershipAssessment owner)
    {
        var dllMatch = DllRegex().Match(cmd);
        string? dll = dllMatch.Success
            ? Path.GetFileName(dllMatch.Groups[1].Success ? dllMatch.Groups[1].Value : dllMatch.Groups[2].Value)
            : null;
        if (Eq(dll, "VBCSCompiler.dll"))
            return R("Compiler server Roslyn", "Componente Roslyn suggerito da VBCSCompiler.dll", "È il compiler server della toolchain Roslyn.", "VBCSCompiler.dll è indicato nei parametri di avvio; la richiesta originaria e il riuso corrente non sono osservati.", "Può essere ricreato; una compilazione attiva potrebbe interrompersi.", "BASSO", "Sviluppo", 100, "VBCSCompiler.dll nella command line");
        if (Eq(dll, "MSBuild.dll"))
            return R("Motore di build MSBuild", "Componente MSBuild indicato nei parametri", "MSBuild coordina target, dipendenze e compilazione dei progetti .NET.", "Il processo è compatibile con una build o altra operazione MSBuild; il progresso non è disponibile.", "Può interrompere una build, se in corso.", "BASSO / MEDIO", "Sviluppo", 100, "MSBuild.dll nella command line");
        if (!string.IsNullOrWhiteSpace(dll))
            return R("Applicazione .NET", $"Assembly indicato: {dll}", $"Nei parametri compare l’assembly {dll}; l’attività corrente non è verificata.", $"Assembly indicato nei parametri: {dll}; autore e motivo dell’avvio sconosciuti.", "Interrompe l'applicazione .NET associata.", "BASSO / MEDIO", "Sviluppo", 96, $"assembly .NET esplicito: {dll}");

        foreach (string verb in new[] { "build", "restore", "test", "publish", "run", "watch" })
            if (cmd.Contains($"dotnet {verb}", StringComparison.OrdinalIgnoreCase))
                return R($"dotnet {verb}", $"Comando indicato: dotnet {verb}", "È un comando della CLI .NET.", $"Nella riga di comando compare dotnet {verb}; l’esito e il progresso del comando non sono misurati.", "Interrompe il comando/progetto corrente.", "BASSO / MEDIO", "Sviluppo", 98, $"'dotnet {verb}' nella command line");

        if (p.IsDeepInspected && p.TcpListeningCount > 0)
            return R("Applicazione/servizio .NET in ascolto", $"Runtime .NET con {p.TcpListeningCount} porta/e TCP in ascolto",
                "Il classificatore suggerisce .NET; Windows rileva socket TCP LISTEN attribuite al PID.",
                "Il PID ha socket TCP in ascolto; questo non dimostra che abbia elaborato richieste.", "Interrompe il servizio/app .NET e le porte osservate.", "MEDIO", "Sviluppo", 86,
                "runtime .NET + socket TCP LISTEN osservate");
        if (p.IsDeepInspected && p.HasVisibleWindow)
            return R("Applicazione desktop .NET", "Processo .NET con finestra visibile",
                "Il classificatore suggerisce .NET; Windows associa una finestra top-level al PID.",
                p.ParentName is null ? "L'applicazione .NET è aperta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.", "Può chiudere la finestra o l’app .NET associata.", "MEDIO", "Applicazioni", 82,
                "runtime .NET + finestra top-level visibile");
        return R("Runtime .NET", "Segnali compatibili con .NET; applicazione non determinata", "I segnali disponibili sono compatibili con .NET; assembly e operazione applicativa non sono determinati.", p.ParentName is null ? "Origine sconosciuta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.", "Interrompe il processo .NET associato.", "DA VERIFICARE", "Sviluppo", p.IsDeepInspected ? 64 : 58, p.IsDeepInspected ? "deep inspection completata senza un indicatore di ruolo forte" : "dotnet.exe senza assembly/comando riconoscibile");
    }

    private static RoleAssessment ClassifyPython(ProcessSnapshot p, string cmd, OwnershipAssessment owner)
    {
        // Generic CPython launcher patterns; no product/vendor-specific matching.
        // Do not infer specific workload (training, scraping, etc.) from CPU load alone.
        if (Regex.IsMatch(cmd, @"(?:^|\s)--multiprocessing-fork(?:\s|$)", RegexOptions.IgnoreCase) ||
            cmd.Contains("multiprocessing.spawn", StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains("multiprocessing.forkserver", StringComparison.OrdinalIgnoreCase))
        {
            return R("Worker multiprocessing Python", "Parametri compatibili con multiprocessing Python",
                "Nei parametri compaiono indicatori di multiprocessing CPython; non provano quale lavoro sia in corso o chi lo abbia richiesto.",
                p.ParentName is null ? "Indicatori multiprocessing nei parametri; motivo dell’avvio sconosciuto." : $"Padre: {p.ParentName}. Catena di avvio non verificata. Autore e motivo dell’avvio sconosciuti.",
                "La chiusura può interrompere il pool di worker o l'attività attualmente eseguita.", "MEDIO", "Sviluppo", 97,
                "indicatori multiprocessing nella command line Python");
        }

        var mod = Regex.Match(cmd, "(?:^|\\s)-m\\s+([^\\s\"]+)", RegexOptions.IgnoreCase);
        if (mod.Success)
        {
            string module = mod.Groups[1].Value;
            string meaning = module.StartsWith("ipykernel", StringComparison.OrdinalIgnoreCase)
                ? "Kernel Jupyter/IPython: esegue celle e codice ricevuti dal notebook. Senza telemetria del kernel non sappiamo quale cella sia attiva."
                : module.StartsWith("pip", StringComparison.OrdinalIgnoreCase)
                    ? "Gestore pacchetti Python: installazione, verifica o gestione delle dipendenze richiesta tramite pip."
                    : $"Il parametro -m indica il modulo '{module}'; non ne verifica l’esecuzione corrente.";
            string role = module.StartsWith("ipykernel", StringComparison.OrdinalIgnoreCase) ? "Kernel Python di notebook" :
                module.StartsWith("pip", StringComparison.OrdinalIgnoreCase) ? "Gestione pacchetti Python" :
                "Modulo Python";
            return R(role, $"Parametro riconosciuto: python -m {module}", meaning,
                p.ParentName is null ? $"Modulo indicato nei parametri: {module}; esecuzione corrente non verificata." : $"Modulo indicato: {module}; padre riportato: {p.ParentName}. Motivo dell’avvio sconosciuto.",
                "La chiusura interrompe il modulo e le attività che gestisce.", "BASSO / MEDIO", "Sviluppo", 98,
                $"-m {module} nella command line");
        }
        // -c executes inline code. Do not reproduce source text: it can contain secrets.
        if (Regex.IsMatch(cmd, @"(?:^|\s)-c(?:\s|$)", RegexOptions.IgnoreCase))
            return R("Codice Python inline", "Parametro -c rilevato per Python",
                "L’opzione -c indica codice fornito nei parametri di avvio. Non leggiamo il contenuto come spiegazione per evitare informazioni sensibili.",
                p.ParentName is null ? "Avvio tramite comando Python inline." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.",
                "Interrompe l'esecuzione di quelle istruzioni.", "MEDIO", "Sviluppo", 96, "-c nella command line");

        var script = Regex.Match(cmd, "(?:\"([^\"]+\\.py)\"|(\\S+\\.py))", RegexOptions.IgnoreCase);
        if (script.Success)
        {
            string path = script.Groups[1].Success ? script.Groups[1].Value : script.Groups[2].Value;
            string filename = FileNameForDisplay(path);
            string entry = $"Script: {filename}";
            if (p.IsDeepInspected && p.TcpListeningCount > 0)
                entry += $"; TCP LISTEN: {string.Join(", ", p.ListeningTcpPorts)}";
            return R("Script Python", $"Script indicato: {filename}",
                $"Nei parametri compare lo script '{filename}'. Il nome del file non prova quale funzione del codice sia attiva ora.",
                p.ParentName is null ? $"Script indicato nei parametri: {filename}; esecuzione corrente non verificata." : $"Script indicato: {filename}; padre riportato: {p.ParentName}. Motivo dell’avvio sconosciuto.",
                "La chiusura interrompe lo script e i suoi eventuali task attivi.", "BASSO / MEDIO", "Sviluppo", 98,
                $"{entry}; percorso dichiarato: {path}");
        }
        if (p.IsDeepInspected && p.TcpListeningCount > 0)
            return R("Processo Python in ascolto", $"Python ha {p.TcpListeningCount} porta/e TCP in ascolto" +
                (p.ListeningTcpPorts.Count > 0 ? $": {string.Join(", ", p.ListeningTcpPorts)}" : string.Empty),
                "Una socket TCP LISTEN è attribuita direttamente a questo PID. Non prova di per sé che sia un server HTTP.",
                "Windows rileva socket TCP LISTEN; richieste e byte trasferiti non sono misurati.", "La chiusura interrompe l’ascolto e le eventuali attività del processo.", "MEDIO", "Sviluppo", 92,
                "runtime Python + porte TCP LISTEN osservate");
        if (p.IsDeepInspected && p.HasVisibleWindow)
            return R("Applicazione Python con interfaccia", "Processo Python con finestra visibile",
                "Windows associa una finestra top-level al PID Python; il toolkit grafico non è necessariamente determinabile.",
                "Windows associa una finestra visibile al processo Python; il compito del codice non è rilevato.", "Può chiudere l’app Python associata.", "MEDIO", "Sviluppo", 80,
                "runtime Python + finestra visibile");
        return R("Interprete Python", "Python in esecuzione: attività interna non osservabile direttamente",
            "Senza script/modulo leggibile o telemetria specifica il ruolo del codice non è determinabile.",
            p.ParentName is null ? "Origine sconosciuta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.",
            "Interrompe il processo e le operazioni che eventualmente esegue.", "DA VERIFICARE", "Sviluppo", 55,
            "runtime Python identificato; attività applicativa non esposta");
    }

    private static RoleAssessment ClassifyNode(ProcessSnapshot p, string cmd, OwnershipAssessment owner)
    {
        foreach (var pair in new[] { ("tsserver", "TypeScript language server"), ("eslint", "Analizzatore ESLint"), ("vite", "Dev server Vite"), ("next", "Runtime/dev server Next.js") })
            if (cmd.Contains(pair.Item1, StringComparison.OrdinalIgnoreCase))
                return R(pair.Item2, $"Testo riconosciuto nei parametri: {pair.Item1}", "Il ruolo è suggerito da una corrispondenza testuale nei parametri Node.js; un nome presente fra gli argomenti può essere un falso positivo.", "Nome di tool nei parametri Node.js; chi ha richiesto l’avvio non è identificato.", "Interrompe il tool o server associato.", "BASSO / MEDIO", "Sviluppo", 94, $"'{pair.Item1}' nella command line");
        var js = Regex.Match(cmd, "(?:\"([^\"]+\\.(?:js|cjs|mjs))\"|(\\S+\\.(?:js|cjs|mjs)))", RegexOptions.IgnoreCase);
        if (js.Success)
        {
            string path = js.Groups[1].Success ? js.Groups[1].Value : js.Groups[2].Value;
            return R("Applicazione Node.js", $"File indicato nei parametri: {FileNameForDisplay(path)}", "Nei parametri Node.js compare un file JavaScript; il testo non verifica il lavoro corrente.", $"La riga di comando indica come punto di ingresso {FileNameForDisplay(path)}; l’operazione corrente non è stata misurata.", "Interrompe l'app/tool Node.js associato.", "BASSO / MEDIO", "Sviluppo", 94, $"entrypoint JavaScript: {path}");
        }
        if (p.IsDeepInspected && p.TcpListeningCount > 0)
            return R("Server Node.js", $"Node.js ascolta su {p.TcpListeningCount} porta/e TCP" + (p.ListeningTcpPorts.Count > 0 ? $": {string.Join(", ", p.ListeningTcpPorts)}" : string.Empty),
                "Windows rileva socket TCP nello stato LISTEN attribuite al PID Node.js. Non sono misurate richieste accettate, risposte o byte trasferiti.",
                "Il PID ha socket TCP in ascolto; questo non dimostra che abbia elaborato richieste.", "Può interrompere l’ascolto Node.js e le eventuali attività del processo; richieste in corso non rilevate.", "MEDIO", "Sviluppo", 90,
                "runtime Node.js + socket TCP LISTEN osservate");
        return R("Runtime Node.js", "Node.js attivo; compito specifico non esposto", "Il processo Node.js è identificato; l’entrypoint applicativo non è disponibile.", p.ParentName is null ? "Origine sconosciuta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.", "Interrompe il processo Node.js associato.", "DA VERIFICARE", "Sviluppo", p.IsDeepInspected ? 62 : 55, p.IsDeepInspected ? "deep inspection senza entrypoint o comportamento forte" : "runtime Node.js senza entrypoint riconoscibile");
    }

    private static RoleAssessment ClassifyJava(ProcessSnapshot p, string cmd, OwnershipAssessment owner)
    {
        var jar = Regex.Match(cmd, "-jar\\s+(?:\"([^\"]+\\.jar)\"|(\\S+\\.jar))", RegexOptions.IgnoreCase);
        if (jar.Success)
        {
            string path = jar.Groups[1].Success ? jar.Groups[1].Value : jar.Groups[2].Value;
            return R("Applicazione Java", $"File indicato nei parametri: {FileNameForDisplay(path)}", "Il parametro -jar indica un file JAR; il lavoro corrente non è verificato.", $"File JAR indicato nei parametri: {FileNameForDisplay(path)}; motivo dell’avvio sconosciuto.", "Interrompe l'app Java associata.", "BASSO / MEDIO", "Sviluppo", 98, $"-jar {path}");
        }
        if (p.IsDeepInspected && p.TcpListeningCount > 0)
            return R("Servizio/server Java", $"JVM in ascolto su {p.TcpListeningCount} porta/e TCP",
                "Il classificatore suggerisce Java/JVM; Windows rileva socket TCP LISTEN attribuite al PID.",
                "Windows rileva socket TCP LISTEN; richieste elaborate non misurate.", "Interrompe il servizio Java e le connessioni servite.", "MEDIO", "Sviluppo", 88,
                "JVM + socket TCP LISTEN osservate");
        if (p.IsDeepInspected && p.HasVisibleWindow)
            return R("Applicazione desktop Java", "JVM con finestra visibile",
                "Windows associa una finestra top-level alla JVM; il toolkit/app specifico non è deducibile senza ulteriori metadati.",
                "Windows associa una finestra visibile alla JVM; motivo dell’avvio sconosciuto.", "Può chiudere l’applicazione o finestra Java associata.", "MEDIO", "Applicazioni", 80,
                "JVM + finestra top-level visibile");
        return R("Java Virtual Machine", "JVM attiva; applicazione specifica non esposta", "La JVM è identificata, ma non è presente un JAR esplicito.", p.ParentName is null ? "Origine sconosciuta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.", "Interrompe l'app Java associata.", "DA VERIFICARE", "Sviluppo", p.IsDeepInspected ? 64 : 58, p.IsDeepInspected ? "deep inspection senza JAR o comportamento forte" : "runtime Java senza JAR esplicito");
    }

    private static RoleAssessment ClassifyPowerShell(ProcessSnapshot p, string cmd, OwnershipAssessment owner)
    {
        var ps = Regex.Match(cmd, "(?:-File\\s+)?(?:\"([^\"]+\\.ps1)\"|(\\S+\\.ps1))", RegexOptions.IgnoreCase);
        if (ps.Success)
        {
            string path = ps.Groups[1].Success ? ps.Groups[1].Value : ps.Groups[2].Value;
            return R("Script PowerShell", $"File indicato nei parametri: {FileNameForDisplay(path)}", "Nei parametri PowerShell compare un file .ps1; il lavoro corrente non è verificato.", $"La riga di comando indica come punto di ingresso {FileNameForDisplay(path)}; l’operazione corrente non è stata misurata.", "Interrompe lo script e i comandi correnti.", "BASSO / MEDIO", "Sviluppo", 98, $"script PowerShell: {path}");
        }
        return R("Shell PowerShell", "Sessione PowerShell", "È una shell/ambiente di automazione PowerShell.", p.ParentName is null ? "Sessione PowerShell aperta." : $"Padre: {p.ParentName}. Autore e motivo dell’avvio sconosciuti.", "Chiude la sessione e gli eventuali comandi in esecuzione.", "BASSO / MEDIO", "Sviluppo", 90, "eseguibile PowerShell");
    }

    private static RoleAssessment? ClassifyByGenericNameSignals(ProcessSnapshot p, OwnershipAssessment owner, FrameworkAssessment fw)
    {
        string cleanExe = Path.GetFileNameWithoutExtension(p.Name).ToLowerInvariant();
        switch (cleanExe)
        {
            case "processlens.memoryagent":
                return R("Agente avvisi RAM ProcessLens", "Monitora la soglia RAM (85% / 80%) nella sessione utente",
                    "Controlla periodicamente la pressione della RAM fisica e mostra una notifica se supera l'85%.",
                    p.ParentName is null ? "Avviato nella sessione utente per il monitoraggio della RAM." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Chiude gli avvisi automatici sulla soglia RAM finché non viene riavviato.", "BASSO", "Background", 98,
                    "eseguibile ProcessLens.MemoryAgent riconosciuto");
            case "processlens.guardian.service":
                return R("Servizio diagnostico ProcessLens Guardian", "Raccoglie telemetria ETW locale su avvii e chiusure processi",
                    "Servizio locale di ProcessLens per registrare la cronologia di avvio dei processi e l'uso risorse.",
                    "Servizio in background associato a ProcessLens Guardian; motivo puntuale non osservato.",
                    "Interrompe la raccolta storica ETW di Guardian.", "BASSO / MEDIO", "Background", 98,
                    "eseguibile ProcessLens.Guardian.Service riconosciuto");
            case "processlensprofiler":
                return R("Profiler heap .NET di ProcessLens", "Acquisisce su richiesta i tipi di oggetti nell'heap .NET",
                    "Strumento esterno on-demand avviato da ProcessLens per ispezionare l'heap .NET di un PID selezionato.",
                    p.ParentName is null ? "Avviato su richiesta per profilazione .NET." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Annulla la sessione di profilazione heap .NET in corso.", "BASSO", "Sviluppo", 98,
                    "eseguibile ProcessLensProfiler riconosciuto");
            case "conhost" or "openconsole":
                return R("Host finestra console di Windows", "Gestisce finestra, buffer di testo e I/O per programmi a riga di comando",
                    "Windows affianca conhost.exe a ogni applicazione console (PowerShell, CMD, .NET, Python) per gestire l'input/output testuale.",
                    p.ParentName is null ? "Associato a una sessione console; processo originario non determinato." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Può chiudere la finestra terminale o interrompere il programma console collegato.", "MEDIO", "Windows", 96,
                    "eseguibile console host di Windows (conhost/openconsole)");
            case "dwm":
                return R("Gestione finestre desktop (DWM)", "Compone e disegna le finestre del desktop tramite GPU",
                    "Desktop Window Manager gestisce la composizione grafica di tutte le finestre di Windows.",
                    "Componente grafico di sessione di Windows; motivo dell’avvio sconosciuto.",
                    "Sconsigliato terminarlo: causa il riavvio dello schermo e possibili blocchi grafici.", "ALTO", "Windows", 98,
                    "eseguibile Desktop Window Manager (dwm.exe)");
            case "explorer":
                return R("Esplora risorse e shell di Windows", "Gestisce desktop, barra delle applicazioni e finestre cartelle",
                    "Fornisce l'interfaccia grafica principale di Windows (taskbar, menu Start, Esplora file).",
                    "Shell grafica della sessione utente Windows; motivo dell’avvio sconosciuto.",
                    "Scompaiono temporaneamente barra delle applicazioni e cartelle aperte.", "MEDIO", "Windows", 98,
                    "eseguibile shell Windows (explorer.exe)");
            case "sihost":
                return R("Host infrastruttura shell (sihost)", "Supporta menu Start, centro notifiche e funzioni moderne della shell",
                    "Shell Infrastructure Host coordina componenti moderni dell'interfaccia utente di Windows.",
                    "Componente di sessione dell'interfaccia Windows; motivo dell’avvio sconosciuto.",
                    "Può bloccare menu Start, barra delle applicazioni e scorciatoie di sistema.", "ALTO", "Windows", 96,
                    "eseguibile Shell Infrastructure Host (sihost.exe)");
            case "taskhostw":
                return R("Host attività pianificate di Windows", "Ospita attività di manutenzione e task di sistema pianificati",
                    "Esegue in background task registrati nell'Utilità di pianificazione di Windows.",
                    "Ospita task pianificati di Windows; il singolo task attivo non è esposto nel nome.",
                    "Interrompe le attività pianificate attualmente ospitate in questo processo.", "MEDIO", "Windows", 96,
                    "eseguibile Task Host Windows (taskhostw.exe)");
            case "prevhost" or "smartscreen":
                var reference = WindowsHostReference.Find(p.Name)!;
                return R(reference.Title + " (dal nome)", "Dal nome: " + reference.Title,
                    reference.Summary + " Funzione tipica dal nome; identità e attività corrente non verificate.",
                    "Motivo dell’avvio sconosciuto.",
                    "La chiusura può interrompere il componente ospitato; l’operazione corrente non è identificata.",
                    "DA VERIFICARE", p.ExecutablePath is { } hostPath && SystemProcessPresentation.UnderWindows(hostPath, Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ? "Windows" : "Background", 75,
                    $"nome eseguibile: {p.Name}; riferimento Microsoft: {reference.Source}");
            case "runtimebroker":
                return R("Runtime Broker di Windows", "Media permessi e risorse di sistema per le app pacchettizzate",
                    "Gestisce le autorizzazioni e i contratti di sistema per le applicazioni Windows/UWP.",
                    "Avviato da Windows per mediare i permessi delle app; l'app specifica non è indicata nel nome.",
                    "Le app Windows collegate possono chiudersi o perdere accesso alle risorse.", "BASSO / MEDIO", "Windows", 96,
                    "eseguibile Runtime Broker (RuntimeBroker.exe)");
            case "searchhost" or "searchapp":
                return R("Interfaccia di ricerca Windows", "Gestisce il pannello di ricerca di Windows e i risultati rapidi",
                    "Fornisce l'interfaccia di ricerca nella barra delle applicazioni di Windows.",
                    "Componente di ricerca della shell Windows; rimane pronto o in sospensione in memoria.",
                    "Chiude il pannello di ricerca; Windows lo ricrea automaticamente alla successiva ricerca.", "BASSO", "Windows", 96,
                    "eseguibile ricerca Windows (SearchHost/SearchApp)");
            case "searchindexer" or "searchprotocolhost" or "searchfilterhost":
                return R("Indicizzatore di Windows Search", "Indicizza file e contenuti per la ricerca rapida locale",
                    "Aggiorna e interroga il catalogo di ricerca di Windows.",
                    "Servizio/worker di indicizzazione di Windows Search.",
                    "Interrompe temporaneamente l'indicizzazione o le ricerche sui file.", "BASSO / MEDIO", "Background", 96,
                    "componente indicizzazione Windows Search");
            case "msmpeng" or "mpdefendercoreservice" or "securityhealthservice" or "securityhealthsystray":
                return R("Sicurezza di Windows / Microsoft Defender", "Protezione antivirus e controllo sicurezza di sistema",
                    "Componente di sicurezza e scansione antimalware integrato in Windows.",
                    "Servizio o interfaccia di sicurezza di Windows; operazione puntuale non osservata.",
                    "Protetto dal sistema o sconsigliato da chiudere per non ridurre la protezione.", "ALTO", "Windows", 98,
                    "componente Microsoft Defender / Sicurezza di Windows");
            case "ctfmon" or "textinputhost":
                return R("Servizio input di testo e tastiera", "Gestisce layout tastiera, pannello emoji, dettatura e metodi di input",
                    "Coordina l'inserimento del testo e i servizi linguistici nelle applicazioni Windows.",
                    "Componente di input della sessione utente Windows.",
                    "Può impedire la digitazione nelle app moderne o nel menu Start finché non si riavvia.", "MEDIO", "Windows", 95,
                    "componente input testo Windows (ctfmon/TextInputHost)");
            case "dllhost":
                return R("Host componenti COM (dllhost)", "Ospita librerie COM o generatori di anteprime in un processo isolato",
                    "COM Surrogate carica DLL e componenti COM fuori dal processo chiamante per isolarne la memoria.",
                    p.ParentName is null ? "Ospita un componente COM su richiesta." : $"Padre riportato: {p.ParentName}; il CLSID specifico richiede ispezione dei parametri.",
                    "Interrompe l'operazione COM o la generazione di miniature in corso.", "BASSO / MEDIO", "Windows", 94,
                    "eseguibile COM Surrogate (dllhost.exe)");
            case "audiodg":
                return R("Motore audio di Windows (audiodg)", "Elaborazione del segnale audio ed effetti sonori in processo isolato",
                    "Windows Audio Device Graph Isolation elabora i flussi audio separatamente dai programmi.",
                    "Attivato dal sottosistema audio di Windows.",
                    "Interrompe temporaneamente la riproduzione o acquisizione audio.", "MEDIO", "Windows", 96,
                    "eseguibile Windows Audio Device Graph Isolation (audiodg.exe)");
            case "wmiprvse":
                return R("Provider WMI di Windows", "Risponde alle interrogazioni di diagnostica e gestione WMI",
                    "Esegue le query WMI richieste da strumenti di monitoraggio, script o servizi di sistema.",
                    "Avviato dal servizio WMI quando un programma interroga i dati di gestione di Windows.",
                    "Può far fallire le query WMI in corso; Windows lo riavvia se necessario.", "BASSO / MEDIO", "Background", 95,
                    "eseguibile WMI Provider Host (WmiPrvSE.exe)");
            case "spoolsv":
                return R("Spooler di stampa Windows", "Gestisce code di stampa e comunicazione con le stampanti",
                    "Servizio di sistema per la gestione dei processi di stampa locali e di rete.",
                    "Servizio di stampa di Windows.",
                    "Blocca le stampe fino al riavvio del servizio Spooler.", "BASSO / MEDIO", "Background", 96,
                    "eseguibile Spooler di stampa (spoolsv.exe)");
            case "fontdrvhost":
                return R("Host driver caratteri (fontdrvhost)", "Renderizza i caratteri tipografici in modalità utente isolata",
                    "Usermode Font Driver Host elabora i font fuori dal kernel per maggiore sicurezza.",
                    "Componente grafico di sistema avviato da Windows.",
                    "Non terminare: può compromettere la visualizzazione del testo nelle applicazioni.", "ALTO", "Windows", 96,
                    "eseguibile Usermode Font Driver Host (fontdrvhost.exe)");
        }

        string signal = $"{p.Name} {p.FileDescription} {p.ProductName}".ToLowerInvariant();

        if (signal.Contains("language server") || signal.Contains("language_server") || signal.Contains("languageserver"))
            return R("Language server", "Possibile componente language server",
                "Il nome o i metadati del processo dichiarano un language server: tipicamente fornisce completamento, diagnostica, simboli e navigazione del codice.",
                p.ParentName is null ? "Il nome o i metadati suggeriscono un componente di supporto a editor/IDE; il processo che ha richiesto l’avvio non è noto." : $"Padre riportato da Windows: {p.ParentName}; motivo dell’avvio sconosciuto.",
                "L'editor può perdere temporaneamente completamento, diagnostica e navigazione finché il server non viene riavviato.", "BASSO / MEDIO", "Sviluppo", 92,
                "segnale 'language server' nel nome/metadati del processo");

        if (signal.Contains("crashpad") || signal.Contains("crash handler") || signal.Contains("crashreport"))
            return R("Gestore crash", "Possibile componente per rapporti di crash",
                "Il nome o i metadati indicano un componente dedicato alla gestione degli arresti anomali.",
                "Nome o metadati compatibili con gestione crash; stato interno e motivo dell’avvio non osservati.",
                "L’applicazione può continuare a funzionare, ma potrebbe non registrare gli arresti anomali.", "BASSO", CategoryFor(p, fw), 90,
                "segnale crash handler nei metadati/nome");

        if (signal.Contains("updater") || signal.Contains("update service") || signal.Contains("update helper"))
            return R("Aggiornamento software", "Possibile componente di aggiornamento software",
                "Il nome/metadati indicano una funzione di aggiornamento software.",
                "Nome o metadati compatibili con aggiornamenti; operazioni in corso e motivo dell’avvio non osservati.",
                "Può interrompere un controllo o aggiornamento in corso; il riavvio dipende dalla configurazione dell’applicazione.", "BASSO", "Background", 82,
                "segnale updater/update service nei metadati/nome");

        return null;
    }

    private static bool HasAnyModule(ProcessSnapshot p, params string[] names) =>
        p.ModuleNames.Any(module => names.Any(name => module.Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static string CategoryFor(ProcessSnapshot p, FrameworkAssessment fw)
    {
        if (fw.Name == "WSL") return "WSL";
        if (fw.Name is ".NET" or "Python" or "Node.js" or "Java/JVM" or "PowerShell") return "Sviluppo";
        if (p.ServiceNames.Count > 0) return "Background";
        if (p.ExecutablePath?.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase) == true) return "Windows";
        if (string.Equals(p.ParentName, p.Name, StringComparison.OrdinalIgnoreCase)) return "Background";
        return "Applicazioni";
    }

    private static string? Opt(string cmd, string option)
    {
        var m = Regex.Match(cmd, Regex.Escape(option) + "=(?:\"([^\"]+)\"|([^\\s]+))", RegexOptions.IgnoreCase);
        return m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : null;
    }

    private static bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static string UtilityRole(string? value)
    {
        string s = value ?? string.Empty;
        if (s.Contains("Network", StringComparison.OrdinalIgnoreCase)) return "Servizio di rete";
        if (s.Contains("Audio", StringComparison.OrdinalIgnoreCase)) return "Servizio audio";
        if (s.Contains("Storage", StringComparison.OrdinalIgnoreCase)) return "Servizio storage";
        if (s.Contains("Video", StringComparison.OrdinalIgnoreCase)) return "Servizio video";
        if (s.Contains("NodeService", StringComparison.OrdinalIgnoreCase) || s.Contains("Node", StringComparison.OrdinalIgnoreCase)) return "Processo ausiliario Node.js";
        if (s.Contains("DataDecoder", StringComparison.OrdinalIgnoreCase)) return "Servizio decodifica dati";
        if (s.Contains("ProxyResolver", StringComparison.OrdinalIgnoreCase)) return "Risolutore proxy";
        if (s.Contains("Tracing", StringComparison.OrdinalIgnoreCase)) return "Servizio tracing/diagnostica";
        if (s.Contains("Media", StringComparison.OrdinalIgnoreCase)) return "Servizio multimediale";
        return "Processo ausiliario";
    }
    private static string UtilitySummary(string? value) => UtilityRole(value) switch
    {
        "Servizio di rete" => "Parametri: possibile componente di rete",
        "Servizio audio" => "Parametri: possibile componente audio",
        "Servizio storage" => "Parametri: possibile componente storage",
        "Servizio video" => "Parametri: possibile componente video",
        "Processo ausiliario Node.js" => "Parametri: possibile processo ausiliario Node.js",
        "Servizio decodifica dati" => "Parametri: possibile componente di decodifica",
        "Risolutore proxy" => "Parametri: possibile risolutore proxy",
        "Servizio tracing/diagnostica" => "Parametri: possibile componente diagnostico",
        "Servizio multimediale" => "Parametri: possibile componente multimediale",
        _ => "Parametri: possibile processo ausiliario"
    };

    private static RoleAssessment R(string role, string summary, string meaning, string why, string impact, string risk, string category, int confidence, string evidence) =>
        new(role, summary, meaning, why, impact, risk, category, confidence, evidence);

    [GeneratedRegex("(?:^|\\s)(?:\"([^\"]+\\.dll)\"|(\\S+\\.dll))(?:\\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex DllRegex();
}
