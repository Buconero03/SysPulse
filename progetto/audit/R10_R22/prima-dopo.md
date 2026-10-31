# R22 — copertura del catalogo Windows

Il campione dell'utente mostrava 16 gruppi e 136 istanze, ma il catalogo ammetteva solo 17 definizioni. Non era il totale dei processi del PC. Le istanze omonime erano già raggruppate e selezionabili per PID.

| Prima | R22 |
|---|---|
| Catalogo di 17 componenti | 29 componenti, inclusi audio, stampa, WMI e ricerca |
| Componenti mostrati e istanze nel catalogo | Gruppi/istanze visibili distinti dal totale restituito dalla fonte |
| Totale originale scartato con le righe escluse | Conteggi scalari conservati prima del filtro |
| Filtri non dichiarati nel riepilogo | Filtri attivi o assenti, conteggio dopo la ricerca |
| Esclusioni non quantificate | Quattro motivi disgiunti; la somma torna al totale della fonte |
| Percorsi non letti nelle singole righe | Anche conteggi percorso/avvio/RAM N/D nel rapporto |
| Launcher leggero poteva aprire R20 | Primo avvio: suite e compilazione R22; poi avvio diretto della nuova GUI |

## Confini

Il catalogo resta deliberatamente mirato: i programmi comuni non entrano nella scheda Sistema Windows neppure se messi in una cartella Windows. Altri componenti del sistema possono restare fuori catalogo; il riepilogo lo dichiara. Il percorso non verifica firma, produttore o contenuto dei moduli. Gli host Windows possono ospitare codice di terze parti. I nomi compatibili senza percorso sono indicazioni, non identità certificate. Le varianti SysWOW64 sono ammesse solo per cinque host esplicitamente elencati.

Il totale è quello delle righe restituite da NtQuerySystemInformation o dalla fonte alternativa Toolhelp, non una certificazione di tutti i processi possibili. Processi brevi o nascosti alla fonte possono mancare. Non si ricavano driver, DPC, interrupt o sicurezza del PC contando processi. L'assenza dal campione o un percorso diverso non provano un'intrusione.

## Risorse e isolamento

Nessun nuovo timer, watcher, scansione del disco, verifica firme, driver, servizio o comando di rete. Riutilizzata la lettura manuale esistente. Conservate solo righe del catalogo e dieci interi di copertura, senza secondo inventario o cronologia. Il costo RAM effettivo della GUI richiede misura su Windows: nessuna riduzione o soglia in MiB è dichiarata.

Acquisizione nativa, parser nativo, Guardian, profiler, MemoryAgent, CPU e rete restano identici a R21. Il record SystemProcessCatalog riceve una proprietà opzionale: i parametri e la decostruzione esistenti restano invariati. Ragione: conservare i conteggi della fonte dopo il filtro senza conservare tutte le app. Copertura verificata con fixture indipendenti da Windows e con errori/dati mancanti.

## Avvio

Un solo comando principale: AVVIA_PROCESSLENS.cmd. Nessun EXE precompilato è incluso. Il primo avvio compila e verifica questa candidata; solo dopo successo scrive la ricevuta R22. Una vecchia installazione non sostituisce la nuova GUI. R20 fornisce eventualmente SDK privato, runtime e cache già presenti: non viene reinstallata o sovrascritta. Con la cache R20 si usano sorgenti NuGet vuote, quindi nessuna verifica vulnerabilità online è dichiarata. Senza R20 si usa lo SDK installato e il ripristino NuGet standard. Non vengono installati automaticamente SDK o driver. Gli avvii seguenti non compilano; per ricompilare dopo modifiche eliminare supporto/build-r22.ok.

## Verifiche eseguite qui

- 103 asserzioni WindowsComponentTests: nuove famiglie e percorsi, esclusioni, raggruppamento, PID esatto, memoria mancante/zero, partizione totale, conteggi con filtri, it-IT/en-US, fallimento e lettura alternativa.
- 22 asserzioni SystemProcessTests: parser sintetico e contatori; nessuna acquisizione Windows reale.
- 30 script preliminari della pipeline Windows, eseguiti con PowerShell 7.4.21 su Linux.
- 17 prove dell'avvio con file e script simulati: fallimenti non producono ricevuta, vecchia GUI mai sostitutiva, cache, percorso con spazi, avvio successivo, argomenti UAC.
- C# compilato contro reference WindowsDesktop: Core, Windows, Semantics, GUI e tre suite UI. Per la GUI usati stub espliciti dei campi generati e InitializeComponent: NON è compilazione XAML/BAML o esecuzione WPF.
- XML/XAML/progetti, diff applicabile e manifest completo controllati nel confezionamento.

## Non eseguito qui

Build MSBuild Windows completa, caricamento reale XAML/BAML, esecuzione WPF/clipboard/UAC, integrazione con la cache installata dell'utente, resa a 100/125/150/200% DPI e RAM reale. Le nuove asserzioni WPF sono incluse nella normale build; non vengono dichiarate passate. Le evidenze storiche in altre cartelle audit descrivono le rispettive revisioni; per i sorgenti modificati fanno fede le nuove evidenze R22.

## Verifica Windows

1. Chiudere la GUI precedente, estrarre tutto e avviare AVVIA_PROCESSLENS.cmd. Conservare eventuali errori della prima build.
2. Controllare la dicitura R22 sotto Componenti di Windows e premere Aggiorna campione.
3. Verificare: totale fonte = istanze catalogo + esclusioni; il totale non cambia cambiando categoria o cercando un PID, mentre le istanze mostrate cambiano.
4. Selezionare svchost/conhost e verificare ogni PID nel selettore delle istanze. WS max letto resta il massimo di UNA istanza, non la RAM totale del gruppo.
5. Copiare il rapporto: deve includere fonte originale, limiti ed eventuali errori. Tornare da un'altra scheda: aggiornare il campione, senza dati vecchi.
6. Ripetere l'avvio: deve aprire direttamente R22 senza nuova build. Controllare ridimensionamento, scroll e leggibilità ai DPI indicati.

## Fonti tecniche

- https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntquerysysteminformation
- https://learn.microsoft.com/en-us/windows/win32/search/-search-indexing-process-overview
- https://learn.microsoft.com/en-us/windows/win32/wmisdk/provider-hosting-and-security
- https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/windows-audio-architecture
- https://learn.microsoft.com/en-us/windows/win32/tsf/64-bit-platform-considerations
