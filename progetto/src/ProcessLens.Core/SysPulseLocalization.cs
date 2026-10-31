using System;
using System.Collections.Generic;
using System.IO;

namespace ProcessLens.Core;

public enum AppLanguage
{
    Italian,
    English
}

/// <summary>
/// Central localization manager for SysPulse. Provides complete dual-language
/// support (Italian and English) across terminology, UI components, process
/// intelligence analysis, and diagnostic summaries.
/// </summary>
public static class SysPulseLocalization
{
    private static AppLanguage _current = AppLanguage.Italian;
    private static readonly object _sync = new();

    static SysPulseLocalization()
    {
        try
        {
            string settingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
            string settingsFile = Path.Combine(settingsFolder, "language.txt");
            if (File.Exists(settingsFile))
            {
                string text = File.ReadAllText(settingsFile).Trim();
                if (text.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("english", StringComparison.OrdinalIgnoreCase))
                {
                    _current = AppLanguage.English;
                }
            }
        }
        catch
        {
            // Fallback gracefully to Italian
            _current = AppLanguage.Italian;
        }
    }

    public static event Action<AppLanguage>? LanguageChanged;

    public static AppLanguage CurrentLanguage
    {
        get
        {
            lock (_sync) return _current;
        }
        set
        {
            lock (_sync)
            {
                if (_current == value) return;
                _current = value;
            }

            try
            {
                string settingsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
                Directory.CreateDirectory(settingsFolder);
                string settingsFile = Path.Combine(settingsFolder, "language.txt");
                File.WriteAllText(settingsFile, value == AppLanguage.English ? "en" : "it");
            }
            catch
            {
                // Silently ignore configuration save errors
            }

            LanguageChanged?.Invoke(value);
        }
    }

