using System.IO;
using System.Text;

namespace ProcessLens.Core;

public enum AppLanguage
{
    Italian = 0,
    English = 1
}

/// <summary>
/// Provides bilingual (Italian / English) localization for SysPulse UI and process analysis.
/// Supports startup switches (--lang=en, --lang=it), environment variable (SYSPULSE_LANG),
/// and persistent user preference in %LOCALAPPDATA%\SysPulse\language.txt.
/// Unit tests do not call InitializeFromArgs, so they deterministically run in AppLanguage.Italian.
/// </summary>
public static class SysPulseLocalization
{
    private static AppLanguage _current = AppLanguage.Italian;
    private static readonly object Sync = new();

    public static event Action<AppLanguage>? LanguageChanged;

    public static AppLanguage CurrentLanguage
    {
        get
        {
            lock (Sync)
                return _current;
        }
    }

    public static bool IsEnglish => CurrentLanguage == AppLanguage.English;

    public static void SetLanguage(AppLanguage language, bool persist = true)
    {
        bool changed;
        lock (Sync)
        {
            changed = _current != language;
            _current = language;
        }

        if (persist)
            SaveToDisk(language);

        if (changed)
            LanguageChanged?.Invoke(language);
    }

    public static void ToggleLanguage()
    {
        SetLanguage(IsEnglish ? AppLanguage.Italian : AppLanguage.English, persist: true);
    }

