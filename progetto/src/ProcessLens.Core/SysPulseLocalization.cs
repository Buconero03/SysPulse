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
public static partial class SysPulseLocalization
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

        var culture=System.Globalization.CultureInfo.GetCultureInfo(language==AppLanguage.English?"en-US":"it-IT");
        System.Globalization.CultureInfo.CurrentCulture=culture;
        System.Globalization.CultureInfo.CurrentUICulture=culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture=culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=culture;
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
    private static readonly Dictionary<string,string> Dictionary = LoadCatalog();

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
        if(string.IsNullOrEmpty(text))return string.Empty;
        if((language??CurrentLanguage)==AppLanguage.Italian)return Origins.TryGetValue(text,out var original)?original.Text:text;
        string rendered=Messages.TryGetValue(text,out var message)?message.EnglishText(0):Dictionary.GetValueOrDefault(text,text);
        if(rendered==text)return text;
        string copy=new(rendered.AsSpan());Origins.Add(copy,new CanonicalOrigin(text));return copy;
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

    // Compatibility export. The main interface uses the same canonical analysis in either language.
    // It never appends a second report or infers current work from a process name or CPU threshold.
    public static string BuildEnglishProcessAnalysis(ProcessSnapshot process, ProcessInsight insight,
        string? identifiedOperation=null,string? loadedSubsystems=null)
    {
        var lines=new List<string> {
            $"Process: {process.Name} (PID {process.Pid})",
            "Estimated role: "+Translate(insight.Role,AppLanguage.English),
            "Description: "+Translate(insight.WhatIsIt,AppLanguage.English),
            "Launch context: "+Translate(insight.WhyRunning,AppLanguage.English),
            "Possible termination effects: "+Translate(insight.CloseImpact,AppLanguage.English)
        };
        if(process.CpuSampleAvailable)lines.Add(FormattableString.Invariant($"Sampled CPU: {process.CpuPercent:F1}% of total PC capacity."));
        else lines.Add("Sampled CPU: unavailable.");
        if(process.PrivateWorkingSetAvailable)lines.Add(FormattableString.Invariant($"Private working set: {process.PrivateWorkingSetMb:F1} MiB."));
        if(process.WorkingSetAvailable)lines.Add(FormattableString.Invariant($"Total working set: {process.WorkingSetMb:F1} MiB, including private and shareable pages."));
        if(process.PrivateCommitAvailable)lines.Add(FormattableString.Invariant($"Private commit: {process.PrivateMb:F1} MiB; not physical RAM."));
        lines.Add("Use the network report for per-PID socket counts and acquisition status. Socket states do not measure transferred bytes.");
        lines.Add("Role classification does not establish current internal work, launch intent, software authenticity or safety.");
        return string.Join(Environment.NewLine,lines);
    }
}
