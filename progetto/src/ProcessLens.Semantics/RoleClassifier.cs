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

        if (!string.IsNullOrWhiteSpace(p.FileDescription) &&
            !p.FileDescription.Equals(p.Name, StringComparison.OrdinalIgnoreCase) &&
            !p.FileDescription.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            p.FileDescription.Trim().Length > 2)
        {
            string desc = p.FileDescription.Trim();
            string category = CategoryFor(p, fw);
            string impact = p.IsDeepInspected && p.HasVisibleWindow
                ? "Può chiudere la finestra e le funzionalità dell'applicazione."
                : "La chiusura interrompe le funzionalità fornite da questo componente.";
            return R(desc, $"{desc} ({owner.Owner})",
                $"Eseguibile identificato dalla descrizione file: '{desc}'. Componente di {owner.Owner}.",
                p.ParentName is null ? $"Componente associato a {owner.Owner}; motivo dell’avvio non osservato." : $"Padre riportato: {p.ParentName}. Componente associato a {owner.Owner}.",
                impact, "MEDIO", category, 88,
                $"descrizione file eseguibile: '{desc}'; editore={owner.Owner}");
        }

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
        string cmd = p.CommandLine ?? string.Empty;

        switch (cleanExe)
        {
            case "system":
                return R("Kernel NT e sottosistema di sistema", "Componente principale di Windows (PID 4)",
                    "È il processo del kernel NT di Windows. Gestisce thread in modalità kernel, driver di dispositivo e allocazione della memoria fisica.",
                    "Avviato dal boot loader di Windows all'accensione del computer.",
                    "Non può e non deve essere terminato: è il nucleo operativo dell'intero sistema Windows.", "CRITICO", "Windows", 100,
                    "processo kernel NT di sistema (PID 4)");

            case "smss":
                return R("Sottosistema gestione sessioni (smss)", "Inizializza sessioni utente, memoria virtuale e sottosistema Win32",
                    "Session Manager Subsystem è il primo processo utente avviato dal kernel. Avvia csrss e winlogon.",
                    "Processo fondamentale di boot del sistema operativo.",
                    "Non terminare: la terminazione causa l'immediata schermata blu (BSOD) e riavvio del PC.", "CRITICO", "Windows", 100,
                    "eseguibile Session Manager Subsystem (smss.exe)");

            case "csrss":
                return R("Sottosistema di runtime Client/Server (csrss)", "Gestisce finestre di console, processi e thread utente",
                    "Client/Server Runtime Subsystem è il componente utente critico di Win32. Mantiene le tabelle di processi e le finestre di console.",
                    "Componente critico di ogni sessione attiva di Windows.",
                    "Non terminare: provoca l'arresto anomalo immediato del sistema.", "CRITICO", "Windows", 100,
                    "eseguibile Client/Server Runtime Subsystem (csrss.exe)");

            case "wininit":
                return R("Inizializzazione di sistema (wininit)", "Avvia il gestore servizi SCM, LSASS e il gestore sessioni locali",
                    "Windows Initialization Process coordina i servizi di base di Windows avviati nella sessione 0.",
                    "Avviato all'avvio del sistema operativo.",
                    "Non terminare: causa l'arresto anomalo del sistema operativo.", "CRITICO", "Windows", 100,
                    "eseguibile Windows Initialization (wininit.exe)");

            case "services":
                return R("Gestore controllo servizi (SCM)", "Avvia, arresta e monitora tutti i servizi e driver di Windows",
                    "Service Control Manager (services.exe) è il coordinatore centrale di tutti i servizi Windows registrati.",
                    "Componente fondamentale di Windows per l'orchestrazione dei servizi in background.",
                    "Non terminare: interrompe la gestione di tutti i servizi di rete e di sistema.", "CRITICO", "Windows", 100,
                    "eseguibile Service Control Manager (services.exe)");

            case "lsass":
                return R("Sottosistema di sicurezza locale (LSASS)", "Autenticazione utenti, token di sicurezza e policy di accesso",
                    "Local Security Authority Subsystem Service verifica le credenziali di accesso, genera token di sicurezza e applica i criteri locali.",
                    "Componente centrale di sicurezza e autenticazione di Windows.",
                    "Non terminare: provoca la disconnessione immediata o il riavvio del computer.", "CRITICO", "Windows", 100,
                    "eseguibile Local Security Authority (lsass.exe)");

            case "lsaiso":
                return R("Isolamento credenziali e sicurezza VBS (LSAISO)", "Protegge chiavi e token di autenticazione con isolamento hardware",
                    "Credential Guard isola i processi LSA in un ambiente virtualizzato sicuro per impedire il furto delle credenziali in memoria.",
                    "Abilitato dai criteri di sicurezza basati su virtualizzazione (VBS).",
                    "Non terminare: protetto dall'hypervisor; essenziale per la sicurezza delle credenziali.", "CRITICO", "Windows", 100,
                    "eseguibile LSA Isolation / Credential Guard (lsaiso.exe)");

            case "winlogon":
                return R("Applicazione di accesso a Windows (winlogon)", "Gestisce schermata di blocco, Ctrl+Alt+Canc e sessione utente",
                    "Windows Logon Application gestisce l'accesso interattivo, il blocco del desktop e il caricamento del profilo utente.",
                    "Processo fondamentale della sessione di accesso interattiva.",
                    "Non terminare: provoca la disconnessione della sessione utente attiva.", "CRITICO", "Windows", 100,
                    "eseguibile Windows Logon Application (winlogon.exe)");

            case "svchost":
                var svcGroupMatch = Regex.Match(cmd, @"(?i)(?:^|\s)-k\s+([a-zA-Z0-9_.\-]{1,64})");
                var svcSingleMatch = Regex.Match(cmd, @"(?i)(?:^|\s)-s\s+([a-zA-Z0-9_.\-]{1,64})");
                string? svcGroup = svcGroupMatch.Success ? svcGroupMatch.Groups[1].Value : null;
                string? svcSingle = svcSingleMatch.Success ? svcSingleMatch.Groups[1].Value : null;
                string svcSugg = cmd.Contains("-k netsvcs", StringComparison.OrdinalIgnoreCase)
                    ? "Host servizi di rete (svchost · netsvcs)"
                    : cmd.Contains("-k localservice", StringComparison.OrdinalIgnoreCase)
                        ? "Host servizi locali (svchost · localService)"
                        : cmd.Contains("-k networkservice", StringComparison.OrdinalIgnoreCase)
                            ? "Host servizi di rete locali (svchost · networkService)"
                            : cmd.Contains("-k dcomlaunch", StringComparison.OrdinalIgnoreCase)
                                ? "Host avvio DCOM (svchost · DcomLaunch)"
                                : cmd.Contains("-k appmodel", StringComparison.OrdinalIgnoreCase)
                                    ? "Host modello app Windows (svchost · AppModel)"
                                    : cmd.Contains("-k utcsvc", StringComparison.OrdinalIgnoreCase)
                                        ? "Host telemetria Windows (svchost · UtcSvc)"
                                        : svcSingle is not null
                                            ? $"Host servizio Windows (svchost · {svcSingle})"
                                            : svcGroup is not null
                                                ? $"Host servizi Windows (svchost · {svcGroup})"
                                                : "Host condiviso servizi Windows (svchost)";
                string svcDesc = p.ServiceDisplayNames.Count > 0
                    ? $"Ospita: {string.Join(", ", p.ServiceDisplayNames)}"
                    : svcSingle is not null
                        ? $"Parametro servizio: -s {svcSingle}"
                        : svcGroup is not null
                            ? $"Gruppo di servizi: -k {svcGroup}"
                            : "Ospita servizi DLL di sistema raggruppati per ottimizzare la memoria";
                return R(svcSugg, svcDesc,
                    "Processo generico host di Windows per l'esecuzione di servizi implementati in librerie dinamiche DLL.",
                    p.ParentName is null ? "Gestito dal Service Control Manager (services.exe)." : $"Padre: {p.ParentName}.",
                    "La terminazione può interrompere i servizi di sistema ospitati all'interno.", "ALTO", "Windows", 98,
                    "eseguibile host servizi Windows (svchost.exe)");

            case "registry":
                return R("Sottosistema del Registro di Windows", "Gestione in memoria degli hive e ottimizzazione chiavi di sistema",
                    "Mantiene in memoria gli hive del Registro di sistema per accelerare le letture e scritture fuori dallo spazio kernel.",
                    "Componente architetturale di sistema di Windows 10/11.",
                    "Non terminare: è protetto dal sistema operativo.", "CRITICO", "Windows", 100,
                    "processo architettura Registro di sistema");

            case "shellexperiencehost":
                return R("Host esperienza Shell di Windows", "Gestisce elementi grafici moderni: flyout, orologio e notifiche",
                    "Coordina l'integrazione tra la shell desktop classica e i componenti visivi moderni di Windows.",
                    "Avviato dalla sessione grafica per arricchire l'interfaccia utente.",
                    "La barra delle applicazioni o i menu a comparsa potrebbero bloccarsi o riavviarsi.", "MEDIO", "Windows", 96,
                    "eseguibile ShellExperienceHost.exe riconosciuto");

            case "startmenuexperiencehost":
                return R("Host del Menu Start di Windows", "Renderizza il Menu Start, icone bloccate e collegamenti rapidi",
                    "Processo dedicato che disegna e anima il menu Start di Windows 10 e Windows 11.",
                    "Avviato all'accesso per consentire l'apertura istantanea del menu Start.",
                    "Il menu Start si chiude; Windows lo ricrea automaticamente al clic successivo.", "BASSO / MEDIO", "Windows", 98,
                    "eseguibile StartMenuExperienceHost.exe riconosciuto");

            case "applicationframehost":
                return R("Host cornice app moderne (UWP)", "Incornicia le app dello Store in finestre compatibili con il desktop",
                    "Fornisce la barra del titolo, i bordi e il ridimensionamento standard per le app Universal Windows Platform.",
                    "Avviato quando una o più app moderne sono aperte.",
                    "Può nascondere o chiudere la cornice delle app moderne aperte.", "BASSO / MEDIO", "Windows", 96,
                    "eseguibile ApplicationFrameHost.exe riconosciuto");

            case "taskmgr":
                return R("Gestione attività di Windows", "Monitoraggio processi, risorse di sistema, prestazioni e avvio",
                    "Strumento nativo di diagnostica e gestione processi di Windows.",
                    "Aperto dall'utente per ispezionare le prestazioni del computer.",
                    "Chiude la finestra di Gestione attività; nessun impatto sugli altri processi.", "BASSO", "Windows", 98,
                    "eseguibile Taskmgr.exe riconosciuto");

            case "cmd":
                return R("Prompt dei comandi di Windows", "Interprete di comandi batch e console a riga di comando tradizionale",
                    "Esegue script .bat/.cmd o comandi interattivi nella shell Win32 standard.",
                    p.ParentName is null ? "Aperto dall'utente o da un'applicazione." : $"Padre: {p.ParentName}.",
                    "Chiude la finestra di comando e interrompe gli script attualmente in esecuzione.", "BASSO / MEDIO", "Windows", 96,
                    "eseguibile Prompt dei comandi (cmd.exe)");

            case "wslhost" or "wslservice" or "vmmem" or "vmmemwsl":
                return R("Host sottosistema Linux WSL / Macchina virtuale", "Esegue il kernel Linux e gestisce risorse per distribuzioni WSL2",
                    "Windows Subsystem for Linux (WSL2) esegue distribuzioni Linux native su un hypervisor leggero Hyper-V.",
                    "Avviato quando una sessione Linux o Docker Desktop è attiva.",
                    "Chiude tutte le sessioni Linux attive, i server web locali o i container in esecuzione in WSL.", "MEDIO / ALTO", "WSL", 98,
                    "componente architettura WSL2/vmmem riconosciuto");

            case "vmwp" or "vmms":
                return R("Gestione macchine virtuali Hyper-V", "Processo di lavoro e coordinamento per macchine virtuali Hyper-V",
                    "Hyper-V Virtual Machine Management Service gestisce lo stato e la virtualizzazione hardware delle VM.",
                    "Avviato per gestire macchine virtuali, Sandbox di Windows o emulatori Android.",
                    "Interrompe o sospende la macchina virtuale associata.", "MEDIO / ALTO", "Windows", 97,
                    "componente Hyper-V riconosciuto");

            case "werfault" or "wermgr":
                return R("Segnalazione errori di Windows (WerFault)", "Cattura crash dump e raccoglie informazioni sugli arresti anomali",
                    "Windows Error Reporting si attiva quando un'applicazione si blocca in modo imprevisto.",
                    "Attivato automaticamente dal sistema in seguito all'arresto anomalo di un programma.",
                    "Interrompe la generazione del dump di memoria dell'errore; l'app terminata rimane chiusa.", "BASSO", "Windows", 96,
                    "componente Windows Error Reporting (WerFault.exe)");

            case "trustedinstaller" or "tiworker" or "mousocoreworker" or "usoclient":
                return R("Gestione aggiornamenti e moduli Windows", "Installa patch di sicurezza, pacchetti cumulativi e moduli di sistema",
                    "Windows Modules Installer e Update Orchestrator applicano modifiche ai file di sistema protetti di Windows.",
                    "Avviato durante la manutenzione o l'installazione di aggiornamenti Windows Update.",
                    "Sconsigliato terminarlo durante un aggiornamento: può causare pacchetti corrotti o lunghi rollback all'avvio.", "ALTO", "Windows", 98,
                    "componente Windows Update / TrustedInstaller");

            case "msiexec":
                return R("Motore di installazione Windows Installer (MSI)", "Esegue installazione, configurazione o rimozione pacchetti .msi",
                    "Servizio standard di Windows per l'installazione sicura di programmi conformi alle specifiche MSI.",
                    "Avviato per installare o aggiornare un'applicazione.",
                    "Interrompe l'installazione in corso, lasciando il programma parzialmente configurato.", "MEDIO / ALTO", "Windows", 97,
                    "eseguibile Windows Installer (msiexec.exe)");

            case "rundll32":
                return R("Esecutore di librerie DLL a 32/64 bit (rundll32)", "Esegue funzioni esportate da librerie DLL come processi autonomi",
                    "Strumento di sistema Windows per richiamare punti di ingresso all'interno di moduli dinamici.",
                    "Utilizzato da applet del Pannello di controllo, wizard o utilità di sistema.",
                    "Chiude l'operazione o la finestra di dialogo aperta tramite la DLL.", "BASSO / MEDIO", "Windows", 94,
                    "eseguibile rundll32.exe riconosciuto");

            case "chrome":
                if (!cmd.Contains("--type=", StringComparison.OrdinalIgnoreCase))
                    return R("Processo principale Google Chrome", "Coordina finestre, schede, estensioni e navigazione web",
                        "Google Chrome coordina l'interfaccia utente, memorizza la cronologia e gestisce l'accesso a rete e disco.",
                        "Browser web aperto dall'utente.",
                        "Terminarlo chiude tutte le finestre, schede e processi collegati di Google Chrome.", "MEDIO", "Applicazioni", 98,
                        "processo principale Google Chrome (chrome.exe)");
                break;

            case "msedge":
                if (!cmd.Contains("--type=", StringComparison.OrdinalIgnoreCase))
                    return R("Processo principale Microsoft Edge", "Coordina finestre, schede, navigazione e integrazione di sistema",
                        "Microsoft Edge coordina l'interfaccia del browser, i profili utente e i sottoprocessi dedicati alle schede.",
                        "Browser web aperto dall'utente o da un'applicazione integrata.",
                        "Terminarlo chiude il browser e tutte le schede aperte.", "MEDIO", "Applicazioni", 98,
                        "processo principale Microsoft Edge (msedge.exe)");
                break;

            case "firefox":
                if (cmd.Contains("-contentproc", StringComparison.OrdinalIgnoreCase))
                    return R("Processo contenuti web Firefox", "Elabora codice HTML, JavaScript e risorse web isolate in sandbox",
                        "Firefox isola ogni scheda o origine in processi contenuti separati per sicurezza e stabilità.",
                        "Avviato da Firefox per renderizzare le schede aperte.",
                        "La scheda associata mostrerà un errore di arresto anomalo e potrà essere ricaricata.", "BASSO / MEDIO", "Applicazioni", 98,
                        "processo contenuti Firefox (-contentproc)");
                return R("Processo principale Mozilla Firefox", "Coordina finestre, schede, segnalibri e navigazione del browser",
                    "Processo genitore di Mozilla Firefox con motore Gecko per la gestione sicura del browser.",
                    "Browser web aperto dall'utente.",
                    "Terminarlo chiude Firefox e tutte le schede web aperte.", "MEDIO", "Applicazioni", 98,
                    "eseguibile Mozilla Firefox (firefox.exe)");

            case "brave" or "opera" or "opera_gx" or "vivaldi" or "tor":
                return R($"Browser web {owner.Owner}", $"Coordina navigazione web e schede per {owner.Owner}",
                    $"Browser ottimizzato per navigazione web da {owner.Owner}.",
                    "Applicazione aperta dall'utente.",
                    "Terminarlo chiude la sessione di navigazione attiva.", "MEDIO", "Applicazioni", 96,
                    $"eseguibile browser {cleanExe}");

            case "code":
                if (cmd.Contains("--type=extensionHost", StringComparison.OrdinalIgnoreCase))
                    return R("Host estensioni Visual Studio Code", "Esegue estensioni, linters e integrazioni per l'editor",
                        "VS Code isola le estensioni di terze parti in questo processo per non bloccare l'interfaccia utente.",
                        "Avviato da Visual Studio Code per supportare i plugin attivi.",
                        "Le estensioni attive si riavviano o mostrano un avviso nell'editor.", "BASSO / MEDIO", "Sviluppo", 98,
                        "VS Code extensionHost riconosciuto");
                if (cmd.Contains("--type=watcherService", StringComparison.OrdinalIgnoreCase))
                    return R("Watcher del file system VS Code", "Monitora le modifiche ai file nel workspace per l'albero di progetto",
                        "Rileva file creati, modificati o eliminati nella cartella di lavoro aperta.",
                        "Avviato da Visual Studio Code per il tracciamento dei file del progetto.",
                        "L'esploratore file di VS Code potrebbe non rilevare modifiche esterne fino al riavvio.", "BASSO", "Sviluppo", 98,
                        "VS Code watcherService riconosciuto");
                if (!cmd.Contains("--type=", StringComparison.OrdinalIgnoreCase))
                    return R("Editor Visual Studio Code", "Ambiente di sviluppo leggero e gestione progetti con finestre di codice",
                        "Visual Studio Code coordina il workspace di sviluppo, l'interfaccia dell'editor e i terminali integrati.",
                        "Aperto dall'utente per modificare codice sorgente.",
                        "Terminarlo chiude la finestra dell'editor; i file non salvati sono protetti da hot-exit.", "MEDIO", "Sviluppo", 98,
                        "processo principale Visual Studio Code (code.exe)");
                break;

            case "devenv":
                return R("IDE Microsoft Visual Studio", "Ambiente di sviluppo completo per C++, C#, .NET e cloud",
                    "Visual Studio gestisce soluzioni complesse, compilatori, designer visuali e debugger avanzati.",
                    "Aperto dall'utente per lo sviluppo software.",
                    "Chiude l'IDE; le sessioni di compilazione o debug attive vengono interrotte.", "MEDIO", "Sviluppo", 98,
                    "Microsoft Visual Studio (devenv.exe)");

            case "git" or "git-lfs":
                return R("Controllo versione distribuito Git", "Esegue comandi di repository, tracciamento commit, branching o sincronizzazione",
                    "Strumento a riga di comando Git per la gestione della cronologia del codice sorgente.",
                    p.ParentName is null ? "Invocato da terminale o da un'estensione dell'editor." : $"Padre: {p.ParentName}.",
                    "Interrompe l'operazione git in corso (clone, fetch, commit, checkout).", "BASSO / MEDIO", "Sviluppo", 98,
                    "eseguibile Git riconosciuto");

            case "docker" or "dockerd" or "com.docker.backend" or "com.docker.proxy":
                return R("Virtualizzazione e container Docker", "Gestisce immagini, container attivi, volumi e reti virtuali Docker",
                    "Docker Desktop coordina la piattaforma container per lo sviluppo e l'esecuzione di ambienti isolati.",
                    "Avviato per eseguire container applicativi locali o stack di sviluppo.",
                    "Interrompe tutti i container Docker in esecuzione e i servizi mappati sulle porte locali.", "MEDIO / ALTO", "Sviluppo", 98,
                    "piattaforma Docker riconosciuta");

            case "cargo" or "rustc":
                return R("Compilatore e gestore pacchetti Rust", "Compila codice sorgente Rust, gestisce dipendenze crate e crea binari",
                    "Toolchain ufficiale di Rust per l'analisi del codice, il borrowing checker e la compilazione nativa.",
                    "Invocato da cargo build, cargo test o da un language server come rust-analyzer.",
                    "Interrompe la compilazione o l'esecuzione dei test Rust in corso.", "BASSO / MEDIO", "Sviluppo", 98,
                    "toolchain Rust (cargo/rustc)");

            case "go":
                return R("Compilatore e toolchain Go", "Compila, scarica moduli ed esegue pacchetti di codice sorgente Go",
                    "Strumento di sviluppo della toolchain del linguaggio Go (Golang).",
                    "Avviato per compilare, testare o eseguire programmi Go.",
                    "Interrompe la compilazione o l'esecuzione dell'applicazione Go.", "BASSO / MEDIO", "Sviluppo", 98,
                    "toolchain Go (go.exe)");

            case "npm" or "npx" or "yarn" or "pnpm":
                return R("Gestore pacchetti JavaScript", "Installa librerie npm, esegue script di build e avvia strumenti di sviluppo",
                    "Strumento di gestione pacchetti per Node.js per il download e l'esecuzione di dipendenze JavaScript/TypeScript.",
                    "Invocato per installare pacchetti o eseguire comandi del progetto web.",
                    "Interrompe l'installazione delle dipendenze o lo script di build corrente.", "BASSO / MEDIO", "Sviluppo", 97,
                    "package manager JavaScript riconosciuto");

            case "discord":
                return R("Client di comunicazione Discord", "Chat vocale, streaming video e messaggistica istantanea",
                    "Applicazione desktop per la connessione a server vocali e canali testuali Discord.",
                    "Applicazione aperta dall'utente.",
                    "Chiude Discord e disconnette dai canali vocali attivi.", "BASSO / MEDIO", "Applicazioni", 98,
                    "eseguibile Discord riconosciuto");

            case "slack":
                return R("Piattaforma di collaborazione Slack", "Messaggistica di gruppo, canali di lavoro e chiamate integrate",
                    "Client desktop per la comunicazione interna aziendale e gestione canali Slack.",
                    "Aperto dall'utente per la collaborazione di lavoro.",
                    "Chiude Slack; interrompe le notifiche e le chiamate in corso.", "BASSO / MEDIO", "Applicazioni", 98,
                    "eseguibile Slack riconosciuto");

            case "teams" or "ms-teams":
                return R("Client Microsoft Teams", "Riunioni online, videoconferenze e chat aziendale integrata",
                    "Piattaforma di collaborazione Microsoft Teams per videoconferenze e canali aziendali.",
                    "Aperto dall'utente o configurato per l'avvio automatico con Windows.",
                    "Chiude Microsoft Teams; interrompe le riunioni video o le chiamate attive.", "BASSO / MEDIO", "Applicazioni", 98,
                    "Microsoft Teams riconosciuto");

            case "telegram":
                return R("Client desktop Telegram", "Messaggistica istantanea sincronizzata, gruppi e canali",
                    "Applicazione nativa di messaggistica veloce e crittografata Telegram Desktop.",
                    "Aperto dall'utente per comunicare.",
                    "Chiude l'applicazione Telegram; sospende le notifiche dei messaggi.", "BASSO", "Applicazioni", 98,
                    "Telegram.exe riconosciuto");

            case "whatsapp":
                return R("Client desktop WhatsApp", "Messaggistica istantanea, chiamate e condivisione file sincronizzata",
                    "Applicazione desktop ufficiale WhatsApp per Windows.",
                    "Aperto dall'utente per la messaggistica.",
                    "Chiude WhatsApp e sospende le notifiche in arrivo.", "BASSO", "Applicazioni", 98,
                    "WhatsApp.exe riconosciuto");

            case "spotify":
                return R("Riproduttore musicale Spotify", "Streaming audio, podcast e gestione playlist musicali",
                    "Applicazione client desktop per lo streaming e la riproduzione di musica Spotify.",
                    "Aperto dall'utente per l'ascolto di musica o podcast.",
                    "Interrompe immediatamente la riproduzione musicale e chiude l'app.", "BASSO", "Applicazioni", 98,
                    "Spotify.exe riconosciuto");

            case "steam" or "steamwebhelper":
                return R("Piattaforma giochi Steam", "Libreria giochi, negozio digitale, download aggiornamenti e overlay",
                    "Client della piattaforma Steam di Valve per il download, la gestione e l'esecuzione di videogiochi.",
                    "Aperto per avviare giochi o attivo nell'area di notifica per gli aggiornamenti.",
                    "Terminarlo chiude Steam e interrompe i download di giochi in corso.", "BASSO / MEDIO", "Applicazioni", 98,
                    "Steam.exe riconosciuto");

            case "winword":
                return R("Microsoft Word", "Creazione e modifica di documenti di testo con formattazione avanzata",
                    "Applicazione di videoscrittura della suite Microsoft Office / 365.",
                    "Aperto dall'utente per redigere o consultare documenti di testo.",
                    "Chiude Microsoft Word; le modifiche non salvate potrebbero andare perse.", "MEDIO", "Applicazioni", 98,
                    "Microsoft Word (winword.exe)");

            case "excel":
                return R("Microsoft Excel", "Calcoli analitici, formule, tabelle pivot e grafici numerici",
                    "Applicazione per fogli elettronici e analisi di dati della suite Microsoft Office / 365.",
                    "Aperto dall'utente per lavorare su tabelle e dati numerici.",
                    "Chiude Excel; le modifiche non salvate ai fogli di calcolo potrebbero andare perse.", "MEDIO", "Applicazioni", 98,
                    "Microsoft Excel (excel.exe)");

            case "powerpnt":
                return R("Microsoft PowerPoint", "Creazione e visualizzazione di presentazioni multimediali",
                    "Applicazione per la creazione di diapositive della suite Microsoft Office / 365.",
                    "Aperto dall'utente per lavorare a una presentazione.",
                    "Chiude PowerPoint; le modifiche non salvate potrebbero andare perse.", "MEDIO", "Applicazioni", 98,
                    "Microsoft PowerPoint (powerpnt.exe)");

            case "acrobat" or "acrord32":
                return R("Adobe Acrobat / Reader", "Visualizzazione, compilazione moduli e modifica di file PDF",
                    "Applicazione standard per la lettura e gestione di documenti in formato Portable Document Format (PDF).",
                    "Aperto dall'utente per consultare o firmare un documento PDF.",
                    "Chiude il documento PDF visualizzato.", "BASSO", "Applicazioni", 98,
                    "Adobe Acrobat / Reader riconosciuto");

            case "notepad" or "notepad++":
                return R("Editor di testo", "Visualizzazione e modifica veloce di file di testo (.txt, configurazioni, codice)",
                    "Editor di testo leggero per appunti, file di log e configurazioni.",
                    "Aperto dall'utente per leggere o scrivere un file di testo.",
                    "Chiude l'editor; i file non salvati richiedono conferma prima della chiusura.", "BASSO", "Applicazioni", 98,
                    "Editor Notepad riconosciuto");

            case "onedrive":
                return R("Sincronizzazione Microsoft OneDrive", "Sincronizza file personali e cartelle di sistema con il cloud",
                    "Client di sincronizzazione nativo di Microsoft per il backup e l'accesso su richiesta ai file cloud.",
                    "Configurato per l'avvio automatico all'accesso per mantenere aggiornati i file locali.",
                    "Sospende la sincronizzazione dei file con il cloud Microsoft.", "BASSO / MEDIO", "Background", 98,
                    "Microsoft OneDrive (onedrive.exe)");

            case "dropbox":
                return R("Sincronizzazione cloud Dropbox", "Mantiene allineati i file della cartella Dropbox con il cloud",
                    "Client desktop Dropbox per la sincronizzazione e condivisione file.",
                    "Attivo in background per rilevare modifiche ai file nella cartella sincronizzata.",
                    "Sospende la sincronizzazione dei file con Dropbox.", "BASSO", "Background", 98,
                    "Dropbox.exe riconosciuto");

            case "powertoys" or "powertoys.runner" or "powertoys.powerlauncher":
                return R("Suite Microsoft PowerToys", "Fornisce scorciatoie, ancoraggio finestre FancyZones e utilità avanzate",
                    "Raccolta di utilità sviluppate da Microsoft per ottimizzare l'esperienza utente su Windows.",
                    "Eseguito in background per gestire scorciatoie da tastiera e zone finestre.",
                    "Disabilita temporaneamente le scorciatoie di PowerToys finché non viene riavviato.", "BASSO", "Background", 98,
                    "Microsoft PowerToys riconosciuto");

            case "everything":
                return R("Indicizzatore rapido Everything", "Indicizzazione NTFS istantanea per la ricerca immediata di file",
                    "Utilità ad altissime prestazioni per la ricerca di file per nome tramite lettura diretta NTFS.",
                    "Attivo in background per mantenere aggiornato l'indice dei file.",
                    "Chiude l'indicizzatore; la ricerca istantanea non sarà disponibile finché non viene riaperto.", "BASSO", "Background", 98,
                    "Everything.exe riconosciuto");

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
                var clsidMatch = Regex.Match(cmd, @"(?i)/processid:\{?([0-9a-fA-F\-]{8,36})\}?");
                string dllHostSummary = clsidMatch.Success
                    ? $"Ospita componente COM isolato (AppID/CLSID {{{clsidMatch.Groups[1].Value}}})"
                    : "Ospita librerie COM o generatori di anteprime in un processo isolato";
                return R("Host componenti COM (dllhost)", dllHostSummary,
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
                    "Processo del servizio WMI per l'interrogazione dei dati di gestione di Windows.",
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
                    "Componente grafico di sistema di Windows.",
                    "Non terminare: può compromettere la visualizzazione del testo nelle applicazioni.", "ALTO", "Windows", 96,
                    "eseguibile Usermode Font Driver Host (fontdrvhost.exe)");
            case "processlens":
                return R("Dashboard diagnostica ProcessLens / SysPulse", "Interfaccia WPF di monitoraggio processi, memoria RAM, rete e servizi",
                    "Applicazione principale ProcessLens / SysPulse che ispeziona in sola lettura i processi Windows, la memoria fisica/virtuale e i socket TCP/UDP.",
                    p.ParentName is null ? "Sessione diagnostica aperta dall'utente." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Chiude la finestra di monitoraggio ProcessLens / SysPulse.", "BASSO", "Applicazioni", 99,
                    "eseguibile principale ProcessLens.exe riconosciuto");
            case "aggregatorhost":
                return R("Host aggregatore telemetria Windows", "Raccoglie metriche diagnostiche locali per i componenti di sistema",
                    "Componente ausiliario di Windows associato all'aggregazione di eventi diagnostici.",
                    "Processo di sistema Windows; motivo puntuale dell’avvio sconosciuto.",
                    "Interrompe l'aggregazione corrente; Windows può ricrearlo automaticamente.", "BASSO", "Windows", 95,
                    "eseguibile AggregatorHost.exe riconosciuto");
            case "widgets" or "widgetservice":
                return R("Pannello Widget di Windows", "Gestisce la bacheca dei widget, notizie e schede informative di Windows 11",
                    "Fornisce il servizio in background e il rendering WebView2 per il pannello Widget di Windows.",
                    "Componente della shell di Windows 11; motivo puntuale dell’avvio sconosciuto.",
                    "Chiude i Widget in background; si riavvia aprendo il pannello Widget.", "BASSO", "Windows", 96,
                    "componente Windows Widgets riconosciuto");
            case "phoneexperiencehost":
                return R("Collegamento al telefono (Phone Link)", "Sincronizza notifiche, messaggi, chiamate e foto con lo smartphone",
                    "Applicazione Microsoft Phone Link per l'integrazione tra Windows e dispositivi mobili Android/iOS.",
                    "Componente di sincronizzazione mobile; motivo dell’avvio sconosciuto.",
                    "Sospende il collegamento con il telefono fino alla riapertura dell'app.", "BASSO", "Background", 96,
                    "eseguibile PhoneExperienceHost.exe riconosciuto");
            case "gamebar" or "gamebarftserver" or "gamingservices" or "gamingservicesnet":
                return R("Xbox Game Bar e servizi di gioco", "Gestisce overlay di gioco, cattura clip, audio e servizi Xbox su Windows",
                    "Infrastruttura di gioco Microsoft per overlay rapidi (Win+G) e gestione pacchetti Xbox.",
                    "Componente gaming di Windows; motivo dell’avvio sconosciuto.",
                    "Disabilita temporaneamente l'overlay Xbox Game Bar o i servizi di gioco.", "BASSO", "Background", 96,
                    "componente Xbox Game Bar / GamingServices riconosciuto");
            case "systemsettings" or "systemsettingsbroker":
                return R("Impostazioni di sistema Windows", "Interfaccia moderna per la configurazione di rete, schermo, dispositivi e sistema",
                    "Applicazione nativa di configurazione di Windows 10/11 (Impostazioni).",
                    "Finestra o broker delle Impostazioni di Windows; motivo dell’avvio sconosciuto.",
                    "Chiude la finestra delle Impostazioni di Windows.", "BASSO", "Windows", 98,
                    "eseguibile SystemSettings riconosciuto");
            case "lockapp" or "logonui":
                return R("Schermata di blocco e accesso Windows", "Mostra l'interfaccia della schermata di blocco e i controlli di autenticazione",
                    "Gestisce l'interfaccia grafica di blocco sessione, sfondo Spotlight e credenziali di accesso.",
                    "Componente di sessione di Windows; motivo dell’avvio sconosciuto.",
                    "Windows gestisce automaticamente il ciclo di vita della schermata di blocco.", "MEDIO", "Windows", 96,
                    "componente LockApp/LogonUI riconosciuto");
            case "securityhealthhost" or "mpcmdrun" or "nissrv":
                return R("Ispezione e scansione Microsoft Defender", "Esegue scansioni antimalware su richiesta e ispezione del traffico di rete",
                    "Modulo operativo di Sicurezza di Windows per verifiche da riga di comando o protezione di rete in tempo reale.",
                    "Componente operativo di sicurezza Windows; motivo dell’avvio sconosciuto.",
                    "Può interrompere una scansione antimalware o ridurre la protezione di rete.", "ALTO", "Windows", 97,
                    "componente operativo Microsoft Defender riconosciuto");
            case "vssvc":
                return R("Copia Shadow del volume (VSS)", "Coordina snapshot coerenti dei dischi per backup e punti di ripristino",
                    "Volume Shadow Copy Service consente di creare copie di sicurezza dei file anche mentre sono in uso.",
                    "Servizio di sistema Windows; motivo dell’avvio sconosciuto.",
                    "Interrompe la creazione di punti di ripristino o backup in corso.", "MEDIO / ALTO", "Windows", 97,
                    "eseguibile Volume Shadow Copy (vssvc.exe)");
            case "dashost":
                return R("Host associazione dispositivi (dasHost)", "Gestisce l'accoppiamento di periferiche Bluetooth, Wi-Fi, stampanti e UPnP",
                    "Device Association Framework Provider Host coordina la scoperta e l'associazione dei dispositivi hardware.",
                    "Servizio hardware di Windows; motivo dell’avvio sconosciuto.",
                    "Può interrompere l'abbinamento di dispositivi Bluetooth o di rete.", "BASSO / MEDIO", "Windows", 96,
                    "eseguibile Device Association Host (dasHost.exe)");
            case "wlanext":
                return R("Estensibilità rete wireless (wlanext)", "Supporta autenticazione 802.11, hotspot mobile e driver Wi-Fi",
                    "Windows Wireless LAN 802.11 Extensibility Framework ospita moduli per la connettività Wi-Fi.",
                    "Sottosistema wireless di Windows; motivo dell’avvio sconosciuto.",
                    "Può causare la disconnessione temporanea dalla rete Wi-Fi.", "MEDIO", "Windows", 96,
                    "eseguibile WLAN Extensibility Host (wlanext.exe)");
            case "wudfhost":
                return R("Host driver in modalità utente (WUDFHost)", "Esegue driver di periferiche USB, sensori e dispositivi portatili in spazio utente",
                    "Windows Driver Foundation User-mode Driver Framework isola i driver fuori dal kernel per prevenire crash di sistema.",
                    "Sottosistema driver di Windows; motivo dell’avvio sconosciuto.",
                    "Non terminare: disconnette le periferiche hardware gestite dal driver.", "ALTO", "Windows", 97,
                    "eseguibile Windows User-Mode Driver Host (WUDFHost.exe)");
            case "unsecapp":
                return R("Sink asincrono WMI (unsecapp)", "Inoltra risultati e notifiche asincrone WMI ai programmi client",
                    "WMI Unsecured App funge da intermediario per ricevere callback asincrone dal sottosistema WMI.",
                    "Componente ausiliario WMI; motivo dell’avvio sconosciuto.",
                    "I programmi in ascolto di eventi WMI possono smettere di ricevere notifiche.", "BASSO / MEDIO", "Background", 95,
                    "eseguibile WMI Unsecured App (unsecapp.exe)");
            case "backgroundtaskhost":
                return R("Host attività in background UWP", "Esegue compiti periodici di sincronizzazione o notifica per app dello Store",
                    "Processo temporaneo di Windows che esegue task in background per le app pacchettizzate.",
                    "Gestito dal broker di sistema Windows; motivo dell’avvio sconosciuto.",
                    "Interrompe il task secondario corrente dell'app associata.", "BASSO", "Background", 95,
                    "eseguibile backgroundTaskHost.exe riconosciuto");
            case "msbuild" or "vbcscompiler" or "csc":
                return R("Compilatore Roslyn / Motore MSBuild .NET", "Compila progetti C#/VB.NET e mantiene in cache l'analisi sintattica",
                    "Strumento della toolchain .NET SDK per la compilazione incrementale e l'orchestrazione dei file di progetto.",
                    p.ParentName is null ? "Processo di build .NET attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe la compilazione .NET in corso o azzera la cache del compilatore Roslyn.", "BASSO / MEDIO", "Sviluppo", 98,
                    "toolchain di compilazione .NET (MSBuild/Roslyn)");
            case "cl" or "link" or "mspdbsrv":
                return R("Toolchain di compilazione Microsoft Visual C++", "Compila sorgenti C/C++, collega oggetti nativi e genera simboli PDB",
                    "Compilatore nativo MSVC (cl.exe), linker (link.exe) e server di database simboli di debug (mspdbsrv.exe).",
                    p.ParentName is null ? "Processo di compilazione C/C++ attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Fa fallire la compilazione C/C++ attualmente in esecuzione.", "BASSO / MEDIO", "Sviluppo", 98,
                    "toolchain MSVC (cl/link/mspdbsrv)");
            case "gcc" or "g++" or "clang" or "clang++" or "lld" or "cc1" or "cc1plus":
                return R("Compilatore C/C++ GCC / LLVM Clang", "Traduce codice sorgente C/C++ in codice macchina nativo ed eseguibili",
                    "Front-end e ottimizzatore di compilazione per progetti C e C++.",
                    p.ParentName is null ? "Processo di compilazione nativa attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe il job di compilazione C/C++ corrente.", "BASSO / MEDIO", "Sviluppo", 98,
                    "compilatore C/C++ (GCC/Clang)");
            case "cmake" or "ninja" or "make" or "mingw32-make":
                return R("Orchestratore di build CMake / Ninja / Make", "Configura il grafo delle dipendenze e coordina le compilazioni parallele",
                    "Sistema di automazione della build per progetti multipiattaforma.",
                    p.ParentName is null ? "Processo di orchestrazione build attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Arresta l'intera sequenza di compilazione del progetto.", "BASSO / MEDIO", "Sviluppo", 98,
                    "sistema di build (CMake/Ninja/Make)");
            case "javac" or "gradle" or "mvn":
                return R("Sistema di build e compilatore Java (Gradle / Maven)", "Compila classi Java, risolve dipendenze Maven ed esegue test",
                    "Toolchain di automazione e compilazione per l'ecosistema JVM.",
                    p.ParentName is null ? "Processo di build JVM attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe la build Gradle/Maven o il daemon di compilazione Java.", "BASSO / MEDIO", "Sviluppo", 97,
                    "toolchain di build Java (javac/gradle/mvn)");
            case "bun" or "deno":
                return R("Runtime moderno JavaScript / TypeScript (Bun / Deno)", "Esegue applicazioni TypeScript/JS, server web, bundler e test runner",
                    "Motore ad alte prestazioni per l'esecuzione diretta di codice TypeScript e JavaScript.",
                    p.ParentName is null ? "Runtime JS/TS attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Arresta il server o lo script JavaScript/TypeScript in esecuzione.", "BASSO / MEDIO", "Sviluppo", 98,
                    "runtime JS/TS (Bun/Deno)");
            case "cursor" or "windsurf":
                return R("Editor di codice AI-native (Cursor / Windsurf)", "Ambiente di sviluppo integrato con assistenza AI, indicizzazione e terminale",
                    "IDE basato su architettura Chromium/VS Code con integrazione nativa per modelli linguistici del codice.",
                    p.ParentName is null ? "Editor di sviluppo aperto." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Chiude l'editor o il sottoprocesso associato.", "MEDIO", "Sviluppo", 98,
                    "IDE AI-native (Cursor/Windsurf) riconosciuto");
            case "rider64" or "idea64" or "pycharm64" or "webstorm64" or "clion64" or "goland64":
                return R("Ambiente di sviluppo JetBrains IDE", "Indicizzazione codice, refactoring, compilazione e debug avanzato",
                    "IDE professionale della suite JetBrains basato su piattaforma JVM.",
                    p.ParentName is null ? "IDE JetBrains attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Chiude l'ambiente di sviluppo JetBrains e interrompe i task in corso.", "MEDIO", "Sviluppo", 98,
                    "eseguibile JetBrains IDE riconosciuto");
            case "ollama" or "ollama_llama_server" or "llama-server" or "lmstudio":
                return R("Motore di inferenza AI / LLM locale", "Esegue modelli linguistici di grandi dimensioni (LLM) su GPU/CPU locale",
                    "Server e runtime di inferenza locale per l'esecuzione privata di modelli AI generativi.",
                    p.ParentName is null ? "Motore AI locale attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe la generazione AI in corso e scarica il modello dalla memoria VRAM/RAM.", "BASSO / MEDIO", "Sviluppo", 98,
                    "motore inferenza LLM locale (Ollama/llama.cpp/LM Studio)");
            case "claude":
                return R("Assistente AI Claude", "Client desktop o agente a riga di comando per assistenza AI e sviluppo codice",
                    "Applicazione o CLI ufficiale Anthropic Claude.",
                    p.ParentName is null ? "Sessione Claude attiva." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Chiude la sessione corrente di Claude.", "BASSO / MEDIO", "Sviluppo", 97,
                    "eseguibile Claude riconosciuto");
            case "adb" or "emulator" or "qemu-system-x86_64":
                return R("Strumenti SDK Android (ADB / Emulatore)", "Ponte di debug con dispositivi Android ed esecuzione di dispositivi virtuali AVD",
                    "Android Debug Bridge e motore QEMU per il test e il deploy di applicazioni mobile Android.",
                    p.ParentName is null ? "Servizio di sviluppo Android attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Disconnette i dispositivi Android in debug o chiude l'emulatore attivo.", "BASSO / MEDIO", "Sviluppo", 98,
                    "componente Android SDK (adb/emulator/qemu)");
            case "sqlservr" or "sqlwriter":
                return R("Motore database Microsoft SQL Server", "Gestisce database relazionali, transazioni SQL, indici e cache buffer",
                    "Servizio principale del database relazionale Microsoft SQL Server.",
                    "Servizio database attivo; motivo puntuale dell’avvio sconosciuto.",
                    "Interrompe le connessioni ai database SQL locali e annulla le transazioni aperte.", "ALTO", "Sviluppo", 98,
                    "eseguibile Microsoft SQL Server (sqlservr/sqlwriter)");
            case "postgres" or "pg_ctl":
                return R("Server database PostgreSQL", "Gestisce connessioni client, query SQL, WAL e processi worker del database",
                    "Motore di database relazionale open-source PostgreSQL.",
                    p.ParentName is null ? "Istanza database PostgreSQL attiva." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe il servizio database PostgreSQL o chiude la connessione worker.", "MEDIO / ALTO", "Sviluppo", 98,
                    "eseguibile PostgreSQL (postgres.exe)");
            case "mysqld" or "mariadbd":
                return R("Server database MySQL / MariaDB", "Elabora interrogazioni SQL, tabelle InnoDB e connessioni applicative",
                    "Demone del server database relazionale MySQL o MariaDB.",
                    "Servizio database attivo; motivo dell’avvio sconosciuto.",
                    "Arresta il database MySQL/MariaDB; le applicazioni collegate perderanno la connessione.", "MEDIO / ALTO", "Sviluppo", 98,
                    "eseguibile MySQL/MariaDB riconosciuto");
            case "mongod" or "mongos":
                return R("Server database documentale MongoDB", "Gestisce collezioni BSON/JSON, indici e replicazione dati",
                    "Processo principale del database NoSQL orientato ai documenti MongoDB.",
                    "Servizio database MongoDB attivo; motivo dell’avvio sconosciuto.",
                    "Interrompe l'istanza MongoDB e disconnette i client locali.", "MEDIO / ALTO", "Sviluppo", 98,
                    "eseguibile MongoDB (mongod/mongos)");
            case "redis-server":
                return R("Data store in-memory Redis", "Gestisce cache ad altissima velocità, strutture dati in RAM e code di messaggi",
                    "Server Redis per il caching in memoria e la gestione di sessioni e broker di messaggi.",
                    "Processo server Redis attivo; motivo dell’avvio sconosciuto.",
                    "Arresta la cache Redis; i dati non ancora persistiti su disco potrebbero andare persi.", "MEDIO", "Sviluppo", 98,
                    "eseguibile Redis Server riconosciuto");
            case "nginx" or "httpd" or "caddy" or "traefik" or "w3wp" or "iisexpress":
                return R("Server Web HTTP / Reverse Proxy", "Accetta richieste HTTP/HTTPS, serve contenuti web e instrada traffico di rete",
                    "Server web o worker applicativo (Nginx, Apache, Caddy, Traefik, IIS).",
                    p.ParentName is null ? "Server web attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Rende irraggiungibili i siti o le API ospitate su questo server.", "MEDIO", "Sviluppo", 98,
                    "server web HTTP riconosciuto");
            case "kubectl" or "helm" or "minikube" or "kubelet":
                return R("Orchestrazione cluster Kubernetes", "Comunica con le API del cluster Kubernetes, gestisce pod e port-forwarding",
                    "Strumento di gestione e orchestrazione per cluster containerizzati Kubernetes.",
                    p.ParentName is null ? "Comando o tunnel Kubernetes attivo." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Chiude il comando o interrompe i tunnel di port-forward Kubernetes attivi.", "BASSO / MEDIO", "Sviluppo", 97,
                    "strumento Kubernetes (kubectl/helm/minikube)");
            case "vboxheadless" or "virtualboxvm" or "vmware-vmx":
                return R("Macchina virtuale (VirtualBox / VMware)", "Esegue un intero sistema operativo guest isolato con risorse virtualizzate",
                    "Processo hypervisor che mantiene in esecuzione una macchina virtuale completa.",
                    p.ParentName is null ? "Macchina virtuale in esecuzione." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Spegne bruscamente la macchina virtuale guest, con rischio di perdita dati nel sistema ospitato.", "ALTO", "Applicazioni", 98,
                    "processo macchina virtuale (VirtualBox/VMware)");
            case "nvcontainer" or "nvdisplay.container" or "nvidia share" or "nvidia web helper":
                return R("Servizi driver e overlay NVIDIA", "Gestisce pannello di controllo GPU, profili schermo, ShadowPlay e telemetria driver",
                    "Contenitore di servizi utente e di sistema per schede grafiche NVIDIA GeForce / RTX.",
                    "Componente del pacchetto driver NVIDIA; motivo dell’avvio sconosciuto.",
                    "Disabilita temporaneamente l'overlay NVIDIA o le funzioni accessorie del driver grafico.", "BASSO / MEDIO", "Background", 97,
                    "servizio contenitore driver NVIDIA riconosciuto");
            case "amdrsserv" or "radeonsoftware" or "amdow":
                return R("Software e servizi grafici AMD Radeon", "Gestisce impostazioni GPU, profili di gioco, registrazione e metriche hardware",
                    "Componente utente e servizio del driver grafico AMD Software: Adrenalin Edition.",
                    "Componente del pacchetto driver AMD; motivo dell’avvio sconosciuto.",
                    "Chiude il pannello di controllo AMD Radeon e le scorciatoie grafiche.", "BASSO / MEDIO", "Background", 97,
                    "componente software grafico AMD Radeon riconosciuto");
            case "igfxem" or "igfxext" or "igfxcuiservice" or "oneapp.igcc.winservice":
                return R("Servizio controllo grafico Intel", "Gestisce scorciatoie schermo, profili colore e impostazioni della GPU integrata Intel",
                    "Modulo di supporto per i driver grafici Intel Graphics Command Center.",
                    "Componente del driver grafico Intel; motivo dell’avvio sconosciuto.",
                    "Disabilita la gestione avanzata dello schermo Intel fino al riavvio.", "BASSO", "Background", 96,
                    "servizio grafico Intel riconosciuto");
            case "rtkauduservice64" or "ravbg64" or "fmapp":
                return R("Servizio gestione audio Realtek HD", "Gestisce il rilevamento dei jack audio, effetti sonori e configurazione altoparlanti",
                    "Servizio di accompagnamento per il chip audio integrato Realtek High Definition Audio.",
                    "Servizio audio hardware; motivo dell’avvio sconosciuto.",
                    "Può disabilitare il cambio automatico cuffie/altoparlanti o gli effetti Realtek.", "BASSO", "Background", 96,
                    "servizio audio Realtek riconosciuto");
            case "obs64" or "obs-ffmpeg-mux":
                return R("OBS Studio (Registrazione e Streaming)", "Acquisisce schermo, audio e webcam per codifica video o streaming live",
                    "Open Broadcaster Software per la produzione video e la registrazione su disco.",
                    p.ParentName is null ? "Applicazione OBS Studio aperta." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe immediatamente la registrazione video o la diretta streaming in corso.", "MEDIO / ALTO", "Applicazioni", 98,
                    "eseguibile OBS Studio riconosciuto");
            case "vlc" or "mpv":
                return R("Riproduttore multimediale (VLC / mpv)", "Decodifica e riproduce file video, tracce audio e flussi di rete",
                    "Lettore multimediale multiprotocollo con accelerazione hardware.",
                    "Aperto per la riproduzione di contenuti audio/video.",
                    "Interrompe la riproduzione multimediale e chiude la finestra del player.", "BASSO", "Applicazioni", 98,
                    "riproduttore multimediale (VLC/mpv) riconosciuto");
            case "7z" or "7zg" or "winrar" or "tar":
                return R("Gestore archivi e compressione file", "Comprime o estrae archivi di file (.zip, .7z, .rar, .tar.gz)",
                    "Utilità di compressione e decompressione dati ad alte prestazioni.",
                    p.ParentName is null ? "Operazione su archivio in corso." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe la compressione o estrazione, lasciando l'archivio di destinazione incompleto.", "MEDIO", "Applicazioni", 97,
                    "strumento di archiviazione (7-Zip/WinRAR/tar)");
            case "anydesk" or "teamviewer" or "rustdesk" or "mstsc":
                return R("Client di controllo e desktop remoto", "Trasmette lo schermo e inoltra i comandi di mouse e tastiera a distanza",
                    "Software di teleassistenza, accesso remoto o connessione Desktop Remoto (RDP).",
                    "Sessione o servizio di accesso remoto attivo; motivo dell’avvio sconosciuto.",
                    "Disconnette immediatamente la sessione di controllo remoto attiva.", "MEDIO", "Applicazioni", 98,
                    "client desktop remoto (AnyDesk/TeamViewer/RustDesk/RDP)");
            case "wireshark" or "dumpcap" or "tshark":
                return R("Analizzatore di rete Wireshark / Dumpcap", "Cattura e decodifica pacchetti di rete dalle interfacce Ethernet/Wi-Fi",
                    "Strumento diagnostico per l'ispezione del traffico di rete e l'analisi dei protocolli.",
                    p.ParentName is null ? "Analisi pacchetti di rete attiva." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Termina la cattura dei pacchetti di rete in corso.", "BASSO / MEDIO", "Sviluppo", 98,
                    "analizzatore di rete (Wireshark/dumpcap/tshark)");
            case "curl":
                return R("Client trasferimento dati di rete (curl)", "Esegue richieste HTTP/HTTPS/FTP da riga di comando o script",
                    "Utilità a riga di comando per il trasferimento dati tramite URL.",
                    p.ParentName is null ? "Richiesta di rete da riga di comando." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Annulla il download o la richiesta HTTP in corso.", "BASSO", "Sviluppo", 97,
                    "eseguibile curl riconosciuto");
            case "ffmpeg" or "ffprobe":
                return R("Elaboratore multimediale FFmpeg", "Transcodifica, converte, unisce o analizza flussi audio e video",
                    "Motore a riga di comando per la codifica e l'elaborazione di file multimediali.",
                    p.ParentName is null ? "Elaborazione audio/video attiva." : $"Padre riportato: {p.ParentName}; motivo dell’avvio sconosciuto.",
                    "Interrompe la conversione multimediale lasciando il file di output incompleto.", "MEDIO", "Sviluppo", 98,
                    "eseguibile FFmpeg/FFprobe riconosciuto");
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
