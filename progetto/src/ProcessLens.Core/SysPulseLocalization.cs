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
        { "Non classificato", "Unclassified" }
    };

    private static readonly Dictionary<string, string> PhraseReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Padre: ", "Parent: " },
        { "Padre riportato: ", "Reported parent: " },
        { "Autore e motivo dell’avvio sconosciuti.", "Author and start reason unknown." },
        { "Motivo dell’avvio sconosciuto.", "Start reason unknown." },
        { "Non terminarlo: può causare instabilità grave o arresto del sistema.", "Do not terminate: can cause severe instability or system crash." },
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
        { "nella command line", "in command line" },
        { "finestra visibile", "visible window" },
        { "finestra top-level visibile", "visible top-level window" },
        { "connessioni TCP stabilite", "established TCP connections" },
        { "socket TCP LISTEN", "TCP LISTEN sockets" },
        { "porta/e TCP", "TCP port(s)" },
        { "Ascolta su ", "Listening on " }
    };
}