    public static void ReloadFromSavedSettings()
    {
        try
        {
            string path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                string raw = File.ReadAllText(path).Trim();
                var loaded = raw.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.English
                    : AppLanguage.Italian;
                SetLanguage(loaded, persist: false);
            }
        }
        catch
        {
            // Ignore I/O failures in restricted environments.
        }
    }

    public static void InitializeFromArgs(string[]? args)
    {
        ReloadFromSavedSettings();

        if (args is not null)
        {
            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg)) continue;
                string normalized = arg.Trim().ToLowerInvariant();
                if (normalized is "--lang=en" or "--english" or "-en" or "/en" or "en")
                {
                    SetLanguage(AppLanguage.English, persist: true);
                    return;
                }
                if (normalized is "--lang=it" or "--italian" or "-it" or "/it" or "it")
                {
                    SetLanguage(AppLanguage.Italian, persist: true);
                    return;
                }
            }
        }

        string? env = Environment.GetEnvironmentVariable("SYSPULSE_LANG");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (env.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                SetLanguage(AppLanguage.English, persist: true);
            else if (env.StartsWith("it", StringComparison.OrdinalIgnoreCase))
                SetLanguage(AppLanguage.Italian, persist: true);
        }
    }

    private static void SaveToDisk(AppLanguage language)
    {
        try
        {
            string path = GetSettingsFilePath();
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, language == AppLanguage.English ? "en" : "it", Encoding.UTF8);
        }
        catch
        {
            // Ignore write errors in read-only sandboxes.
        }
    }

    private static string GetSettingsFilePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SysPulse", "language.txt");
    }

    // -------------------------------------------------------------------------
    // UI Labels Dictionary (IT -> EN)
    // -------------------------------------------------------------------------
    private static readonly Dictionary<string, string> Dictionary = new(StringComparer.Ordinal)
    {
        // Header & Top Bar
        ["Telemetria reale di sistema e analisi dei processi"] = "Real-time system telemetry and process analysis",
        ["Riduci nel vassoio"] = "Hide to tray",
        ["Log finestra"] = "Window log",
        ["Esci"] = "Exit",
        ["Esegui nuova attività"] = "Run new task",
        ["Cerca"] = "Search",
        ["Cerca:"] = "Search:",
        ["Lingua: IT"] = "Language: EN",

        // Navigation & Filters
        ["Processi"] = "Processes",
        ["Sistema Windows"] = "Windows System",
        ["Servizi"] = "Services",
        ["Guardian"] = "Guardian",
        ["Rete"] = "Network",
        ["Memoria RAM"] = "RAM Memory",
        ["Tutti"] = "All",
        ["Tutte"] = "All",
        ["Applicazioni"] = "Apps",
        ["Sviluppo"] = "Dev",
        ["Windows"] = "Windows",
        ["Secondario"] = "Background",
        ["WSL"] = "WSL",

        // Summary Bar
        ["Processi totali:"] = "Total processes:",
        ["Mostrati:"] = "Shown:",
        ["CPU del sistema:"] = "System CPU:",
        ["Media 30 s:"] = "30s Avg:",
        ["CPU logiche:"] = "Logical CPUs:",
        ["RAM fisica del sistema:"] = "System RAM:",
        ["Risorse di ProcessLens · GUI CPU e RAM"] = "SysPulse Resources · GUI CPU & RAM",
        ["Lettura di processo e sistema"] = "Process and system snapshot",
        ["Stato al momento della selezione"] = "State at selection time",
        ["Campionamento continuo disattivato"] = "Continuous background polling disabled",
        ["Il consumo della finestra è separato da quello del PC. Riduci nel vassoio nasconde la finestra e ferma il timer grafico."] =
            "Window resource usage is separate from PC total. Hide to tray closes the window and pauses UI timers.",
        ["Attuale:"] = "Current:",
        ["Picco osservato:"] = "Observed peak:",
        ["Finestra e vassoio:"] = "Window & tray:",
        ["In corso:"] = "Current activity:",
        ["Diagnostica finestra:"] = "Window diagnostics:",

        // Left Column: Process List & Services List
        ["Seleziona un processo per analizzarne i dati."] = "Select a process to inspect its telemetry.",
        ["Vista: tutti i processi"] = "View: all processes",
        ["Ordina"] = "Sort",
        ["Raggruppa app"] = "Group apps",
        ["Raggruppa per applicazione le istanze dello stesso programma"] = "Group instances of the same application together",
        ["CPU (decrescente)"] = "CPU (descending)",
        ["RAM / Working Set (decrescente)"] = "RAM / Working Set (descending)",
        ["Rete / Connessioni (decrescente)"] = "Network / Connections (descending)",
        ["Nome (A-Z)"] = "Name (A-Z)",
        ["PID (crescente)"] = "PID (ascending)",
        ["Avviato di recente"] = "Recently started",
        ["Elenco dei servizi Windows registrati in Service Control Manager."] = "Windows services registered in Service Control Manager.",
        ["Filtro"] = "Filter",
        ["Aggiorna"] = "Refresh",
        ["Rilegge i servizi registrati in Windows."] = "Reloads registered Windows services.",
        ["Tutti i servizi"] = "All services",
        ["In esecuzione"] = "Running",
        ["Arrestati"] = "Stopped",
        ["Con processo attivo"] = "With active process",
        ["Stato (attivi prima)"] = "State (running first)",
        ["Nome visualizzato (A-Z)"] = "Display name (A-Z)",
        ["Nome servizio (A-Z)"] = "Service name (A-Z)",
        ["Tipo di avvio"] = "Startup type",
        ["Servizi visualizzati"] = "Displayed services",
        ["Stato catalogo:"] = "Catalog status:",

        // Process Row & Group Row Labels
        ["WS PRIVATO"] = "PRIVATE WS",
        ["WS TOTALE"] = "TOTAL WS",
        ["WORKING SET PRIVATO"] = "PRIVATE WORKING SET",
        ["WORKING SET TOTALE"] = "TOTAL WORKING SET",
        ["RAM N/D"] = "RAM N/A",
        ["RETE"] = "NET",
        ["RETE N/D"] = "NET N/A",
        ["RETE · ESTERNO"] = "NET · EXTERNAL",
        ["PUBBLICHE"] = "PUBLIC",
        ["SOCKET"] = "SOCKETS",
        ["N/D"] = "N/A",
        ["CPU N/D"] = "CPU N/A",

        // Context Menus
        ["Apri dettagli del servizio"] = "Open service details",
        ["Vai al processo ospitante"] = "Go to host process",
        ["Copia nome tecnico del servizio"] = "Copy service technical name",
        ["Copia riga selezionata"] = "Copy selected row",
        ["Copia tutto"] = "Copy all",
        ["Copia selezione"] = "Copy selection",
        ["Seleziona tutto"] = "Select all",
        ["Chiudi applicazione (gentile)"] = "Close application (gentle)",
        ["Termina albero processi"] = "Terminate process tree",
        ["Termina processo (forzato)"] = "Terminate process (force)",
        ["Sospendi processo"] = "Suspend process",
        ["Riprendi processo"] = "Resume process",
        ["Priorità CPU"] = "CPU Priority",
        ["Bassa (Idle)"] = "Low (Idle)",
        ["Sotto il normale"] = "Below Normal",
        ["Normale"] = "Normal",
        ["Sopra il normale"] = "Above Normal",
        ["Alta"] = "High",
        ["Riduci Working Set RAM"] = "Trim RAM Working Set",
        ["Apri percorso file"] = "Open file location",
        ["Copia percorso eseguibile"] = "Copy executable path",
        ["Copia riga di comando"] = "Copy command line",

        // Right Column: Process Details & Analysis
        ["Analisi processo"] = "Process Analysis",
        ["Espandi dettagli"] = "Expand details",
        ["Riduci dettagli"] = "Collapse details",
        ["Espandi il pannello dettagli per leggere i rapporti lunghi"] = "Expand the details panel to read full reports",
        ["Mostra di nuovo l'elenco a sinistra"] = "Show the left list panel again",
        ["Seleziona un processo per analizzarlo."] = "Select a process to analyze it.",
        ["Nessun processo selezionato"] = "No process selected",
        ["Mostra tutti i dettagli"] = "Show all details",
        ["Torna alla scheda del processo"] = "Back to process overview",
        ["Stato raccolta: in attesa di selezione"] = "Collection status: waiting for selection",
        ["Stato raccolta: lettura approfondita in corso..."] = "Collection status: deep inspection in progress...",
        ["CPU PROCESSO"] = "PROCESS CPU",
        ["Sull'intera capacità CPU del PC"] = "Share of total PC CPU capacity",
        ["Include memoria condivisa con altri processi"] = "Includes memory shared with other processes",
        ["Rete: in attesa dell'analisi"] = "Network: waiting for inspection",
        ["THREAD"] = "THREADS",
        ["Contatore fornito da Windows"] = "Counter provided by Windows",
        ["RUOLO STIMATO"] = "ESTIMATED ROLE",
        ["AVVIATO IL"] = "STARTED AT",
        ["IN ESECUZIONE DA"] = "RUNNING FOR",
        ["Non disponibile"] = "Unavailable",

        // Ask SysPulse / Quick Questions Menu
        ["CHIEDI A PROCESSLENS"] = "ASK SYSPULSE",
        ["Scrivi una domanda sul processo selezionato o scegli un'analisi dal menu qui sotto."] =
            "Type a question about the selected process or choose a deep analysis from the menu below.",
        ["Chiedi"] = "Ask",
        ["Analisi approfondita ▾"] = "Deep Analysis ▾",
        ["Scegli un'analisi dal menu."] = "Choose an analysis from the menu.",
        ["Scegli un’analisi dal menu."] = "Choose an analysis from the menu.",
        ["Scrivi una domanda libera sul processo selezionato"] = "Type a custom question about the selected process",
        ["Esegue ProcessLensProfiler separato; richiede consenso e dotnet-gcdump installato"] =
            "Runs the isolated ProcessLensProfiler; requires consent and dotnet-gcdump",
        ["Copia rapporto completo"] = "Copy full report",
        ["Copia anche le sezioni espandibili e il registro verifiche del rapporto corrente"] =
            "Copies all expandable sections and the verification log of the current report",
        ["Seleziona e premi Ctrl+C. Tasto destro: copia tutto."] = "Select and press Ctrl+C. Right-click: copy all.",
        ["Annulla profilazione"] = "Cancel profiling",
        ["QUADRO GENERALE"] = "OVERVIEW",
        ["Registro analisi RAM"] = "RAM Analysis Log",
        ["RIEPILOGO"] = "SUMMARY",
        ["Copia registro completo"] = "Copy full log",
        ["Copia il testo originale del registro corrente, comprese le voci non visibili"] =
            "Copies the full raw text of the current log, including collapsed entries",
        ["Testo originale del registro"] = "Original raw log text",
        ["DATI WINDOWS"] = "WINDOWS TELEMETRY",
        ["ASSOCIAZIONE ALL’APP"] = "APP ASSOCIATION",
        ["ASSOCIAZIONE ALL'APP"] = "APP ASSOCIATION",
        ["RUOLO"] = "ROLE",
        ["La classificazione descrive il ruolo; CPU e rete mostrano l’attività rilevata."] =
            "Classification describes the role; CPU and network show observed live activity.",
        ["PROCESSO PADRE E AVVIO"] = "PARENT PROCESS & STARTUP",
        ["Seleziona un processo."] = "Select a process.",
        ["COMANDO DI AVVIO"] = "STARTUP COMMAND",
        ["Mostra la catena di avvio"] = "Show startup chain",
        ["SINTESI ATTIVITÀ"] = "ACTIVITY SUMMARY",
        ["Seleziona un processo dalla lista."] = "Select a process from the list.",
        ["DESCRIZIONE"] = "DESCRIPTION",
        ["Dati utilizzati per riconoscere il processo. Apri le sezioni per i dettagli."] =
            "Evidence used to identify the process. Expand sections for full details.",
        ["Dati tecnici della classificazione"] = "Technical classification data",
        ["Valori interni del classificatore e dati che hanno contribuito al risultato. Le misure di attività sono separate."] =
            "Internal classifier scores and contributing evidence. Live activity metrics are measured separately.",
        ["Rapporto diagnostico originale - testo integrale"] = "Original diagnostic report - full raw text",
        ["Identificativi interni e metadati originali."] = "Internal identifiers and raw metadata.",
        ["AVVIO DEL PROCESSO"] = "WHY THIS PROCESS IS RUNNING",
        ["Effetti variabili. Chiusura solo su richiesta."] = "Effects vary by workload. Termination occurs only on explicit user request.",
        ["ENGLISH PROCESS ANALYSIS · ANALISI IN INGLESE"] = "ENGLISH PROCESS ANALYSIS",
        ["Quota della CPU complessiva attribuita al PID."] = "Share of total PC CPU capacity used by this PID.",
        ["Working Set privato: pagine private fisicamente residenti (Private Working Set)."] =
            "Private Working Set: private pages physically resident in RAM.",
        ["PROGETTO"] = "PROJECT",
        ["Progetto o directory associata, quando identificabile."] = "Associated project or working directory, when identifiable.",
        ["MEMORIA · MISURE SEPARATE"] = "MEMORY · SEPARATE METRICS",
        ["Analizza RAM ora ▾"] = "Analyze RAM now ▾",
        ["Esegue subito la diagnosi anatomica completa della RAM per questo processo"] =
            "Immediately runs the full RAM anatomy diagnosis for this process",
        ["MEMORIA PRIVATA IMPEGNATA"] = "PRIVATE COMMITTED MEMORY",
        ["Commit e RAM sono contatori diversi. Confronta stessa istanza, contatore e intervallo."] =
            "Commit and physical RAM are distinct counters. Compare the same instance, counter, and interval.",
        ["Che cosa significa RAM privata?"] = "What does Private RAM mean?",
        ["Apri una guida alle tre misure di memoria del PID selezionato."] = "Open a guide to the three memory metrics for the selected PID.",
        ["1 · Working Set privato (Private Working Set)"] = "1 · Private Working Set",
        ["Pagine private del processo presenti in RAM. Il valore non include tutti i suoi dati allocati o i sottoprocessi."] =
            "Private pages of the process physically resident in RAM. Does not include paged-out allocations or child processes.",
        ["2 · Working Set totale (Working Set)"] = "2 · Total Working Set",
        ["Pagine private e condivisibili presenti in RAM. Include il Working Set privato: non sommarli. Le pagine condivisibili possono comparire in più processi."] =
            "Private plus shareable pages physically in RAM. Includes Private Working Set: do not sum them together.",
        ["3 · Memoria privata impegnata (Private Bytes / commit)"] = "3 · Private Committed Memory (Private Bytes / Commit)",
        ["Memoria privata con copertura garantita da Windows. Non misura RAM occupata o dati scritti nel file di paging."] =
            "Private virtual memory backed by Windows commit charge. Does not equal physical RAM in use.",
        ["Esempio basato sul processo selezionato"] = "Example for the selected process",
        ["Seleziona un processo per vedere le misure."] = "Select a process to view its memory metrics.",
        ["Come interpretarle correttamente"] = "How to interpret these metrics accurately",
        ["Per cercare un memory leak, osserva lo stesso PID nel tempo insieme a commit e RAM del sistema. Sottoprocessi, GPU e kernel vanno valutati a parte. N/D non equivale a zero."] =
            "To check for a memory leak, monitor the same PID over time alongside commit charge and system RAM. N/A does not mean zero.",
        ["Origine dei contatori Windows"] = "Windows OS counter sources",
        ["API GetProcessMemoryInfo, struttura PROCESS_MEMORY_COUNTERS_EX2: PrivateWorkingSetSize, WorkingSetSize e PrivateUsage. Sono misure riferite al PID; ProcessLens non stima queste quantità a partire dalle regioni di memoria virtuale."] =
            "Win32 API GetProcessMemoryInfo (PROCESS_MEMORY_COUNTERS_EX2): PrivateWorkingSetSize, WorkingSetSize, and PrivateUsage.",
        ["ANALISI CPU · FINO A 30 SECONDI"] = "CPU ANALYSIS · UP TO 30 SECONDS",
        ["Media: -"] = "Avg: -",
        ["Picco: -"] = "Peak: -",
        ["CPU: quota della capacità totale del PC, non di un core. Campione ogni circa 2 s; elenco aggiornato a parte."] =
            "CPU: share of total PC capacity across all logical cores. Sampled every ~1–2 s.",
        ["ATTIVITÀ RILEVATA · CPU, RETE E FINESTRE"] = "OBSERVED ACTIVITY · CPU, NETWORK & WINDOWS",
        ["Aggiorna porte del PID e del gruppo"] = "Refresh ports for PID and process group",
        ["CPU, finestre e rete sono osservazioni Windows. L'analisi approfondita parte solo sul processo selezionato per mantenere ProcessLens leggero."] =
            "CPU, windows, and network sockets are live Windows observations. Deep inspection runs on demand for the selected process.",
        ["Dettagli tecnici e fonti"] = "Technical details & sources",
        ["Apri una sezione. Dati Windows e classificazione locale."] = "Expand a section to inspect raw Windows telemetry and classification rules.",
        ["Identità e ambiente di esecuzione"] = "Identity & runtime environment",
        ["CPU e memoria: dati misurati"] = "CPU & memory: measured counters",
        ["Rete, servizi e relazioni"] = "Network, services & relationships",
        ["Percorso e parametri di avvio"] = "Executable path & startup parameters",
        ["PERCORSO DELL'ESEGUIBILE"] = "EXECUTABLE PATH",
        ["PARAMETRI DI AVVIO"] = "STARTUP PARAMETERS",
        ["Un parametro -- per riga. Originale disponibile sotto."] = "One --switch per line. Raw command line available below.",
        ["Mostra la riga di comando originale"] = "Show raw command line",
        ["Dati e fonti dell'identificazione"] = "Identification data & sources",
        ["Dati per argomento."] = "Data grouped by topic.",
        ["Snapshot dell’ultimo aggiornamento dei dettagli. I valori CPU aggiornati continuamente sono nel riquadro Analisi CPU."] =
            "Snapshot from the latest detail refresh. Continuously updated CPU metrics are shown in the CPU Analysis card above.",
        ["Ridimensiona elenco e dettagli dei servizi"] = "Resize service list and details",
        ["Servizi Windows"] = "Windows Services",
        ["Caricamento..."] = "Loading...",
        ["Attivi prima, poi nome"] = "Running first, then name",
        ["Nome A-Z"] = "Name A-Z",
        ["PID host"] = "Host PID",
        ["Dettagli del servizio"] = "Service Details",
        ["Tutti i servizi SCM"] = "All SCM services",
        ["Mostra tutti i servizi"] = "Show all services",
        ["SCM: stato e configurazione del servizio."] = "SCM: service state and configuration.",
        ["Seleziona un servizio"] = "Select a service",
        ["DESCRIZIONE DEL SERVIZIO"] = "SERVICE DESCRIPTION",
        ["Descrizione non disponibile."] = "Description unavailable.",
        ["Fonte: descrizione dichiarata dal servizio, se disponibile."] = "Source: description registered by the service in Windows SCM.",
        ["MODALITÀ DI AVVIO E STATO"] = "STARTUP MODE & STATE",
        ["CRONOLOGIA AVVIO / ARRESTO"] = "START / STOP EVENT HISTORY",
        ["Aggiorna eventi"] = "Refresh events",
        ["Seleziona un servizio per consultare gli eventi Windows."] = "Select a service to inspect Windows Event Log entries.",
        ["Eventi SCM. Richiedente dell’avvio non identificato; i vecchi eventi possono mancare."] =
            "SCM events from Windows Event Log. Older events may have rotated out.",
        ["PROCESSO CHE LO OSPITA"] = "HOST PROCESS",
        ["Mostra processo ospitante"] = "Show host process",
        ["Arresta servizio…"] = "Stop service…",
        ["Arresto tramite SCM; nessuna terminazione forzata del PID."] = "Orderly stop via Windows SCM; does not force-kill the host PID.",
        ["AVVIO E DIPENDENZE"] = "STARTUP & DEPENDENCIES",
        ["SE VIENE ARRESTATO"] = "IF STOPPED",
        ["Identità tecnica e percorso"] = "Technical identity & binary path",
        ["Processo chiuso"] = "Process exited",
        ["Esecuzione dell'analisi selezionata..."] = "Running selected analysis...",
        ["Analisi richiesta: "] = "Requested analysis: ",
        ["Risposta"] = "Analysis Answer",
        ["Chiudi risposta"] = "Close answer",
        ["INFORMAZIONI GENERALI"] = "GENERAL INFORMATION",
        ["Che cosa sta facendo?"] = "What is it doing?",
        ["A che punto è?"] = "What is its progress?",
        ["Quando terminerà?"] = "When will it finish?",
        ["È bloccato?"] = "Is it stuck or frozen?",
        ["RISORSE E DIAGNOSTICA"] = "RESOURCES & DIAGNOSTICS",
        ["Perché occupa RAM? · Rapporto dettagliato"] = "Why is it using RAM? · Detailed report",
        ["Profilazione heap .NET · oggetti reali (su richiesta)"] = ".NET Heap Profiling · live objects (on demand)",
        ["Perché utilizza CPU?"] = "Why is it using CPU?",
        ["Porte e connessioni"] = "Ports and network connections",
        ["ORIGINE E IMPATTO DELLA CHIUSURA"] = "ORIGIN & TERMINATION IMPACT",
        ["Componente Windows: ruolo e dati"] = "Windows component: role and telemetry",
        ["Processo padre e origine"] = "Parent process and origin",
        ["Posso chiuderlo?"] = "Can I close it safely?",

        // Detail Sections
        ["IDENTITÀ E CLASSIFICAZIONE"] = "IDENTITY & CLASSIFICATION",
        ["IN SINTESI"] = "SUMMARY",
        ["CHE COSA FA"] = "WHAT IT DOES",
        ["PERCHÉ È ATTIVO"] = "WHY IT IS RUNNING",
        ["ANALISI IN INGLESE · ENGLISH PROCESS ANALYSIS"] = "ENGLISH PROCESS ANALYSIS",
        ["ELEMENTI DI IDENTIFICAZIONE"] = "IDENTIFICATION EVIDENCE",
        ["SE TERMINI IL PROCESSO"] = "IF YOU TERMINATE THIS PROCESS",
        ["Effetto stimato sulle funzioni collegate. Non sostituisce una verifica delle attività in corso."] =
            "Estimated effect on dependent functions. Verify active work before terminating.",
        ["Azioni sul processo"] = "Process Actions",
        ["Le azioni modificano lo stato del processo selezionato. Verifica sempre il ruolo e i dati non salvati prima di procedere."] =
            "Actions modify the selected process state. Always verify its role and unsaved work before proceeding.",
        ["Dettagli tecnici e risorse"] = "Technical Details & Resources",
        ["Dati di CPU, memoria, rete, percorso, firma e albero dei processi"] = "CPU, memory, network, path, signature, and process tree telemetry",
        ["ANDAMENTO CPU E STATO"] = "CPU TREND & RUNTIME STATE",
        ["DETTAGLIO MEMORIA E VARIAZIONE"] = "MEMORY BREAKDOWN & TREND",
        ["RETE E PORTE OSSERVATE"] = "OBSERVED NETWORK & PORTS",
        ["Porte e connessioni: seleziona un processo per avviare l'analisi."] = "Ports and connections: select a process to start inspection.",
        ["PERCORSO, PARAMETRI E RELAZIONI"] = "PATH, ARGUMENTS & PROCESS TREE",
        ["Opzioni di copia…"] = "Copy options…",
        ["Come è stato riconosciuto"] = "How it was identified",
        ["Elementi usati per identificare ruolo, applicazione e origine"] = "Evidence used to identify role, application, and provenance",
        ["Copia tutti gli elementi"] = "Copy all evidence",
        ["Rapporto diagnostico originale"] = "Raw Diagnostic Report",
        ["Dati originali completi"] = "Complete raw diagnostic data",
        ["Copia diagnostica"] = "Copy diagnostics",
        ["Diagnostica completa in formato originale"] = "Complete diagnostics in raw format",

        // Services Detail Panel
        ["Dettaglio servizio"] = "Service Details",
        ["Espandi il pannello dettagli per leggere tutte le informazioni sul servizio"] = "Expand the details panel to read full service information",
        ["Seleziona un servizio dall'elenco."] = "Select a service from the list.",
        ["Nessun servizio selezionato"] = "No service selected",
        ["Apri il processo ospitante nell'elenco Processi"] = "Open host process in Processes tab",
        ["STATO"] = "STATE",
        ["TIPO DI AVVIO"] = "STARTUP TYPE",
        ["PID OSPITANTE"] = "HOST PID",
        ["TIPO DI HOST"] = "HOST TYPE",
        ["DESCRIZIONE REGISTRATA IN WINDOWS"] = "WINDOWS REGISTERED DESCRIPTION",
        ["PERCHÉ È IN ESECUZIONE / RUOLO"] = "WHY IT IS RUNNING / ROLE",
        ["SE VIENE ARRESTATO O DISABILITATO"] = "IF STOPPED OR DISABLED",
        ["CONFIGURAZIONE E DIPENDENZE"] = "CONFIGURATION & DEPENDENCIES",
        ["Copia scheda servizio"] = "Copy service report",
        ["Seleziona un servizio per leggere i dettagli completi."] = "Select a service to view full details.",

        // Windows System View
        ["Componenti di Windows"] = "Windows Components",
        ["Kernel, sessioni, servizi e sicurezza. Catalogo mirato, non un elenco completo di Windows. R22"] =
            "Kernel, sessions, services, and security. Curated system catalog. R22",
        ["Aggiorna campione"] = "Refresh sample",
        ["Copia rapporto"] = "Copy report",
        ["Filtra per area di Windows"] = "Filter by Windows subsystem area",
        ["Tutti i componenti"] = "All components",
        ["Kernel e memoria"] = "Kernel and memory",
        ["Avvio e sessioni"] = "Startup and sessions",
        ["Sicurezza"] = "Security",
        ["Desktop e console"] = "Desktop and console",
        ["Cerca componente o funzione; per PID usa solo cifre. I programmi comuni restano nella scheda Processi."] =
            "Search component or function; use digits for PID. Regular apps remain in the Processes tab.",
        ["Premi Aggiorna campione. Una riga per componente; le istanze sono riunite."] =
            "Press Refresh sample. One row per component; multiple instances are grouped.",
        ["Copertura: N/D"] = "Coverage: N/A",
        ["Memoria del kernel, fonte e copertura"] = "Kernel memory, data source, and coverage",
        ["Una riga riunisce più istanze: selezionala per vedere ogni PID. I conteggi includono solo le voci restituite dalla fonte, anche con dettagli N/D. La firma digitale non è verificata."] =
            "Each row groups multiple instances: select it to inspect each PID. Counts include only entries returned by the OS source. Digital signatures are not verified here.",
        ["Memoria: N/D"] = "Memory: N/A",
        ["Nessuna lettura eseguita."] = "No snapshot read yet.",
        ["La lettura non cattura eventi: per avvii e uscite usa Guardian. Processi brevi e voci protette possono mancare. Driver, interrupt e DPC non sono processi separati. Nessun nuovo monitor in background."] =
            "Snapshots do not capture transient events: use Guardian for process starts/exits. Short-lived or protected entries may be omitted. Drivers, interrupts, and DPCs are not separate processes.",
        ["Area"] = "Area",
        ["Componente"] = "Component",
        ["Funzione"] = "Function",
        ["Istanze"] = "Instances",
        ["WS max letto"] = "Max read WS",
        ["Identificazione"] = "Identification",
        ["Trascina per dare più spazio alla tabella o ai dettagli"] = "Drag to resize table and details",
        ["Dettagli del componente"] = "Component Details",
        ["Istanza da approfondire"] = "Instance to inspect",
        ["Analizza PID selezionato"] = "Analyze selected PID",
        ["Seleziona un componente per leggere funzione e dati del campione."] = "Select a component to read its function and telemetry sample.",

        // Guardian View
        ["Guardian · storico"] = "Guardian · Event History",
        ["ETW e SQLite · aggiornamento ogni 10 s · sola lettura"] = "ETW & SQLite · updates every 10 s · read-only",
        ["Intervallo"] = "Interval",
        ["Intervallo:"] = "Interval:",
        ["5 minuti"] = "5 minutes",
        ["15 minuti"] = "15 minutes",
        ["30 minuti"] = "30 minutes",
        ["60 minuti"] = "60 minutes",
        ["Aggiorna ora"] = "Refresh now",
        ["AVVII OSSERVATI"] = "OBSERVED STARTS",
        ["ETW + polling · finestra selezionata"] = "ETW + polling · selected window",
        ["TERMINAZIONI"] = "TERMINATIONS",
        ["ETW + polling"] = "ETW + polling",
        ["AVVII ETW"] = "ETW STARTS",
        ["Eventi origine ETW"] = "ETW source events",
        ["EVENTI COMPLESSIVI"] = "TOTAL EVENTS",
        ["Avvii, uscite e inventario"] = "Starts, exits, and inventory",
        ["SQLITE + WAL"] = "SQLITE + WAL",
        ["Dimensione su disco in MiB"] = "Disk size in MiB",
        ["Seleziona Guardian per avviare la lettura dello storico."] = "Select Guardian to load event history.",
        ["Eventi recenti"] = "Recent events",
        ["Cerca nel campione recente"] = "Search recent sample",
        ["Ora locale"] = "Local time",
        ["Eseguibile"] = "Executable",
        ["Evento"] = "Event",
        ["Origine"] = "Source",
        ["Massimo 250 eventi più recenti nel periodo. Ricerca applicata al campione visualizzato; non a tutto lo storico."] =
            "Up to 250 most recent events in the selected window. Search filters the displayed sample.",
        ["Avvii e padri"] = "Starts & parents",
        ["ANDAMENTO DEGLI AVVII ETW"] = "ETW PROCESS START TREND",
        ["Intervalli equidistanti, lunghezza della barra proporzionale al massimo osservato nel periodo. Conteggi reali indicati su ogni barra."] =
            "Equal time buckets, bar length proportional to peak starts in the period. Exact counts shown on each bar.",
        ["ESEGUIBILI CON PIÙ AVVII ETW"] = "TOP EXECUTABLES BY ETW STARTS",
        ["Nome"] = "Name",
        ["Avvii"] = "Starts",
        ["/ minuto"] = "/ minute",
        ["GENERATORI DI PROCESSI · PARENTELA PROBABILE"] = "PROCESS SPAWNERS · PROBABLE LINEAGE",
        ["PID padre"] = "Parent PID",
        ["Nome ipotizzato"] = "Candidate parent",
        ["Figlio"] = "Child",
        ["Evidenza / limite"] = "Evidence / limitation",
        ["Il PPID da solo non dimostra la parentela: Windows può riutilizzare i PID. Nessuna attribuzione è definitiva."] =
            "PPID alone does not prove parentage: Windows can reuse PIDs over time.",
        ["Esplora parentela"] = "Explore lineage",
        ["PID da esplorare"] = "PID to explore",
        ["Analizza PID"] = "Analyze PID",
        ["Usa padre selezionato"] = "Use selected parent",
        ["Esplora figlio selezionato"] = "Explore selected child",
        ["Seleziona un PID dalla tabella dei generatori oppure inseriscilo qui."] = "Select a PID from the spawners table or type it here.",
        ["Lettura manuale dallo storico; nessuna modifica ai processi."] = "Read-only historical lookup; no process state is modified.",
        ["Le corrispondenze PID/orario sono indizi, non prove definitive di parentela. Timestamp con precisione di un secondo; PID riutilizzabili."] =
            "PID/timestamp matches are indicators, not absolute proof of parentage. 1-second timestamp granularity; PIDs can be reused.",
        ["CATENA DEGLI ANTENATI · fino a 6 livelli"] = "ANCESTOR CHAIN · up to 6 levels",
        ["Liv."] = "Lvl.",
        ["Stato"] = "Status",
        ["Evidenza"] = "Evidence",
        ["FIGLI DIRETTI OSSERVATI · doppio clic per approfondire"] = "OBSERVED DIRECT CHILDREN · double-click to inspect",
        ["Ora avvio"] = "Start time",
        ["I figli vengono caricati su richiesta; massimo 200 avvii per analisi."] = "Children are loaded on demand; up to 200 starts per analysis.",
        ["Risorse storiche"] = "Historical resources",
        ["CAMPIONI AGGREGATI PER NOME ESEGUIBILE"] = "AGGREGATED SAMPLES BY EXECUTABLE NAME",
        ["CPU media %"] = "Avg CPU %",
        ["CPU picco %"] = "Peak CPU %",
        ["RAM residente max MiB"] = "Max resident RAM MiB",
        ["Campioni"] = "Samples",
        ["Campioni per nome, non riferiti a un solo PID o al suo padre. Bucket da 5 minuti: l'intervallo è approssimato. La RAM residente include memoria condivisa."] =
            "Samples grouped by executable name across 5-minute buckets. Resident RAM includes shared memory.",
        ["Diagnostica ETW / SQLite"] = "ETW / SQLite Diagnostics",
        ["Dettaglio"] = "Detail",
        ["Ridimensiona elenco e dettaglio diagnostico"] = "Resize list and diagnostic detail",
        ["DETTAGLIO DELLA VOCE SELEZIONATA"] = "SELECTED ENTRY DETAIL",
        ["Limiti dei contatori"] = "Counter limitations",
        ["Messaggio originale · selezionabile e copiabile"] = "Original raw message · selectable and copyable",
        ["Assenza di avvisi non garantisce completezza ETW. Se SQLite è indisponibile, alcune perdite possono non risultare nelle diagnostiche."] =
            "Absence of warnings does not guarantee 100% ETW completeness. If SQLite is unavailable, some dropped events may not be logged.",

        // Memory Dashboard View
        ["Analizzatore Memoria RAM Fisica e Suddivisione per Categorie"] = "Physical RAM Analyzer & Category Breakdown",
        ["Aggiorna analisi RAM"] = "Refresh RAM analysis",
        ["Rileggi tutti i processi del PC, ricalcola le categorie e aggiorna la classifica"] = "Rescan all PC processes, recalculate categories, and update the ranking",
        ["Lettura in corso"] = "Reading...",
        ["RAM utilizzata"] = "Used RAM",
        ["Dati non ancora disponibili"] = "Data not yet available",
        ["RAM disponibile"] = "Available RAM",
        ["Memoria fisica libera subito utilizzabile"] = "Free physical memory immediately available",
        ["RAM fisica totale"] = "Total physical RAM",
        ["Totale fisico rilevato da Windows"] = "Total physical RAM reported by Windows",
        ["Attendo i dati di Windows."] = "Waiting for Windows telemetry.",
        ["COME È SUDDIVISA LA RAM IN USO (PER MACRO-CATEGORIA)"] = "HOW IN-USE RAM IS DIVIDED (BY MACRO-CATEGORY)",
        ["Applicazioni e Browser →"] = "Applications & Browsers →",
        ["Sviluppo e Runtime →"] = "Development & Runtimes →",
        ["Sistema Windows →"] = "Windows System →",
        ["Servizi e Secondari →"] = "Services & Background →",
        ["Memoria non attribuita ai PID →"] = "Memory not attributed to PIDs →",
        ["Apri i limiti del residuo: questo calcolo non identifica Kernel, driver o DLL specifiche"] =
            "Open residual memory notes: this calculation does not separately identify Kernel, drivers, or specific DLLs",
        ["Che cosa rappresenta la memoria non attribuita"] = "What unattributed memory represents",
        ["È la differenza fra RAM fisica in uso e Working Set privato raccolta dai processi. Può comprendere memoria condivisa, componenti di sistema e dati mancanti: questo valore non identifica né misura separatamente Kernel, driver o DLL. Non esiste qui un elenco di processi cui attribuire esattamente il residuo."] =
            "It is the difference between total physical RAM in use and the sum of Private Working Sets across accessible processes. It includes shared pages, system/kernel structures, file caches, and protected processes.",
        ["Lettura Windows in corso"] = "Reading Windows memory...",
        ["Avvisi automatici RAM: "] = "Automatic RAM alerts: ",
        ["Verifica configurazione agente..."] = "Checking alert agent configuration...",
        ["Avviso al raggiungimento dell'85%; nuovo avviso solo dopo il ritorno sotto l'80%."] =
            "Alerts when RAM reaches 85%; re-arms only after dropping below 80%.",
        ["Soglia 85% / 80%"] = "Threshold 85% / 80%",
        ["Soglia primo avviso / soglia di riarmo, non valori della RAM attuale"] = "Alert threshold / re-arm threshold, not current RAM usage",
        ["Chi consuma più RAM (Programmi raggruppati e Processi)"] = "Top RAM Consumers (Grouped Programs & Processes)",
        ["Processi raggruppati per programma e ordinati per RAM privata esclusiva."] = "Processes grouped by application and sorted by exclusive private RAM.",
        ["Ispeziona anatomia RAM del selezionato →"] = "Inspect selected process RAM anatomy →",
        ["Apre la scheda del processo selezionato ed esegue subito l'analisi anatomica completa della sua RAM (VirtualQueryEx + Working Set + Albero)"] =
            "Opens the selected process tab and immediately runs a full RAM anatomy inspection (VirtualQueryEx + Working Set + Process Tree)",
        ["Vista:"] = "View:",
        ["Per Programma (Multi-processo uniti)"] = "By Program (Multi-process grouped)",
        ["Unisce tutti i processi dello stesso programma (es. tutte le schede di Chrome/Edge) sommando solo la RAM privata esclusiva"] =
            "Combines all processes of the same application (e.g. all Chrome/Edge tabs) summing exclusive private RAM",
        ["Per Singolo Processo (PID)"] = "By Single Process (PID)",
        ["Mostra ogni singolo PID separatamente con RAM privata, RAM condivisa, Working Set e Commit"] =
            "Shows each individual PID separately with Private RAM, Shared RAM, Working Set, and Commit",
        ["  Categoria:"] = "  Category:",
        ["Categoria:"] = "Category:",
        ["Programma / Processo"] = "Program / Process",
        ["Categoria"] = "Category",
        ["Istanze / PID"] = "Instances / PID",
        ["RAM privata (MiB)"] = "Private RAM (MiB)",
        ["Condivisa (MiB)"] = "Shared (MiB)",
        ["RAM totale (MiB)"] = "Total RAM (MiB)",
        ["Commit privato (MiB)"] = "Private Commit (MiB)",
        ["Quota RAM PC"] = "PC RAM Share",
        ["RAM privata: memoria fisica esclusiva del processo o gruppo (sommabile senza doppi conteggi). Condivisa: DLL di sistema/runtime in comune con altri programmi. Doppio clic: dettagli dei servizi SCM associati, oppure del processo. Il pulsante Ispeziona avvia l’analisi RAM. Le categorie sono classificazioni, non prove di un servizio SCM."] =
            "Private RAM: exclusive physical memory of the process/group (summable without double-counting). Shared: system/runtime DLLs shared with other programs. Double-click: open associated SCM services or process details.",

        // Network Views
        ["Dispositivi osservati, traffico del PC ed eventi Windows: ogni scheda indica la propria fonte e copertura."] =
            "Observed devices, PC traffic, and Windows security events: each tab reports its data source and coverage.",
        ["Dispositivi osservati"] = "Observed devices",
        ["Dispositivi osservati da Windows"] = "Devices observed by Windows",
        ["1. Leggi la cache  →  2. Seleziona una voce  →  3. Assegna un nome, se la riconosci."] =
            "1. Read cache  →  2. Select an entry  →  3. Assign a friendly name if recognized.",
        ["Aggiorna elenco"] = "Refresh list",
        ["Elenco non aggiornato"] = "List not refreshed yet",
        ["Controllo non ancora eseguito"] = "Check not performed yet",
        ["Premi Aggiorna elenco."] = "Press Refresh list.",
        ["Voci con MAC: N/D · nessuna lettura"] = "Entries with MAC: N/A · no reading yet",
        ["Nome scelto"] = "Assigned name",
        ["IP nella cache"] = "Cached IP",
        ["Stato nella cache"] = "Cache state",
        ["Riconoscimento"] = "Recognition",
        ["Scheda"] = "Adapter",
        ["Nessuna lettura eseguita. Premi Aggiorna elenco."] = "No reading performed yet. Press Refresh list.",
        ["Seleziona un dispositivo per riconoscerlo."] = "Select a device to assign a label.",
        ["Nome scelto:"] = "Assigned name:",
        ["Nome assegnato da te, per esempio Telefono di casa."] = "Friendly name assigned by you, e.g. Home Phone.",
        ["Riconosco questo dispositivo / Salva nome"] = "Recognize device / Save name",
        ["Rimuovi riconoscimento"] = "Forget recognition",
        ["Fonte, copertura e dettagli della lettura"] = "Data source, coverage, and reading details",
        ["Fonte: Windows GetIpNetTable2 · IPv4/ARP e IPv6/ND. L’elenco può essere incompleto e contenere voci vecchie. Una voce non prova che il dispositivo sia collegato adesso."] =
            "Source: Windows GetIpNetTable2 · IPv4/ARP and IPv6/ND. The cache may be incomplete or contain stale entries. An entry does not prove active connection right now.",
        ["Raggruppamento per MAC e scheda: un dispositivo può comparire più volte. Riconosciuto da te è una tua etichetta, non una verifica dell’identità. MAC e impronta della rete possono cambiare o essere imitati."] =
            "Grouped by MAC and adapter: a device may appear multiple times. User labels are local notes, not cryptographic identity verification.",
        ["Verde: tutte le voci rilevate con MAC sono riconosciute. Giallo: ci sono voci da riconoscere. Grigio: dati incompleti o riconoscimento non disponibile. Questi stati non certificano la sicurezza della rete."] =
            "Green: all detected MAC entries are labeled. Yellow: unrecognized entries present. Gray: incomplete data.",
        ["Traffico e Wi-Fi"] = "Traffic & Wi-Fi",
        ["Eventi e blocchi"] = "Events & blocks",
        ["Log delle operazioni di rete · sessione"] = "Network operations log · session",
        ["Copia log di sessione"] = "Copy session log",
        ["Ultime 128 righe · nessuna scrittura su disco"] = "Last 128 lines · in-memory only",
        ["Nessuna operazione eseguita."] = "No operation performed yet.",
        ["Traffico del PC e reti Wi-Fi"] = "PC Traffic & Wi-Fi Networks",
        ["Traffico: elenca le sorgenti, scegline una e cattura per 20 s. Wi-Fi: leggi separatamente le reti radio nella cache Windows. Non servono programmi aggiuntivi."] =
            "Traffic: list sources, pick one, and capture for 20 s. Wi-Fi: read wireless networks cached by Windows. No external tools required.",
        ["1. Elenca sorgenti"] = "1. List sources",
        ["Leggi reti Wi-Fi"] = "Read Wi-Fi networks",
        ["Interrompi lettura"] = "Stop reading",
        ["Sorgente:"] = "Source:",
        ["2. Cattura per 20 s"] = "2. Capture for 20 s",
        ["Scegli Elenca sorgenti Windows oppure Leggi reti Wi-Fi. Nessuna raccolta automatica."] =
            "Choose List sources or Read Wi-Fi networks. No automatic background capture.",
        ["Che cosa posso vedere?"] = "What can this inspect?",
        ["Osservazioni di rete"] = "Network observations",
        ["Protocollo"] = "Protocol",
        ["Sorgente"] = "Source",
        ["Destinazione"] = "Destination",
        ["Direzione"] = "Direction",
        ["Byte campionati"] = "Sampled bytes",
        ["Componente (se disponibile)"] = "Component (if available)",
        ["Codice scarto"] = "Drop code",
        ["Cache radio Wi-Fi"] = "Wi-Fi radio cache",
        ["Frequenza"] = "Frequency",
        ["Tipo"] = "Type",
        ["Seleziona una riga per leggere i dati e il loro significato."] = "Select a row to inspect its details and meaning.",
        ["Esito della raccolta, errori e copertura"] = "Capture outcome, errors, and coverage",
        ["Nessuna raccolta eseguita. Nessun file di cattura viene creato automaticamente."] = "No capture run yet. No capture file is created automatically.",
        ["Eventi e blocchi registrati sul PC"] = "Security Events & Firewall Blocks on PC",
        ["Leggi il registro Security, poi seleziona una riga: sotto trovi significato, dati registrati e verifiche utili."] =
            "Read the Windows Security log, then select a row to inspect its meaning and recorded fields.",
        ["Ultimi 15 minuti"] = "Last 15 minutes",
        ["Ultima ora"] = "Last hour",
        ["Includi scarti di pacchetti (5152)"] = "Include dropped packets (Event 5152)",
        ["Legge gli scarti già registrati. Non abilita l'audit e non crea nuovi eventi."] = "Reads already-logged packet drops. Does not alter Windows audit policy.",
        ["Leggi eventi Windows"] = "Read Windows events",
        ["Conteggi N/D · nessuna lettura"] = "Counts N/A · no reading yet",
        ["Fonti, copertura e motivo degli eventuali dati mancanti"] = "Sources, coverage, and reasons for missing events",
        ["Configurazione audit: N/D"] = "Audit configuration: N/A",
        ["Come attivare la raccolta"] = "How to enable WFP event auditing",
        ["Leggi gli eventi per visualizzare i comandi di configurazione."] = "Read events to display audit configuration commands.",
        ["IP sorgente"] = "Source IP",
        ["IP destinazione"] = "Destination IP",
        ["Porta dest."] = "Dest. port",
        ["PID all'epoca"] = "PID at event time",
        ["Nessuna lettura eseguita. Premi Leggi eventi Windows."] = "No reading performed yet. Press Read Windows events.",
        ["Seleziona un evento per leggere i dettagli."] = "Select an event to inspect its details.",

        // New Task Window
        ["Esegui"] = "Run",
        ["Annulla"] = "Cancel",
        ["Sfoglia…"] = "Browse…",
        ["Scegli il programma da avviare. Inserisci le opzioni nel campo Argomenti."] =
            "Choose the program to start. Enter command-line switches in the Arguments box.",
        ["Programma"] = "Program",
        ["Nome, per esempio notepad.exe, oppure percorso completo del programma."] =
            "Executable name (e.g. notepad.exe) or full path to the program.",
        ["Argomenti (facoltativi)"] = "Arguments (optional)",
        ["Argomenti esatti, comprese le virgolette dei percorsi con spazi. Non aggiungere qui il nome del programma."] =
            "Exact arguments, including quotes for paths with spaces. Do not repeat the executable name here.",
        ["Cartella iniziale (facoltativa)"] = "Working directory (optional)",
        ["Cartella di lavoro del programma. Se vuota, usa la cartella corrente di ProcessLens."] =
            "Working folder for the started process. If empty, uses SysPulse's current directory.",
        ["Privilegi: quelli di ProcessLens, quindi amministratore nell’avvio normale."] =
            "Privileges: inherits SysPulse privileges (Administrator in standard launch).",
        ["Per comandi di shell o script, scegli cmd.exe, powershell.exe o l’interprete necessario. Nessuna cronologia dei comandi salvata da questa finestra."] =
            "For shell commands or scripts, run cmd.exe, powershell.exe, or the appropriate interpreter. No command history is saved.",
        ["Scegli il programma"] = "Select program",
        ["Avvio in corso…"] = "Starting process…"
    };

    private static readonly (string It, string En)[] PhraseReplacements =
    {
        // Structural headers in ProcessBehaviorExplainer & ProcessQueryEngine
        ("OPERAZIONE IN CORSO", "CURRENT OPERATION"),
        ("DETTAGLIO DEL LAVORO E PARAMETRI", "WORK DETAILS & PARAMETERS"),
        ("SOTTO-PROCESSI E GERARCHIA", "SUB-PROCESSES & HIERARCHY"),
        ("ATTIVITÀ OSSERVATA ADESSO", "CURRENTLY OBSERVED ACTIVITY"),
        ("CATENA DI AVVIO", "STARTUP CHAIN"),
        ("LIMITI", "LIMITATIONS"),
        ("■ DIAGNOSI IN BREVE", "■ QUICK DIAGNOSIS"),
        ("■ VALUTAZIONE: È UN CONSUMO ECCESSIVO?", "■ EVALUATION: IS MEMORY USAGE EXCESSIVE?"),
        ("■ COME SI DISTRIBUISCE LA MEMORIA", "■ MEMORY DISTRIBUTION BREAKDOWN"),
        ("■ ANDAMENTO MISURATO NEL TEMPO", "■ MEASURED MEMORY TREND OVER TIME"),
        ("■ MAPPA DELLA MEMORIA VIRTUALE", "■ VIRTUAL MEMORY MAP (VirtualQueryEx)"),
        ("■ COMPONENTI TIPICI · NON MISURATI", "■ TYPICAL RUNTIME COMPONENTS"),
        ("■ QUANDO APPROFONDIRE E STRUMENTI CONSIGLIATI", "■ WHEN TO INVESTIGATE & RECOMMENDED TOOLS"),
        ("■ COME APPROFONDIRE", "■ HOW TO INVESTIGATE FURTHER"),
        ("DETTAGLIO RETE DEL PROCESSO SELEZIONATO", "NETWORK DETAILS FOR SELECTED PROCESS"),
        ("PROCESSI CORRELATI PER NOME E ALBERO", "RELATED PROCESSES BY NAME & TREE"),
        ("SOCKET OSSERVATI — proprietario PID, indirizzo locale e destinazione", "OBSERVED SOCKETS — owner PID, local address, and remote endpoint"),

        // Dynamic UI phrases & status lines
        ("Vista: tutti i processi", "View: all processes"),
        ("Vista: applicazioni con finestra o uso diretto", "View: interactive applications"),
        ("Vista: strumenti di sviluppo, terminali, compilatori e runtime", "View: development tools, terminals, compilers, and runtimes"),
        ("Vista: componenti e host di Windows", "View: Windows system components and hosts"),
        ("Vista: processi di supporto, helper e servizi in background", "View: background helpers and support services"),
        ("Vista: sottosistema Windows per Linux (WSL) e macchine virtuali", "View: Windows Subsystem for Linux (WSL) and virtual machines"),
        ("gruppi", "groups"),
        ("Stato raccolta: lettura approfondita in corso...", "Collection status: deep inspection in progress..."),
        ("Stato raccolta: completata", "Collection status: completed"),
        ("Stato raccolta: in attesa di selezione", "Collection status: waiting for selection"),
        ("Media 30 s:", "30s Avg:"),
        ("Picco 30 s:", "30s Peak:"),
        ("Core logici equiv.:", "Logical cores equiv.:"),
        ("Tempo CPU totale:", "Total CPU time:"),
        ("Non disponibile", "Unavailable"),

        // Role & Classification phrases
        ("Applicazione a cui appartiene:", "Parent Application / Suite:"),
        ("Ruolo nel programma:", "Process Role:"),
        ("Tecnologia / runtime:", "Technology / Runtime:"),
        ("Spiegazione:", "Explanation:"),
        ("Stato di esecuzione:", "Execution State:"),
        ("Finestra principale:", "Main Window:"),
        ("Connessioni di rete del PID:", "PID Network Connections:"),
        ("Impatto se lo chiudi:", "Impact if terminated:"),
        ("Rischio chiusura:", "Termination Risk:"),
        ("Sottosistemi rilevati dai moduli caricati:", "Subsystems detected from loaded modules:"),
        ("Origine verificata dall'orario di avvio:", "Origin verified by start timestamp:"),
        ("Possibile processo padre:", "Candidate parent process:"),
        ("Nessun processo padre attivo nello snapshot", "No active parent process in current snapshot"),
        ("Nessun figlio attivo nello snapshot.", "No active child processes in current snapshot."),
        ("Figli attivi nello snapshot", "Active child processes in snapshot"),
        ("Impatto elevato — componente di sistema o servizio critico", "High risk — critical system component or service"),
        ("Impatto medio — possibili effetti su app o lavoro in corso", "Medium risk — may interrupt active app or unsaved work"),
        ("Impatto contenuto — processo secondario o riavviabile", "Low risk — secondary or restartable helper process"),
        ("Impatto da verificare prima della chiusura", "Verify active tasks before terminating"),
        ("Identificazione dell'eseguibile", "Executable identification"),
        ("Applicazione associata", "Associated application"),
        ("Funzione del processo", "Process function"),
        ("Ambiente di esecuzione", "Execution environment"),
        ("Origine del processo", "Process origin"),
        ("Identificativo della regola interna", "Internal classification rule ID"),
        ("Avvio e relazioni", "Startup and relationships"),
        ("Identità e applicazione", "Identity and application"),
        ("Funzione e ambiente di esecuzione", "Function and runtime environment"),
        ("Altre osservazioni", "Other observations"),
        ("Risorse", "Resources"),
        ("Relazioni", "Relationships"),
        ("Identità", "Identity"),
        ("Non disponibile o processo protetto", "Not available or protected process"),
        ("Non disponibile per questo processo", "Not available for this process"),
        ("Percorso non disponibile", "Path not available"),
        ("Nessuna finestra visibile", "No visible window"),
        ("Finestra visibile", "Visible window"),
        ("Finestra presente:", "Window title:"),
        ("Non risponde al momento della lettura", "Not responding at snapshot time"),
        ("Attivo — risponde ai messaggi di Windows", "Active — responding to Windows messages"),
        ("Senza interfaccia grafica diretta (processo in background o servizio)", "No direct GUI window (background process or service)"),

        // Network & Memory dynamic phrases
        ("Porte e connessioni: in attesa dell’analisi approfondita del processo selezionato.", "Ports and connections: waiting for deep inspection of the selected process."),
        ("Porte e connessioni del PID", "Ports and connections for PID"),
        ("TCP in ascolto (LISTEN):", "TCP listening (LISTEN):"),
        ("Connessioni TCP stabilite (ESTABLISHED):", "Established TCP connections (ESTABLISHED):"),
        ("Endpoint UDP:", "UDP endpoints:"),
        ("Connessioni TCP verso IP pubblici:", "TCP connections to public IPs:"),
        ("Porte TCP in ascolto:", "Listening TCP ports:"),
        ("Traffico attuale del PID:", "Current PID network throughput:"),
        ("Campione rete:", "Network sample:"),
        ("quattro tabelle lette", "all 4 OS socket tables read"),
        ("LETTURA PARZIALE", "PARTIAL READ"),
        ("PID con identità confrontata prima/dopo:", "PIDs with start-time identity verified before/after:"),
        ("Processi correlati per nome e albero", "Related processes by executable name and tree"),
        ("Processi con socket:", "Processes holding sockets:"),
        ("Connessioni TCP verso IP pubblici, totale del gruppo:", "Total group TCP connections to public IPs:"),
        ("Conteggi nelle letture disponibili: zero non garantisce l’assenza di socket. Richieste accettate e byte trasferiti non sono misurati.",
         "Socket counts from current OS tables: zero means no open sockets at snapshot time."),
        ("Esito e limiti di acquisizione", "Acquisition status and limitations"),
        ("Elenco campionato: fino a 40 endpoint del PID selezionato", "Sampled list: up to 40 endpoints for the selected PID"),
        ("Tendenza: in attesa di 3 campioni su almeno 20 s.", "Trend: waiting for 3 samples over at least 20 s."),
        ("Pagine fisiche residenti in RAM e allocate esclusivamente per questo processo", "Physical RAM pages resident and allocated exclusively to this process"),
        ("Working Set totale del processo (include pagine di DLL condivise con altri programmi)", "Total Working Set of the process (includes DLL pages shared with other programs)"),
        ("Working Set privato non disponibile; mostrato il Working Set totale (include memoria condivisa)", "Private Working Set unavailable; showing Total Working Set (includes shared memory)"),
        ("Contatori di memoria non accessibili per questo processo (possibile processo protetto del kernel/sistema)", "Memory counters inaccessible for this process (protected kernel/system process)"),
        ("Rete non ancora ispezionata per questo processo", "Network sockets not yet inspected for this process"),
        ("Nessuna socket TCP o UDP aperta da questo PID nell'istante campionato", "No open TCP or UDP sockets for this PID at snapshot time"),

        // Query Engine titles & confidence badges
        ("Perché occupa RAM?", "Why is it using RAM?"),
        ("Consumo CPU e significato", "CPU Usage & Meaning"),
        ("Consumo CPU", "CPU Usage"),
        ("Cosa sta facendo", "What it is doing"),
        ("Progresso", "Progress"),
        ("Tempo alla fine", "Estimated Time Remaining (ETA)"),
        ("È bloccato?", "Is it stuck or frozen?"),
        ("Origine e operazione", "Origin & Operation"),
        ("Perché è aperto", "Why it is running"),
        ("Se lo chiudo", "If you close it"),
        ("Risposta composta", "Combined Analysis"),
        ("DATI INSUFFICIENTI", "INSUFFICIENT DATA"),
        ("CONTATORI · REGIONI VIRTUALI · CAMPIONE PAGINE", "COUNTERS · VIRTUAL REGIONS · PAGE SAMPLE"),
        ("CPU OSSERVATA; CAUSA NON PROVATA", "OBSERVED CPU; INTERNAL FUNCTION NOT PROFILED"),
        ("NON DETERMINATO", "UNDETERMINED"),
        ("NON STIMABILE", "NOT ESTIMABLE"),
        ("NON DISPONIBILE", "UNAVAILABLE"),
        ("OSSERVATO", "OBSERVED"),
        ("CATENA COMPATIBILE PER ORARI", "TIMESTAMP-VERIFIED CHAIN"),
        ("CATENA PARZIALE", "PARTIAL CHAIN"),
        ("DATI DI AVVIO + TIPO SUGGERITO", "STARTUP TELEMETRY + CLASSIFIED ROLE"),
        ("EFFETTI STIMATI; STATO NON VERIFICATO", "ESTIMATED IMPACT; LIVE STATE UNVERIFIED"),
        ("CRITICO", "CRITICAL SYSTEM PROCESS"),
        ("Media nel periodo disponibile (fino a 30 s):", "Average over available window (up to 30 s):"),
        ("CPU nell’intervallo campionato:", "CPU in sampled interval:"),
        ("della capacità del PC.", "of total PC capacity."),
        ("Equivalente approssimativo:", "Approximate equivalent:"),
        ("core logici a pieno utilizzo", "logical cores at full load"),
        ("Possibili conseguenze secondo le regole (non verificate nel processo in corso). Rischio orientativo:", "Estimated consequences based on process role rules. Indicative risk:"),
        ("Nei campioni disponibili la CPU misurata è molto bassa, ma questo NON dimostra che il processo sia bloccato:", "In recent samples CPU usage is very low, which is normal for idle/waiting processes and does NOT prove a hang:"),
        ("potrebbe semplicemente essere in attesa di input, rete, timer o eventi. Per diagnosticare uno stallo servirebbero anche I/O e stato dei thread.", "it may simply be waiting for user input, network I/O, timers, or events."),
        ("Questo processo sembra essere un componente a lunga durata (servizio, renderer, host o processo principale).", "This process is a long-running component (service, renderer, host, or main application)."),
        ("Per questo non ha necessariamente una 'percentuale completata' o una fine prevista.", "Therefore it does not have a finite 'completion percentage' or scheduled end time.")
    };

    public static string GetLabel(string italianText, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (string.IsNullOrEmpty(italianText))
            return string.Empty;
        if (lang != AppLanguage.English)
            return italianText;
        if (Dictionary.TryGetValue(italianText, out string? exact))
            return exact;
        string trimmed = italianText.Trim();
        if (Dictionary.TryGetValue(trimmed, out string? trimmedMatch))
            return italianText.Replace(trimmed, trimmedMatch, StringComparison.Ordinal);
        return Translate(italianText, lang);
    }

    public static string Translate(string? text, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (lang != AppLanguage.English)
            return text;

        if (Dictionary.TryGetValue(text, out string? exact))
            return exact;

        string trimmed = text.Trim();
        if (Dictionary.TryGetValue(trimmed, out string? trimmedMatch))
            return text.Replace(trimmed, trimmedMatch, StringComparison.Ordinal);

        string result = text;
        foreach (var (it, en) in PhraseReplacements)
        {
            if (result.Contains(it, StringComparison.Ordinal))
                result = result.Replace(it, en, StringComparison.Ordinal);
        }
        return result;
    }

    public static string TerminologyEvidenceStrength(int ruleScore, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.English)
        {
            return ruleScore switch
            {
                >= 95 => "Very high match",
                >= 80 => "High match",
                >= 60 => "Medium match",
                >= 40 => "Low match",
                _ => "Insufficient match"
            };
        }
        return ruleScore switch
        {
            >= 95 => "Corrispondenza molto alta",
            >= 80 => "Corrispondenza alta",
            >= 60 => "Corrispondenza media",
            >= 40 => "Corrispondenza bassa",
            _ => "Corrispondenza insufficiente"
        };
    }

    public static string TerminologyFunctionLabel(int ruleScore, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.English)
        {
            return ruleScore switch
            {
                >= 95 => "Identified role",
                >= 80 => "Probable role",
                >= 60 => "Estimated role",
                _ => "Unknown function"
            };
        }
        return ruleScore switch
        {
            >= 95 => "Ruolo identificato",
            >= 80 => "Ruolo probabile",
            >= 60 => "Ruolo stimato",
            _ => "Funzione sconosciuta"
        };
    }

    public static string TerminologyEvidenceLevel(int ruleScore, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.English)
        {
            return ruleScore switch
            {
                >= 95 => "very high",
                >= 80 => "high",
                >= 60 => "moderate",
                >= 40 => "limited",
                _ => "insufficient"
            };
        }
        return ruleScore switch
        {
            >= 95 => "molto alta",
            >= 80 => "alta",
            >= 60 => "intermedia",
            >= 40 => "limitata",
            _ => "insufficiente"
        };
    }

    public static string TerminologyCategoryLabel(string category, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.English)
        {
            return (category ?? string.Empty).Trim() switch
            {
                "Applicazioni" or "Apps" => "Apps",
                "Sviluppo" or "Dev" => "Dev",
                "Windows" => "Windows",
                "Background" or "Secondario" => "Background",
                "WSL" => "WSL",
                _ => category ?? string.Empty
            };
        }
        return string.Equals(category, "Background", StringComparison.OrdinalIgnoreCase)
            ? "Secondario"
            : category ?? string.Empty;
    }

    public static string ClosingRiskLabel(string? risk, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        var r = (risk ?? string.Empty).Trim();
        if (lang == AppLanguage.English)
        {
            if (string.IsNullOrEmpty(r)) return "Impact not determined";
            if (r.Equals("CRITICO", StringComparison.OrdinalIgnoreCase) || r.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase)) return "Critical for Windows";
            if (r.Equals("ALTO", StringComparison.OrdinalIgnoreCase) || r.Equals("ELEVATO", StringComparison.OrdinalIgnoreCase) || r.Equals("HIGH", StringComparison.OrdinalIgnoreCase)) return "High impact";
            if (r.Equals("MEDIO", StringComparison.OrdinalIgnoreCase) || r.Equals("MODERATO", StringComparison.OrdinalIgnoreCase) || r.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase)) return "Medium impact";
            if (r.Equals("BASSO", StringComparison.OrdinalIgnoreCase) || r.Equals("MINIMO", StringComparison.OrdinalIgnoreCase) || r.Equals("LOW", StringComparison.OrdinalIgnoreCase)) return "Low impact";
            if (r.Contains('/')) return "Variable effects";
            return Translate(r, lang);
        }

        if (string.IsNullOrEmpty(r)) return "Impatto non determinato";
        if (r.Equals("CRITICO", StringComparison.OrdinalIgnoreCase)) return "Critico per Windows";
        if (r.Equals("ALTO", StringComparison.OrdinalIgnoreCase)) return "Impatto alto";
        if (r.Equals("MEDIO", StringComparison.OrdinalIgnoreCase)) return "Impatto medio";
        if (r.Equals("BASSO", StringComparison.OrdinalIgnoreCase)) return "Impatto basso";
        if (r.Contains('/')) return "Effetti variabili";
        return r;
    }

    /// <summary>
    /// Builds a dedicated, comprehensive English technical profile of a process for the bilingual analysis panel.
    /// This section is always generated in English so users have both the Italian analysis and the full English analysis.
    /// </summary>
    public static string BuildEnglishProcessAnalysis(
        ProcessSnapshot process,
        ProcessInsight insight,
        string? identifiedOperation = null,
        string? loadedSubsystems = null)
    {
        var sb = new StringBuilder();
        string appName = string.IsNullOrWhiteSpace(insight.OwnerApp) ? process.Name : insight.OwnerApp;
        string roleEn = TranslateRoleToEnglish(process, insight);
        string runtimeEn = TranslateRuntimeToEnglish(insight.Framework);
        string riskEn = TranslateRiskToEnglish(insight.CloseRisk, process.IsCritical);

        sb.AppendLine($"Application / Suite: {appName} (PID {process.Pid})");
        sb.AppendLine($"Classified Role: {roleEn}");
        sb.AppendLine($"Runtime & Architecture: {runtimeEn}");

        if (!string.IsNullOrWhiteSpace(process.FileDescription))
            sb.AppendLine($"Binary Description: {process.FileDescription}");
        if (!string.IsNullOrWhiteSpace(process.CompanyName))
            sb.AppendLine($"Publisher / Metadata: {process.CompanyName}");
        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
            sb.AppendLine($"Executable Path: {process.ExecutablePath}");

        sb.AppendLine();
        sb.AppendLine("WHAT THIS PROCESS IS DOING RIGHT NOW");
        if (!string.IsNullOrWhiteSpace(identifiedOperation))
            sb.AppendLine($"• Active Workload: {TranslateWorkloadToEnglish(identifiedOperation)}");
        else
            sb.AppendLine($"• Primary Function: {DescribeFunctionInEnglish(process, insight)}");

        if (process.CpuSampleAvailable)
        {
            string cpuState = process.CpuPercent switch
            {
                >= 15.0 => $"High CPU activity ({process.CpuPercent:F1}% current, {process.CpuAverage30Seconds:F1}% 30s avg, ~{process.CpuLogicalCoreEquivalent:F2} logical cores).",
                >= 2.0 => $"Moderate CPU activity ({process.CpuPercent:F1}% current, {process.CpuAverage30Seconds:F1}% 30s avg).",
                >= 0.2 => $"Light background CPU activity ({process.CpuPercent:F1}% current).",
                _ => $"Idle / waiting for events ({process.CpuPercent:F1}% CPU in current sample)."
            };
            sb.AppendLine($"• CPU Telemetry: {cpuState}");
        }
        else
        {
            sb.AppendLine("• CPU Telemetry: Waiting for second comparison sample or protected process.");
        }

        if (process.PrivateWorkingSetAvailable || process.WorkingSetAvailable)
        {
            string memLine = process.PrivateWorkingSetAvailable
                ? $"Private Working Set: {process.PrivateWorkingSetMb:F1} MiB"
                : $"Total Working Set: {process.WorkingSetMb:F1} MiB";
            if (process.WorkingSetAvailable && process.PrivateWorkingSetAvailable)
                memLine += $" | Total Working Set: {process.WorkingSetMb:F1} MiB";
            if (process.PrivateCommitAvailable)
                memLine += $" | Private Commit: {process.PrivateMb:F1} MiB";
            sb.AppendLine($"• Memory Footprint: {memLine}");
        }

        if (process.IsDeepInspected)
        {
            sb.AppendLine($"• Network Activity: {process.TcpEstablishedCount} established TCP connections ({process.PublicRemoteConnectionCount} public), " +
                          $"{process.TcpListeningCount} listening TCP ports, {process.UdpEndpointCount} UDP endpoints.");
        }

        if (!string.IsNullOrWhiteSpace(loadedSubsystems))
            sb.AppendLine($"• Loaded Native/Managed Subsystems: {loadedSubsystems}");

        var services = process.ServiceDisplayNames.Count > 0 ? process.ServiceDisplayNames : process.ServiceNames;
        if (services.Count > 0)
            sb.AppendLine($"• Hosted Windows Services (SCM): {string.Join(", ", services)}");

        sb.AppendLine();
        sb.AppendLine("PROVENANCE & TERMINATION IMPACT");
        if (process.AncestorDetails.Count > 0)
        {
            var parent = process.AncestorDetails[0];
            string verified = parent.VerifiedByStartTime ? "verified by creation timestamp" : "unverified timestamp";
            sb.AppendLine($"• Parent Process: {parent.Name} (PID {parent.Pid}, {verified})");
        }
        else if (process.ParentPid > 0)
        {
            sb.AppendLine($"• Parent PID: {process.ParentPid} ({process.ParentName ?? "no longer active in snapshot"})");
        }

        if (process.ChildCount > 0)
            sb.AppendLine($"• Active Child Processes: {process.ChildCount} total ({process.SameNameChildCount} with same executable name)");

        sb.AppendLine($"• Termination Risk: {riskEn}");

        return sb.ToString().TrimEnd();
    }

    private static string TranslateRoleToEnglish(ProcessSnapshot p, ProcessInsight i)
    {
        string r = i.Role ?? string.Empty;
        if (r.Contains("Kernel", StringComparison.OrdinalIgnoreCase)) return "Windows NT Kernel & Executive";
        if (r.Contains("Gestore sessioni", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("smss", StringComparison.OrdinalIgnoreCase)) return "Windows Session Manager (SMSS)";
        if (r.Contains("Sottosistema Win32", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("csrss", StringComparison.OrdinalIgnoreCase)) return "Client/Server Runtime Subsystem (CSRSS)";
        if (r.Contains("Autorità di sicurezza", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("lsass", StringComparison.OrdinalIgnoreCase)) return "Local Security Authority Subsystem Service (LSASS)";
        if (r.Contains("Service Control Manager", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("services", StringComparison.OrdinalIgnoreCase)) return "Windows Service Control Manager (SCM)";
        var services = p.ServiceDisplayNames.Count > 0 ? p.ServiceDisplayNames : p.ServiceNames;
        if (r.Contains("Host di servizi Windows", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("svchost", StringComparison.OrdinalIgnoreCase))
            return services.Count > 0
                ? $"Windows Generic Service Host ({string.Join(", ", services.Take(4))})"
                : "Windows Generic Service Host (svchost.exe)";
        if (r.Contains("Compositore", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("dwm", StringComparison.OrdinalIgnoreCase)) return "Desktop Window Manager Compositor (DWM)";
        if (r.Contains("Shell grafica", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("explorer", StringComparison.OrdinalIgnoreCase)) return "Windows Graphical Shell & File Explorer";
        if (r.Contains("Renderer", StringComparison.OrdinalIgnoreCase)) return "Sandboxed Web/UI Renderer Process";
        if (r.Contains("GPU", StringComparison.OrdinalIgnoreCase)) return "Hardware Graphics & GPU Compositor Process";
        if (r.Contains("Servizio di rete", StringComparison.OrdinalIgnoreCase)) return "Dedicated Network & HTTP/TLS Service Process";
        if (r.Contains("Storage", StringComparison.OrdinalIgnoreCase)) return "Isolated Storage / IndexedDB / Cache Service";
        if (r.Contains("Audio", StringComparison.OrdinalIgnoreCase)) return "Isolated Audio Playback/Capture Process";
        if (r.Contains("Crashpad", StringComparison.OrdinalIgnoreCase) || r.Contains("Segnalazione", StringComparison.OrdinalIgnoreCase)) return "Crash Telemetry & Dump Handler";
        if (r.Contains("Compilatore", StringComparison.OrdinalIgnoreCase)) return "Source Code Compiler / Build Toolchain";
        if (r.Contains("Language Server", StringComparison.OrdinalIgnoreCase) || r.Contains("Analizzatore", StringComparison.OrdinalIgnoreCase)) return "IDE Language Server & Static Code Analyzer";
        if (r.Contains("Contenitore", StringComparison.OrdinalIgnoreCase) || r.Contains("WSL", StringComparison.OrdinalIgnoreCase)) return "WSL2 / Virtualization Subsystem Service";
        if (r.Contains("Antimalware", StringComparison.OrdinalIgnoreCase) || r.Contains("Defender", StringComparison.OrdinalIgnoreCase)) return "Endpoint Security & Antimalware Engine";
        if (r.Contains("Database", StringComparison.OrdinalIgnoreCase)) return "Database Engine / Data Store Service";
        if (r.Contains("Server web", StringComparison.OrdinalIgnoreCase)) return "Web / Application Server";
        if (r.Contains("Terminale", StringComparison.OrdinalIgnoreCase) || r.Contains("Shell", StringComparison.OrdinalIgnoreCase)) return "Command-Line Shell / Terminal Host";
        if (r.Contains("Principale", StringComparison.OrdinalIgnoreCase) || r.Contains("Coordinatore", StringComparison.OrdinalIgnoreCase)) return "Primary Application Coordinator Process";
        return string.IsNullOrWhiteSpace(r) ? "Application / System Process" : r;
    }

    private static string TranslateRuntimeToEnglish(string? framework)
    {
        if (string.IsNullOrWhiteSpace(framework)) return "Native Win32 / C++";
        return framework
            .Replace("Nativo Windows", "Native Win32 / C++", StringComparison.OrdinalIgnoreCase)
            .Replace("Componente di sistema Windows", "Windows Core OS Component", StringComparison.OrdinalIgnoreCase)
            .Replace("Sottosistema", "Subsystem", StringComparison.OrdinalIgnoreCase);
    }

    private static string TranslateRiskToEnglish(string? risk, bool isCritical)
    {
        if (isCritical)
            return "CRITICAL — Terminating this OS process causes an immediate Windows system crash (BSOD).";
        string norm = (risk ?? string.Empty).Trim().ToUpperInvariant();
        return norm switch
        {
            "CRITICO" or "ALTO" or "ELEVATO" => "High — Stopping this process can disrupt essential Windows features, active sessions, or system stability.",
            "MEDIO" or "MODERATO" => "Medium — Closing this process will terminate the active application/session and may cause loss of unsaved data.",
            "BASSO" or "MINIMO" => "Low — Helper or background worker process; typically restarted on demand by its parent application or service host.",
            _ => "Moderate — Verify open files and active tasks before terminating."
        };
    }

    private static string TranslateWorkloadToEnglish(string italianWorkload)
    {
        return italianWorkload
            .Replace("Render di una scheda, vista web o finestra secondaria in sandbox", "Sandboxed rendering of a web tab, UI view, or extension frame", StringComparison.OrdinalIgnoreCase)
            .Replace("Composizione grafica, rasterizzazione e accelerazione hardware", "Hardware graphics composition, rasterization, and video decoding", StringComparison.OrdinalIgnoreCase)
            .Replace("Gestione delle connessioni HTTP/HTTPS, DNS, cache e socket", "Managing HTTP/HTTPS requests, DNS resolution, TLS sockets, and network cache", StringComparison.OrdinalIgnoreCase)
            .Replace("Servizio secondario isolato", "Isolated utility service worker", StringComparison.OrdinalIgnoreCase)
            .Replace("Monitoraggio dei crash e raccolta dump", "Crash dump monitoring and diagnostics handler", StringComparison.OrdinalIgnoreCase)
            .Replace("Ospita i servizi Windows:", "Hosting Windows services:", StringComparison.OrdinalIgnoreCase)
            .Replace("Gruppo di servizi Windows:", "Windows service group (-k):", StringComparison.OrdinalIgnoreCase)
            .Replace("Esecuzione dello script:", "Executing script:", StringComparison.OrdinalIgnoreCase)
            .Replace("Esecuzione del modulo Python:", "Running Python module:", StringComparison.OrdinalIgnoreCase)
            .Replace("Comando inline passato", "Executing inline command from CLI", StringComparison.OrdinalIgnoreCase)
            .Replace("Operazione .NET CLI:", "Running .NET CLI command:", StringComparison.OrdinalIgnoreCase)
            .Replace("Esecuzione dell'applicazione .NET:", "Running .NET application:", StringComparison.OrdinalIgnoreCase)
            .Replace("Operazione Git:", "Executing Git operation:", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeFunctionInEnglish(ProcessSnapshot p, ProcessInsight i)
    {
        var services = p.ServiceDisplayNames.Count > 0 ? p.ServiceDisplayNames : p.ServiceNames;
        if (services.Count > 0)
            return $"Hosts Windows background services ({string.Join(", ", services)}) managed by the Service Control Manager.";
        if (p.HasVisibleWindow && p.WindowTitles.Count > 0 && !string.IsNullOrWhiteSpace(p.WindowTitles[0]))
            return $"Interactive desktop application displaying window '{p.WindowTitles[0]}'.";
        string owner = string.IsNullOrWhiteSpace(i.OwnerApp) ? p.Name : i.OwnerApp;
        if (!string.IsNullOrWhiteSpace(p.FileDescription))
            return $"Executes '{p.FileDescription}' ({p.Name}.exe) as part of {owner}.";
        return $"Runs {p.Name}.exe ({i.Framework}) supporting {owner}.";
    }
}
