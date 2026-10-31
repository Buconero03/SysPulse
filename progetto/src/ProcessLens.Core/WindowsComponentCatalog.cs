using System.Globalization;

namespace ProcessLens.Core;

public sealed record WindowsComponentDefinition(string Id, string Name, string Category,
    string Function, string Detail, string? RelativePath);
public sealed record WindowsComponentCoverage(int Returned, int Included, int Groups,
    int OtherWindowsPath, int OtherPath, int UnknownPath, int UnexpectedPath,
    int IncludedWithoutPath, int IncludedWithoutStart, int IncludedWithoutMemory)
{
    public int Excluded => OtherWindowsPath + OtherPath + UnknownPath + UnexpectedPath;
}
public sealed record WindowsComponentGroup(WindowsComponentDefinition Definition,
    IReadOnlyList<SystemProcessEntry> Instances)
{
    public string Name => Definition.Name;
    public string Category => Definition.Category;
    public string Function => Definition.Function;
    public int Count => Instances.Count;
    public int MemoryCount => Instances.Count(p => p.WorkingSetBytes is >= 0);
    public long? MaximumWorkingSet => Instances.Where(p => p.WorkingSetBytes is >= 0)
        .Select(p => p.WorkingSetBytes).DefaultIfEmpty(null).Max();
    public string Resident => SystemProcessPresentation.MiB(MaximumWorkingSet);
    public string Identity => Instances.Any(p => p.Path is null && p.Pid is not (0 or 4))
        ? "Dal nome · percorso N/D" : "PID riservato o percorso previsto";
}