    public static void InitializeFromArgs(string[]? args)
    {
        if (args is null) return;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim();
            if (arg.Equals("--lang=en", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-lang=en", StringComparison.OrdinalIgnoreCase))
            {
                CurrentLanguage = AppLanguage.English;
                return;
            }
            if (arg.Equals("--lang=it", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-lang=it", StringComparison.OrdinalIgnoreCase))
            {
                CurrentLanguage = AppLanguage.Italian;
                return;
            }
            if ((arg.Equals("--lang", StringComparison.OrdinalIgnoreCase) ||
                 arg.Equals("-lang", StringComparison.OrdinalIgnoreCase) ||
                 arg.Equals("/lang", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length)
            {
                string val = args[i + 1].Trim().ToLowerInvariant();
                if (val is "en" or "english" or "eng")
                {
                    CurrentLanguage = AppLanguage.English;
                    return;
                }
                if (val is "it" or "italian" or "ita")
                {
                    CurrentLanguage = AppLanguage.Italian;
                    return;
                }
            }
        }
    }

    public static string Translate(string italianText, AppLanguage? language = null)
    {
        var targetLang = language ?? CurrentLanguage;
        if (targetLang == AppLanguage.Italian || string.IsNullOrWhiteSpace(italianText))
            return italianText;

        if (Dictionary.TryGetValue(italianText, out string? translated))
            return translated;

        // Dynamic phrase replacements for generated sentences
        string result = italianText;
        foreach (var pair in PhraseReplacements)
        {
            if (result.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                result = result.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    public static string TerminologyFunctionLabel(int score, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.Italian)
        {
            return score switch
            {
                >= 95 => "Ruolo identificato",
                >= 80 => "Ruolo probabile",
                >= 60 => "Ruolo stimato",
                _ => "Funzione sconosciuta"
            };
        }
        return score switch
        {
            >= 95 => "Identified role",
            >= 80 => "Probable role",
            >= 60 => "Estimated role",
            _ => "Unknown function"
        };
    }

    public static string TerminologyEvidenceStrength(int score, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.Italian)
        {
            return score switch
            {
                >= 95 => "Corrispondenza molto alta",
                >= 80 => "Corrispondenza alta",
                >= 60 => "Corrispondenza media",
                >= 40 => "Corrispondenza bassa",
                _ => "Corrispondenza insufficiente"
            };
        }
        return score switch
        {
            >= 95 => "Very high match",
            >= 80 => "High match",
            >= 60 => "Medium match",
            >= 40 => "Low match",
            _ => "Insufficient match"
        };
    }

    public static string TerminologyEvidenceLevel(int score, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.Italian)
        {
            return score switch
            {
                >= 95 => "molto alta",
                >= 80 => "alta",
                >= 60 => "intermedia",
                >= 40 => "limitata",
                _ => "insufficiente"
            };
        }
        return score switch
        {
            >= 95 => "very high",
            >= 80 => "high",
            >= 60 => "intermediate",
            >= 40 => "limited",
            _ => "insufficient"
        };
    }

    public static string TerminologyCategoryLabel(string category, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.Italian)
        {
            return string.Equals(category, "Background", StringComparison.OrdinalIgnoreCase)
                ? "Secondario"
                : category;
        }
        return category switch
        {
            "Background" or "Secondario" => "Background",
            "Sviluppo" => "Development",
            "Applicazioni" => "Applications",
            "Windows" => "Windows",
            "WSL" => "WSL",
            _ => category
        };
    }

    public static string ClosingRiskLabel(string? risk, AppLanguage? language = null)
    {
        var lang = language ?? CurrentLanguage;
        if (lang == AppLanguage.Italian)
        {
            return risk?.ToUpperInvariant() switch
            {
                "CRITICO" => "Rischio critico",
                "ALTO" or "MEDIO / ALTO" => "Rischio elevato o variabile",
                "MEDIO" or "BASSO / MEDIO" => "Effetti variabili",
                "BASSO" => "Possibili effetti limitati",
                _ => "Effetti da verificare"
            };
        }
        return risk?.ToUpperInvariant() switch
        {
            "CRITICO" or "CRITICAL" => "Critical risk",
            "ALTO" or "HIGH" or "MEDIO / ALTO" or "MEDIUM / HIGH" => "High or variable risk",
            "MEDIO" or "MEDIUM" or "BASSO / MEDIO" or "LOW / MEDIUM" => "Variable effects",
            "BASSO" or "LOW" => "Possible limited effects",
            _ => "Effects to verify"
        };
    }

    private static readonly Dictionary<string, string> Dictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        // App titles and headers
        { "ProcessLens", "SysPulse" },
        { "Dati Windows e analisi del processo", "Windows telemetry and process analysis" },
        { "Nascondi accanto all’orologio", "Hide to tray" },
        { "Log finestra", "Window log" },
        { "Esci", "Exit" },
        { "Esegui nuova attività", "Run new task" },
        { "Cerca", "Search" },
        { "Cerca per nome visualizzato, eseguibile o PID esatto. Ctrl+F per scrivere.", "Search by display name, executable or exact PID. Ctrl+F to type." },

        // Navigation
        { "Processi", "Processes" },
        { "Sistema Windows", "Windows System" },
        { "Servizi", "Services" },
        { "Guardian", "Guardian" },
        { "Rete", "Network" },
        { "Memoria RAM", "RAM Memory" },

        // Filters
        { "Tutti", "All" },
        { "App", "Apps" },
        { "Sviluppo", "Dev" },
        { "Windows", "Windows" },
        { "Secondari", "Background" },
        { "WSL", "WSL" },
        { "Attivi", "Running" },
        { "Arrestati", "Stopped" },

        // Counters
        { "Rilevati: ", "Detected: " },
        { "Mostrati: ", "Shown: " },
        { "CPU sistema: ", "System CPU: " },
        { "Media fino a 30 s: ", "30s Avg: " },
        { "CPU logici: ", "Logical CPUs: " },
        { "RAM sistema: ", "System RAM: " },
        { "In attesa…", "Waiting…" },
        { "Risorse di ProcessLens · CPU e RAM della GUI", "SysPulse Resources · GUI CPU & RAM" },
        { "Copia risorse GUI", "Copy GUI resources" },

        // Process list
        { "Seleziona un processo per analizzarlo.", "Select a process to inspect its telemetry." },
        { "Vista: tutti i processi", "View: all processes" },
        { "Ordina", "Sort" },
        { "Raggruppa app", "Group apps" },
        { "Più recenti", "Newest" },
        { "Più vecchi", "Oldest" },
        { "Memoria", "Memory" },
        { "Nome", "Name" },
        { "WS PRIVATO", "PRIVATE WS" },
        { "RAM RESIDENTE", "RESIDENT RAM" },
        { "RAM N/D", "RAM N/A" },
        { "N/D", "N/A" },
        { "Uso CPU elevato", "High CPU usage" },
        { "Uso CPU moderato", "Moderate CPU usage" },
        { "CPU N/D", "CPU N/A" },
        { "In esecuzione da ", "Running for " },
        { "Avvio: ", "Started: " },
        { "Avvio: non disponibile", "Start time: not available" },
        { "Durata non disponibile", "Uptime not available" },
        { "Origine non disponibile", "Origin not available" },
        { "Avviato da: ", "Started by: " },

        // Process details
        { "Analisi del processo", "Process Analysis" },
        { "Espandi dettagli", "Expand details" },
        { "Mostra elenco", "Show list" },
        { "RUOLO STIMATO", "ESTIMATED ROLE" },
        { "IDENTITÀ E CLASSIFICAZIONE", "IDENTITY & CLASSIFICATION" },
        { "ELEMENTI DI IDENTIFICAZIONE", "IDENTIFICATION ELEMENTS" },
        { "SE TERMINI IL PROCESSO", "IF YOU TERMINATE THIS PROCESS" },
        { "Come è stato riconosciuto", "How it was recognized" },
        { "Rapporto diagnostico originale", "Original diagnostic report" },
        { "Dati originali completi", "Complete original data" },
        { "Diagnostica completa in formato originale", "Full diagnostics in raw format" },
        { "Cosa fa", "What it does" },
        { "Perché è in esecuzione", "Why it is running" },
        { "Valutazione del rischio", "Risk assessment" },
        { "Termina processo", "End task" },
        { "Termina albero processi", "End process tree" },
        { "Apri percorso file", "Open file location" },
        { "Copia dettagli", "Copy details" },
        { "Ispezione approfondita", "Deep inspection" },
        { "Proprietà file", "File properties" },
        { "Accesso non disponibile", "Access not available" },
        { "Non identificato", "Not identified" },

        // Classification facts
        { "File eseguibile", "Executable file" },
        { "Nome proposto", "Suggested name" },
        { "Associazione proposta", "Suggested association" },
        { "Tipo suggerito", "Suggested type" },
        { "Corrispondenza alle regole", "Rule match strength" },
        { "Dati utilizzati", "Evidence source" },
        { "Parametro riconosciuto", "Recognized parameter" },
        { "Limite dell'analisi", "Analysis limit" },
        { "Attività attuale non verificata dalla classificazione.", "Live activity is not verified by classification." },
        { "Elementi registrati nel rapporto tecnico", "Technical report records" },
        { "Valutazione interna", "Internal assessment" },
        { "Punteggio assegnato dalle regole", "Score assigned by rules" },
        { "Significato del risultato", "Result meaning" },
        { "Corrispondenza alle regole. Attività attuale non misurata.", "Rule match. Current activity not measured." },
        { "Altre osservazioni", "Other observations" },
        { "Altri dati disponibili", "Other data available" },
        { "Non specificato", "Not specified" },
        { "Avvio e relazioni", "Startup and relationships" },
        { "Identità e applicazione", "Identity and application" },
        { "Funzione e ambiente di esecuzione", "Function and runtime environment" },

        // Generic roles
        { "Processo critico di sistema", "Critical system process" },
        { "Componente essenziale di Windows", "Essential Windows component" },
        { "Processo di servizio Windows", "Windows service process" },
        { "Elaborazione contenuti web", "Web content rendering" },
        { "Processo GPU", "GPU process" },
        { "Processo ausiliario", "Utility process" },
        { "Processo ausiliario Node.js", "Node.js utility process" },
        { "Servizio di rete", "Network service" },
        { "Servizio audio", "Audio service" },
        { "Servizio storage", "Storage service" },
        { "Servizio video", "Video service" },
        { "Servizio decodifica dati", "Data decoder service" },
        { "Risolutore proxy", "Proxy resolver" },
        { "Servizio tracing/diagnostica", "Tracing/diagnostics service" },
        { "Servizio multimediale", "Media service" },
        { "Host dell'interfaccia", "User interface host" },
        { "Server / servizio in ascolto", "Listening server / service" },
        { "Processo con interfaccia utente", "Process with user interface" },
        { "Connessioni TCP rilevate", "TCP connections detected" },
        { "Sottoprocesso dell'applicazione", "Application subprocess" },
        { "Possibile processo principale", "Possible main process" },
        { "Processo applicativo", "Application process" },
        { "Funzione sconosciuta", "Unknown function" },
        { "Non classificato", "Unclassified" },
        { "Processo non classificato", "Unclassified process" },
        { "Servizio o processo host in ascolto", "Listening service or host process" },
        { "Processo Windows/di sistema", "Windows / system process" },

        // Specific Process Roles & Titles (IT -> EN)
        { "Kernel NT e sottosistema di sistema", "NT Kernel and System Subsystem" },
        { "Componente principale di Windows (PID 4)", "Core Windows Kernel Component (PID 4)" },
        { "Sottosistema gestione sessioni (smss)", "Session Manager Subsystem (smss)" },
        { "Sottosistema di runtime Client/Server (csrss)", "Client/Server Runtime Subsystem (csrss)" },
        { "Inizializzazione di sistema (wininit)", "Windows Initialization Process (wininit)" },
        { "Gestore controllo servizi (SCM)", "Service Control Manager (SCM)" },
        { "Sottosistema di sicurezza locale (LSASS)", "Local Security Authority Subsystem (LSASS)" },
        { "Isolamento credenziali e sicurezza VBS (LSAISO)", "VBS Credential Isolation & Guard (LSAISO)" },
        { "Applicazione di accesso a Windows (winlogon)", "Windows Logon Application (winlogon)" },
        { "Host condiviso servizi Windows (svchost)", "Shared Windows Services Host (svchost)" },
        { "Host servizi di rete (svchost · netsvcs)", "Network Services Host (svchost · netsvcs)" },
        { "Host servizi locali (svchost · localService)", "Local Services Host (svchost · localService)" },
        { "Host servizi di rete locali (svchost · networkService)", "Local Network Services Host (svchost · networkService)" },
        { "Host avvio DCOM (svchost · DcomLaunch)", "DCOM Launch Host (svchost · DcomLaunch)" },
        { "Host modello app Windows (svchost · AppModel)", "Windows App Model Host (svchost · AppModel)" },
        { "Host telemetria Windows (svchost · UtcSvc)", "Windows Telemetry Host (svchost · UtcSvc)" },
        { "Sottosistema del Registro di Windows", "Windows Registry Subsystem" },
        { "Host esperienza Shell di Windows", "Windows Shell Experience Host" },
        { "Host del Menu Start di Windows", "Windows Start Menu Experience Host" },
        { "Host cornice app moderne (UWP)", "Modern App Frame Host (UWP)" },
        { "Gestione attività di Windows", "Windows Task Manager" },
        { "Prompt dei comandi di Windows", "Windows Command Prompt" },
        { "Host sottosistema Linux WSL / Macchina virtuale", "WSL Linux Subsystem Host / Virtual Machine" },
        { "Gestione macchine virtuali Hyper-V", "Hyper-V Virtual Machine Management" },
        { "Segnalazione errori di Windows (WerFault)", "Windows Error Reporting (WerFault)" },
        { "Gestione aggiornamenti e moduli Windows", "Windows Update & Modules Installer" },
        { "Motore di installazione Windows Installer (MSI)", "Windows Installer Engine (MSI)" },
        { "Esecutore di librerie DLL a 32/64 bit (rundll32)", "32/64-bit DLL Host Runner (rundll32)" },
        { "Processo principale Google Chrome", "Google Chrome Main Coordinator Process" },
        { "Processo principale Microsoft Edge", "Microsoft Edge Main Coordinator Process" },
        { "Processo contenuti web Firefox", "Firefox Sandboxed Web Content Process" },
        { "Processo principale Mozilla Firefox", "Mozilla Firefox Main Browser Process" },
        { "Host estensioni Visual Studio Code", "Visual Studio Code Extension Host" },
        { "Watcher del file system VS Code", "VS Code File System Watcher" },
        { "Editor Visual Studio Code", "Visual Studio Code Editor" },
        { "IDE Microsoft Visual Studio", "Microsoft Visual Studio IDE" },
        { "Controllo versione distribuito Git", "Git Distributed Version Control" },
        { "Virtualizzazione e container Docker", "Docker Container & Virtualization Engine" },
        { "Compilatore e gestore pacchetti Rust", "Rust Compiler & Cargo Package Manager" },
        { "Compilatore e toolchain Go", "Go (Golang) Compiler & Toolchain" },
        { "Gestore pacchetti JavaScript", "JavaScript Package Manager (npm/pnpm/yarn)" },
        { "Client di comunicazione Discord", "Discord Voice & Text Communication Client" },
        { "Piattaforma di collaborazione Slack", "Slack Workspace Collaboration Client" },
        { "Client Microsoft Teams", "Microsoft Teams Collaboration Client" },
        { "Client desktop Telegram", "Telegram Desktop Messaging Client" },
        { "Client desktop WhatsApp", "WhatsApp Desktop Messaging Client" },
        { "Riproduttore musicale Spotify", "Spotify Music & Podcast Player" },
        { "Piattaforma giochi Steam", "Steam Gaming Platform & Overlay" },
        { "Microsoft Word", "Microsoft Word Document Processor" },
        { "Microsoft Excel", "Microsoft Excel Spreadsheet & Analytics" },
        { "Microsoft PowerPoint", "Microsoft PowerPoint Presentation Suite" },
        { "Adobe Acrobat / Reader", "Adobe Acrobat / PDF Reader" },
        { "Editor di testo", "Text & Code Editor (Notepad / Notepad++)" },
        { "Sincronizzazione Microsoft OneDrive", "Microsoft OneDrive Cloud Sync" },
        { "Sincronizzazione cloud Dropbox", "Dropbox Cloud File Sync" },
        { "Suite Microsoft PowerToys", "Microsoft PowerToys System Utilities" },
        { "Indicizzatore rapido Everything", "Everything Instant NTFS File Indexer" },
        { "Agente avvisi RAM ProcessLens", "SysPulse / ProcessLens RAM Alert Agent" },
        { "Servizio diagnostico ProcessLens Guardian", "SysPulse / ProcessLens Guardian ETW Service" },
        { "Profiler heap .NET di ProcessLens", "SysPulse / ProcessLens .NET Heap Profiler" },
        { "Dashboard diagnostica ProcessLens / SysPulse", "SysPulse / ProcessLens Diagnostic Dashboard" },
        { "Host finestra console di Windows", "Windows Console Window Host (conhost)" },
        { "Gestione finestre desktop (DWM)", "Desktop Window Manager (DWM)" },
        { "Esplora risorse e shell di Windows", "Windows Explorer & Desktop Shell" },
        { "Host infrastruttura shell (sihost)", "Shell Infrastructure Host (sihost)" },
        { "Host attività pianificate di Windows", "Windows Scheduled Task Host (taskhostw)" },
        { "Runtime Broker di Windows", "Windows Runtime Broker" },
        { "Interfaccia di ricerca Windows", "Windows Search UI Host" },
        { "Indicizzatore di Windows Search", "Windows Search File Indexer" },
        { "Sicurezza di Windows / Microsoft Defender", "Windows Security / Microsoft Defender Antivirus" },
        { "Servizio input di testo e tastiera", "Windows Text Input & Keyboard Service" },
        { "Host componenti COM (dllhost)", "COM Surrogate Component Host (dllhost)" },
        { "Motore audio di Windows (audiodg)", "Windows Audio Device Graph Engine (audiodg)" },
        { "Provider WMI di Windows", "Windows WMI Provider Host (WmiPrvSE)" },
        { "Spooler di stampa Windows", "Windows Print Spooler Service (spoolsv)" },
        { "Host driver caratteri (fontdrvhost)", "Usermode Font Driver Host (fontdrvhost)" },
        { "Host aggregatore telemetria Windows", "Windows Telemetry Aggregator Host" },
        { "Pannello Widget di Windows", "Windows 11 Widgets Board & Service" },
        { "Collegamento al telefono (Phone Link)", "Microsoft Phone Link Host" },
        { "Xbox Game Bar e servizi di gioco", "Xbox Game Bar & Windows Gaming Services" },
        { "Impostazioni di sistema Windows", "Windows System Settings Application" },
        { "Schermata di blocco e accesso Windows", "Windows Lock Screen & Logon UI" },
        { "Ispezione e scansione Microsoft Defender", "Microsoft Defender On-Demand & Network Scanner" },
        { "Copia Shadow del volume (VSS)", "Volume Shadow Copy Service (VSS)" },
        { "Host associazione dispositivi (dasHost)", "Device Association Framework Host (dasHost)" },
        { "Estensibilità rete wireless (wlanext)", "Wireless LAN 802.11 Extensibility Host (wlanext)" },
        { "Host driver in modalità utente (WUDFHost)", "Windows User-Mode Driver Host (WUDFHost)" },
        { "Sink asincrono WMI (unsecapp)", "WMI Asynchronous Callback Sink (unsecapp)" },
        { "Host attività in background UWP", "UWP Background Task Host" },
        { "Compilatore Roslyn / Motore MSBuild .NET", "Roslyn Compiler Server / .NET MSBuild Engine" },
        { "Toolchain di compilazione Microsoft Visual C++", "Microsoft Visual C++ (MSVC) Compiler & Linker" },
        { "Compilatore C/C++ GCC / LLVM Clang", "GCC / LLVM Clang C/C++ Compiler" },
        { "Orchestratore di build CMake / Ninja / Make", "CMake / Ninja / Make Build Orchestrator" },
        { "Sistema di build e compilatore Java (Gradle / Maven)", "Java Compiler & Gradle / Maven Build System" },
        { "Runtime moderno JavaScript / TypeScript (Bun / Deno)", "Modern JavaScript / TypeScript Runtime (Bun / Deno)" },
        { "Editor di codice AI-native (Cursor / Windsurf)", "AI-Native Code Editor (Cursor / Windsurf)" },
        { "Ambiente di sviluppo JetBrains IDE", "JetBrains Integrated Development Environment" },
        { "Motore di inferenza AI / LLM locale", "Local AI / LLM Inference Engine (Ollama / llama.cpp / LM Studio)" },
        { "Assistente AI Claude", "Claude AI Development & Desktop Assistant" },
        { "Strumenti SDK Android (ADB / Emulatore)", "Android SDK Tools (ADB Bridge / QEMU Emulator)" },
        { "Motore database Microsoft SQL Server", "Microsoft SQL Server Relational Database Engine" },
        { "Server database PostgreSQL", "PostgreSQL Relational Database Server" },
        { "Server database MySQL / MariaDB", "MySQL / MariaDB Relational Database Server" },
        { "Server database documentale MongoDB", "MongoDB Document NoSQL Database Server" },
        { "Data store in-memory Redis", "Redis In-Memory Data Store & Cache" },
        { "Server Web HTTP / Reverse Proxy", "HTTP Web Server / Reverse Proxy" },
        { "Orchestrazione cluster Kubernetes", "Kubernetes Cluster Orchestration Tool" },
        { "Macchina virtuale (VirtualBox / VMware)", "Virtual Machine Hypervisor (VirtualBox / VMware)" },
        { "Servizi driver e overlay NVIDIA", "NVIDIA Display Driver Container & Overlay Services" },
        { "Software e servizi grafici AMD Radeon", "AMD Radeon Graphics Software & Services" },
        { "Servizio controllo grafico Intel", "Intel Graphics Control & Command Service" },
        { "Servizio gestione audio Realtek HD", "Realtek HD Audio Management Service" },
        { "OBS Studio (Registrazione e Streaming)", "OBS Studio Video Recording & Live Streaming" },
        { "Riproduttore multimediale (VLC / mpv)", "Media Player (VLC / mpv)" },
        { "Gestore archivi e compressione file", "Archive & File Compression Manager (7-Zip / WinRAR / tar)" },
        { "Client di controllo e desktop remoto", "Remote Desktop & Support Client (AnyDesk / TeamViewer / RDP)" },
        { "Analizzatore di rete Wireshark / Dumpcap", "Wireshark / Dumpcap Network Protocol Analyzer" },
        { "Client trasferimento dati di rete (curl)", "Network Data Transfer Client (curl)" },
        { "Elaboratore multimediale FFmpeg", "FFmpeg Audio/Video Media Processor" },
        { "Language server", "Code Intelligence Language Server (LSP)" },
        { "Gestore crash", "Crash Reporter & Dump Handler" },
        { "Aggiornamento software", "Software Update Helper / Service" },
        { "Worker multiprocessing Python", "Python Multiprocessing Worker" },
        { "Servizio o modulo Python", "Python Module or Service" },
        { "Script Python", "Python Script" },
        { "Processo Python", "Python Process" },
        { "Processo Node.js", "Node.js Process" },
        { "Script / servizio Node.js", "Node.js Script / Service" },
        { "Compilatore Roslyn (.NET)", "Roslyn Compiler (.NET)" },
        { "Applicazione/servizio .NET", ".NET Application / Service" },
        { "Processo .NET", ".NET Process" },
        { "Applicazione Java (JAR)", "Java Application (JAR)" },
        { "Servizio Java in ascolto sulla rete", "Network-Listening Java Service" },
        { "Applicazione desktop Java", "Java Desktop Application" },
        { "Java Virtual Machine", "Java Virtual Machine (JVM)" },
        { "Script PowerShell", "PowerShell Script" },
        { "Shell PowerShell", "PowerShell Interactive / Automation Shell" }
    };

    private static readonly Dictionary<string, string> PhraseReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Ospita: ", "Hosts Windows services: " },
        { "Parametro servizio: ", "Service parameter: " },
        { "Gruppo di servizi: ", "Service group: " },
        { "Ospita servizi DLL di sistema raggruppati per ottimizzare la memoria", "Hosts grouped system DLL services to optimize memory" },
        { "Processo generico host di Windows per l'esecuzione di servizi implementati in librerie dinamiche DLL.", "Generic Windows host process for running system services implemented in dynamic-link libraries (DLLs)." },
        { "Gestito dal Service Control Manager (services.exe).", "Managed by the Windows Service Control Manager (services.exe)." },
        { "La terminazione può interrompere i servizi di sistema ospitati all'interno.", "Terminating may disrupt the Windows system services hosted inside this instance." },
        { "Padre: ", "Parent: " },
        { "Padre riportato: ", "Reported parent: " },
        { "Padre riportato da Windows: ", "Parent reported by Windows: " },
        { "Catena di avvio non verificata.", "Launch chain not verified." },
        { "Autore e motivo dell’avvio sconosciuti.", "Author and launch reason unknown." },
        { "Motivo dell’avvio sconosciuto.", "Launch reason unknown." },
        { "motivo dell’avvio sconosciuto", "launch reason unknown" },
        { "motivo puntuale dell’avvio sconosciuto", "exact launch trigger unknown" },
        { "Non terminarlo: può causare instabilità grave o arresto del sistema.", "Do not terminate: can cause severe instability or system crash." },
        { "Non terminare: ", "Do not terminate: " },
        { "La chiusura può interrompere i servizi associati; usa Gestione servizi quando possibile.", "Terminating may disrupt associated services; use Service Manager when possible." },
        { "Terminarlo può chiudere l'intera applicazione.", "Terminating it may close the entire application." },
        { "Interrompe l'applicazione associata.", "Interrupts the associated application." },
        { "Interrompe il processo associato.", "Interrupts the associated process." },
        { "Sconsigliato terminarlo: causa il riavvio dello schermo e possibili blocchi grafici.", "Not recommended to terminate: causes screen reload and graphic freezes." },
        { "Scompaiono temporaneamente barra delle applicazioni e cartelle aperte.", "Taskbar and open folders will temporarily disappear." },
        { "Può bloccare menu Start, barra delle applicazioni e scorciatoie di sistema.", "May freeze Start menu, taskbar and system shortcuts." },
        { "Interrompe temporaneamente la riproduzione o acquisizione audio.", "Temporarily interrupts audio playback or recording." },
        { "Interrompe temporaneamente l'indicizzazione o le ricerche sui file.", "Temporarily pauses file indexing and searches." },
        { "Protetto dal sistema o sconsigliato da chiudere per non ridurre la protezione.", "Protected by system or not recommended to close to avoid weakening defense." },
        { "Chiude il pannello di ricerca; Windows lo ricrea automaticamente alla successiva ricerca.", "Closes search flyout; Windows will recreate it upon next search." },
        { "Interrompe le attività pianificate attualmente ospitate in questo processo.", "Interrupts scheduled tasks currently hosted in this process." },
        { "La parte dell'interfaccia associata può ricaricarsi o mostrare un errore; spesso il processo viene ricreato.", "The associated UI may reload or show an error; the process is often recreated." },
        { "L'app può perdere temporaneamente accelerazione grafica e può ricreare il processo.", "The app may temporarily lose hardware acceleration and recreate the process." },
        { "La funzione associata può interrompersi e il processo può essere ricreato automaticamente.", "The associated function may stop and the process may be automatically recreated." },
        { "Effetto della chiusura non classificato: evita di terminarlo finché il ruolo non è più chiaro.", "Termination effect unclassified: avoid terminating until role is clearer." },
        { "Ruolo dal parametro ", "Role from parameter " },
        { "Parametri: possibile ", "Parameters: possible " },
        { "File indicato nei parametri: ", "File specified in parameters: " },
        { "Modulo indicato nei parametri: ", "Module specified in parameters: " },
        { "Assembly indicato nei parametri: ", "Assembly specified in parameters: " },
        { "Archivio indicato nei parametri: ", "Archive specified in parameters: " },
        { "nella command line", "in command line" },
        { "finestra visibile", "visible window" },
        { "finestra top-level visibile", "visible top-level window" },
        { "connessioni TCP stabilite", "established TCP connections" },
        { "socket TCP LISTEN", "TCP LISTEN sockets" },
        { "porta/e TCP", "TCP port(s)" },
        { "Ascolta su ", "Listening on " }
    };

    /// <summary>
    /// Builds a complete English analysis block for the selected process, explaining its identity,
    /// estimated role, what it does, its parent origin, live resource state, and termination impact.
    /// </summary>
    public static string BuildEnglishProcessAnalysis(
        ProcessSnapshot p,
        ProcessInsight i,
        string? originParentSummary = null,
        string? originOperation = null)
    {
        string roleEn = Translate(i.Role, AppLanguage.English);
        string summaryEn = Translate(i.Summary, AppLanguage.English);
        string meaningEn = Translate(i.WhatIsIt, AppLanguage.English);
        string whyEn = Translate(i.WhyRunning, AppLanguage.English);
        string impactEn = Translate(i.CloseImpact, AppLanguage.English);
        string riskEn = ClosingRiskLabel(i.CloseRisk, AppLanguage.English);
        string categoryEn = TerminologyCategoryLabel(i.Category, AppLanguage.English);

        var lines = new List<string>
        {
            $"• Process & Identity: {i.FriendlyName} ({p.Name}, PID {p.Pid}) — Category: {categoryEn} — Owner/App: {i.OwnerApp}",
            $"• Estimated Role (EN): {roleEn} ({TerminologyFunctionLabel(i.RoleConfidencePercent, AppLanguage.English)})",
            $"• Activity Summary (EN): {summaryEn}",
            $"• What This Process Does (EN): {meaningEn}"
        };

        if (!string.IsNullOrWhiteSpace(originParentSummary))
            lines.Add($"• Parent & Origin (EN): {Translate(originParentSummary, AppLanguage.English)}");
        if (!string.IsNullOrWhiteSpace(originOperation))
            lines.Add($"• Launch Operation (EN): {Translate(originOperation, AppLanguage.English)}");

        lines.Add($"• Why It Is Running (EN): {whyEn}");

        string cpuState = p.CpuSampleAvailable
            ? $"{p.CpuPercent:F1}% system CPU (30s avg {p.CpuAverage30Seconds:F2}%, peak {p.CpuPeak30Seconds:F2}%)"
            : "CPU sample waiting/unavailable";
        string memState = p.PrivateWorkingSetAvailable
            ? $"{p.PrivateWorkingSetMb:F1} MiB Private Working Set"
            : (p.WorkingSetAvailable ? $"{p.WorkingSetMb:F1} MiB Total Working Set" : "Memory N/A");
        string netState = p.IsDeepInspected
            ? $"TCP LISTEN: {p.TcpListeningCount}, TCP ESTABLISHED: {p.TcpEstablishedCount}, UDP: {p.UdpEndpointCount}"
            : "Deep network inspection pending";
        lines.Add($"• Live Telemetry (EN): {cpuState} · {memState} · {netState}");
        lines.Add($"• If Terminated (EN) [{riskEn}]: {impactEn}");

        return string.Join("\n", lines);
    }
}