// A small navigation catalog, not an exhaustive OS inventory or a signature verifier.
public static class WindowsComponentCatalog
{
    public static IReadOnlyList<string> Categories { get; } = Array.AsReadOnly(new[] {
        "Kernel e memoria", "Avvio e sessioni", "Servizi", "Sicurezza", "Desktop e console" });
    public static IReadOnlyList<WindowsComponentDefinition> Definitions { get; } = Array.AsReadOnly(new[] {
        new WindowsComponentDefinition("idle", "System Idle Process", "Kernel e memoria", "Contabilità del tempo CPU inattivo",
            "Rappresenta il tempo in cui i processori non eseguono altri thread. Non è un'app e non rappresenta un consumo di RAM da ridurre.", null),
        new("system", "System", "Kernel e memoria", "Thread e attività attribuiti al kernel",
            "Raccoglie thread di sistema e attività kernel attribuite a questa voce. Il suo Working Set non comprende tutta la RAM del kernel, i pool e la cache.", null),
        new("compression", "Memory Compression", "Kernel e memoria", "Compressione delle pagine di memoria",
            "Windows può conservare pagine compresse in RAM per ridurre le scritture sul file di paging. Il Working Set non è la dimensione logica dei dati prima della compressione. Questa voce può non essere esposta dall'inventario.", null),
        new("registry", "Registry", "Kernel e memoria", "Voce interna associata al registro",
            "È distinta dall'Editor del Registro, regedit.exe. Qui sono disponibili i contatori della voce; non viene tracciata la lettura o scrittura delle singole chiavi.", null),
        new("secure", "Secure System", "Sicurezza", "Voce associata all'ambiente di sicurezza isolato",
            "Il nome è associato alla sicurezza basata sulla virtualizzazione. Questa lettura non verifica la configurazione di VBS o Credential Guard e non accede alla memoria protetta.", null),
        new("smss", "smss.exe", "Avvio e sessioni", "Gestione e inizializzazione delle sessioni",
            "Session Manager partecipa all'avvio delle sessioni Windows. Le istanze brevi possono terminare prima del campione; il numero rilevato non ricostruisce la sequenza di avvio.", @"System32\smss.exe"),
        new("csrss", "csrss.exe", "Avvio e sessioni", "Parte utente del sottosistema Windows",
            "Client/Server Runtime Subsystem supporta il sottosistema Windows nelle sessioni. I thread e gli handle letti non indicano quali richieste sta elaborando.", @"System32\csrss.exe"),
        new("wininit", "wininit.exe", "Avvio e sessioni", "Inizializzazione dei processi della sessione di sistema",
            "Partecipa all'avvio della sessione di sistema, inclusi Service Control Manager e LSA. Il campione non osserva gli avvii già avvenuti.", @"System32\wininit.exe"),
        new("winlogon", "winlogon.exe", "Avvio e sessioni", "Gestione dell'accesso interattivo",
            "Coordina le interazioni di accesso alla sessione. La sua presenza non indica un accesso in corso o un tentativo fallito.", @"System32\winlogon.exe"),
        new("logonui", "LogonUI.exe", "Avvio e sessioni", "Interfaccia di autenticazione",
            "Ospita l'interfaccia di accesso e i relativi provider di credenziali. Questa vista non legge credenziali né registra le interazioni dell'utente.", @"System32\LogonUI.exe"),
        new("services", "services.exe", "Servizi", "Service Control Manager",
            "Mantiene il catalogo dei servizi e ne coordina avvio e controllo. Non è il processo che esegue ogni servizio: consulta la sezione Servizi per stato e PID ospitante.", @"System32\services.exe"),
        new("svchost", "svchost.exe", "Servizi", "Host condiviso o dedicato dei servizi",
            "Più istanze possono ospitare servizi diversi. Il solo nome non identifica il servizio né il lavoro in corso. Scegli un PID e usa l'analisi o la sezione Servizi per l'associazione SCM.", @"System32\svchost.exe"),
        new("lsass", "lsass.exe", "Sicurezza", "Autenticazione e criteri di sicurezza locali",
            "Local Security Authority gestisce funzioni di autenticazione e criteri di sicurezza. I contatori non rivelano autenticazioni, credenziali o tentativi di intrusione.", @"System32\lsass.exe"),
        new("lsaiso", "LsaIso.exe", "Sicurezza", "Isolamento dei segreti per Credential Guard",
            "Con Credential Guard, LSA comunica con un componente isolato che protegge i segreti tramite VBS. La classificazione del nome non verifica che questa protezione sia attiva.", @"System32\LsaIso.exe"),
        new("dwm", "dwm.exe", "Desktop e console", "Composizione grafica delle finestre",
            "Desktop Window Manager compone le superfici delle finestre nell'immagine del desktop. Il Working Set non misura la memoria GPU o il carico grafico.", @"System32\dwm.exe"),
        new("explorer", "explorer.exe", "Desktop e console", "Shell di Windows ed Esplora file",
            "Fornisce funzioni della shell e la navigazione dei file. Una o più istanze possono coesistere; dal solo inventario non si identifica quale finestra o operazione le occupa.", @"explorer.exe"),
        new("conhost", "conhost.exe", "Desktop e console", "Host del sottosistema console",
            "Gestisce funzioni della console per i programmi collegati. Non coincide con il comando eseguito: terminale, shell e applicazione sono componenti distinti.", @"System32\conhost.exe"),
        new("fontdrvhost", "fontdrvhost.exe", "Desktop e console", "Host dei driver dei caratteri in modalità utente",
            "Ospita l'elaborazione dei caratteri in modalità utente. Il campione non identifica il carattere o il documento elaborato.", @"System32\fontdrvhost.exe"),
        new("sihost", "sihost.exe", "Desktop e console", "Infrastruttura della shell di Windows",
            "Supporta funzioni della shell. La presenza del processo non dimostra che il desktop risponda correttamente.", @"System32\sihost.exe"),
        new("ctfmon", "ctfmon.exe", "Desktop e console", "Servizi di input e testo",
            "Componente dei servizi di testo di Windows. Non vengono letti i caratteri digitati o gli input dell'utente.", @"System32\ctfmon.exe"),
        new("runtimebroker", "RuntimeBroker.exe", "Desktop e console", "Mediazione per le app di Windows",
            "Supporta l'accesso delle app alle risorse gestite da Windows. Il solo processo non identifica l'app o i permessi usati nel momento del campione.", @"System32\RuntimeBroker.exe"),
        new("taskhostw", "taskhostw.exe", "Servizi", "Host per attività di Windows",
            "Ospita attività di Windows. Il nome non identifica la singola attività caricata o il suo avanzamento.", @"System32\taskhostw.exe"),
        new("dllhost", "dllhost.exe", "Servizi", "Host di componenti COM",
            "Ospita componenti COM fuori dal processo chiamante, anche di terze parti. Il campione non identifica il componente caricato.", @"System32\dllhost.exe"),
        new("audiodg", "audiodg.exe", "Servizi", "Motore e grafo audio di Windows",
            "Ospita il motore audio e può caricare effetti dei produttori. La sua presenza non dimostra che sia in riproduzione un suono.", @"System32\audiodg.exe"),
        new("spoolsv", "spoolsv.exe", "Servizi", "Spooler di stampa",
            "Gestisce il sottosistema di stampa. Il campione non legge le code e non dimostra che una stampa sia in corso.", @"System32\spoolsv.exe"),
        new("wmiprvse", "WmiPrvSE.exe", "Servizi", "Host dei provider WMI",
            "Ospita provider WMI, anche di terze parti. Per attribuire una richiesta a un client servono dati aggiuntivi; questi contatori non bastano.", @"System32\wbem\WmiPrvSE.exe"),
        new("searchindexer", "SearchIndexer.exe", "Servizi", "Indicizzazione per Windows Search",
            "Gestisce l'indice di ricerca. Il campione non verifica lo stato dell'indice o il documento in elaborazione.", @"System32\SearchIndexer.exe"),
        new("searchprotocolhost", "SearchProtocolHost.exe", "Servizi", "Host dei protocolli di Windows Search",
            "Ospita i gestori che accedono alle fonti da indicizzare, anche di terze parti. Il campione non legge i contenuti indicizzati.", @"System32\SearchProtocolHost.exe"),
        new("prevhost", "prevhost.exe", "Desktop e console", "Host delle anteprime di file",
            "Ospita gestori di anteprima fuori dal processo chiamante. Il campione non identifica il file visualizzato o il gestore caricato.", @"System32\prevhost.exe"),
        new("smartscreen", "smartscreen.exe", "Sicurezza", "Controlli di reputazione SmartScreen",
            "Componente dei controlli di reputazione di Microsoft Defender SmartScreen. La presenza non prova una verifica o una minaccia in corso.", @"System32\smartscreen.exe"),
        new("searchfilterhost", "SearchFilterHost.exe", "Servizi", "Host dei filtri di Windows Search",
            "Ospita filtri e gestori delle proprietà per estrarre dati da indicizzare. La presenza non identifica il filtro o il file elaborato.", @"System32\SearchFilterHost.exe")
    });
    public static WindowsComponentDefinition? Match(SystemProcessEntry p, string windowsDirectory)
    {
        if (p.Pid == 0) return Definitions[0];
        if (p.Pid == 4) return Definitions[1];
        string name = p.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase) ? "System Idle Process" : p.Name;
        var d = Definitions.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (d is null) return null;
        // A missing path is retained as an explicit name hint, never as verified ownership.
        if (p.Path is null) return d;
        if (d.RelativePath is null || !SystemProcessPresentation.UnderWindows(p.Path, windowsDirectory)) return null;
        string expected = windowsDirectory.Replace('/', '\\').TrimEnd('\\') + "\\" + d.RelativePath;
        string actual = p.Path.Replace('/', '\\');
        if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return d;
        // Explicit 32-bit Windows host locations; not an arbitrary path or name wildcard.
        if (d.Id is "prevhost" or "ctfmon" or "dllhost" or "wmiprvse" or "searchprotocolhost" or "searchfilterhost")
            return actual.Equals(windowsDirectory.Replace('/', '\\').TrimEnd('\\') + "\\SysWOW64\\" + d.RelativePath[9..],
                StringComparison.OrdinalIgnoreCase) ? d : null;
        return null;
    }
    public static SystemProcessCatalog Retain(SystemProcessCatalog source, string windowsDirectory)
    {
        if (source.ComponentCoverage is not null) return source;
        var kept = new List<SystemProcessEntry>(); var groups = new HashSet<string>();
        int otherWindows = 0, otherPath = 0, unknown = 0, unexpected = 0;
        foreach (var p in source.Processes)
        {
            var d = Match(p, windowsDirectory);
            if (d is not null) { kept.Add(p); groups.Add(d.Id); }
            else if (Definitions.Any(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))) unexpected++;
            else if (p.Path is null) unknown++;
            else if (SystemProcessPresentation.UnderWindows(p.Path, windowsDirectory)) otherWindows++;
            else otherPath++;
        }
        return source with {
            Processes = kept.ToArray(),
            ComponentCoverage = new(source.Processes.Count, kept.Count, groups.Count, otherWindows, otherPath,
                unknown, unexpected, kept.Count(p => p.Path is null && p.Pid is not (0 or 4)),
                kept.Count(p => !p.StartUtc.HasValue), kept.Count(p => p.WorkingSetBytes is null or < 0))
        };
    }
    public static string CoverageReport(SystemProcessCatalog source)
    {
        if (!source.Available || source.ComponentCoverage is not { } c)
            return "Copertura N/D · il campione non consente di contare i processi.";
        return $"Processi restituiti dalla fonte: {c.Returned} · incluse le app. " +
            $"Catalogo Windows: istanze {c.Included} · gruppi {c.Groups}. Fuori catalogo: {c.Excluded}.\n" +
            $"Esclusioni: altro nome con percorso in Windows {c.OtherWindowsPath}; altro nome con percorso esterno {c.OtherPath}; " +
            $"altro nome senza percorso {c.UnknownPath}; nome del catalogo con percorso inatteso {c.UnexpectedPath}.\n" +
            $"Nel catalogo: percorso N/D {c.IncludedWithoutPath}; avvio N/D {c.IncludedWithoutStart}; Working Set N/D {c.IncludedWithoutMemory}.\n" +
            $"Catalogo di {Definitions.Count} componenti, non esaustivo. Nome e percorso non verificano la firma digitale. " +
            "Le esclusioni non indicano processi sospetti. La lettura non può escludere processi nascosti alla fonte o già terminati.";
    }
    public static string SampleStatus(SystemProcessCatalog source, IReadOnlyList<WindowsComponentGroup> visible, bool filtered)
    {
        if (!source.Available) return "Campione non disponibile · conteggi N/D.";
        return $"Campione {source.CapturedUtc.ToLocalTime():HH:mm:ss} · gruppi mostrati: {visible.Count} · istanze mostrate: {visible.SelectMany(g => g.Instances).Count()}" +
            (filtered ? " · filtri attivi" : " · senza filtri") +
            (source.Partial ? " · LETTURA PARZIALE / ALTERNATIVA" : " · lettura della fonte completata") +
            " · aggiornamento manuale";
    }
    public static IReadOnlyList<WindowsComponentGroup> Groups(IReadOnlyList<SystemProcessEntry> processes,
        string windowsDirectory, string category = "", string search = "")
    {
        var matched = processes.Select(p => (Entry: p, Definition: Match(p, windowsDirectory)))
            .Where(p => p.Definition is not null);
        if (category.Length > 0) matched = matched.Where(p => p.Definition!.Category == category);
        search = search.Trim();
        if (search.Length > 0)
        {
            if (search.All(char.IsAsciiDigit)) matched = matched.Where(p => p.Entry.Pid.ToString(CultureInfo.InvariantCulture) == search);
            else matched = matched.Where(p => p.Entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Definition!.Function.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        return matched.GroupBy(p => p.Definition!.Id).Select(g => new WindowsComponentGroup(g.First().Definition!,
            g.Select(p => p.Entry).OrderBy(p => p.Pid).ToArray()))
            .OrderBy(g => Categories.IndexOfCategory(g.Category)).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static int IndexOfCategory(this IReadOnlyList<string> values, string value)
    { for (int i = 0; i < values.Count; i++) if (values[i] == value) return i; return values.Count; }
    public static string Details(WindowsComponentGroup group, SystemProcessEntry p, string directory) =>
        $"FUNZIONE · {group.Category}\n{group.Function}\n{group.Definition.Detail}\n\n" +
        $"ISTANZA SELEZIONATA · {p.Name} · PID {p.Pid}\n" +
        $"PID padre riportato: {p.ParentPid} · Sessione: {p.Session?.ToString(CultureInfo.CurrentCulture) ?? "N/D"}\n" +
        $"Thread: {p.Threads?.ToString(CultureInfo.CurrentCulture) ?? "N/D"} · Handle: {p.Handles?.ToString(CultureInfo.CurrentCulture) ?? "N/D"}\n" +
        $"Working Set totale: {SystemProcessPresentation.MiB(p.WorkingSetBytes)}\n" +
        $"Percorso: {p.Path ?? "N/D · classificazione dal nome, origine non verificata"}\n" +
        $"Avvio UTC: {(p.StartUtc.HasValue ? p.StartUtc.Value.ToString("O") : "N/D")}\nAccesso: {p.Access}\n\n" +
        $"LETTURA DEL CAMPIONE\n{group.Count} istanze nel gruppo corrente; {group.MemoryCount} contatori RAM disponibili. " +
        "La colonna WS max letto mostra il maggiore Working Set disponibile per una singola istanza, non la somma del gruppo.\n" +
        "Funzione da catalogo; operazione corrente non misurata. Nome e percorso non verificano la firma digitale. " +
        "Il campione non registra eventi di avvio o uscita. Per gli eventi usa Guardian.\n" +
        (p.StartUtc.HasValue ? "Apri l'analisi per approfondire la stessa istanza." : "Analisi del PID non disponibile: manca l'ora di avvio; i dettagli del componente restano consultabili.");
}
