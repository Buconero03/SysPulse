# R10 R8 — monitor RAM parallelo e navigazione nei servizi

La candidata continua da R10 R7. Include entrambe le richieste: un programma separato che registra il consumo di ProcessLens e la navigazione dalla schermata RAM inviata il 19 ottobre 2026. Le correzioni CPU e ripristino R7 restano incluse.

**Build .NET, script PowerShell, test C#, WPF, DPI e misure reali Windows: NON ESEGUITI qui.** L’ambiente non dispone di SDK .NET né PowerShell. Non è allegato un EXE compilato e non si dichiara un consumo o risparmio RAM misurato. Sono inclusi sorgenti, test, launcher e controlli di sorgente portabili. Occorre qualificare la candidata su Windows.

## Come avviare ProcessLens con il log RAM

Dopo la verifica Windows, aprire **Avvia-ProcessLens-con-log-RAM.cmd**. Il launcher:

1. cerca la GUI pubblicata/compilata nella cartella del progetto; se non la trova, permette di scegliere un `ProcessLens.exe` già esistente;
2. se manca il misuratore, compila **solo il nuovo monitor** con l’SDK .NET 8 già installato, senza installare strumenti;
3. apre una finestra console indipendente e avvia la GUI selezionata. Il secondo avvio della GUI usa il normale ripristino dell’istanza esistente.

Se ProcessLens è già aperto e si vuole soltanto misurarlo, usare **Avvia-solo-monitor-RAM.cmd** e selezionare il suo EXE se richiesto. Non occorre ricompilare una GUI precedente per usare il monitor; occorre invece la nuova GUI R8 per le modifiche alla navigazione.

Da PowerShell, con un percorso esplicito:

```powershell
.\start-processlens-ram-monitor.ps1 -GuiExe 'D:\cartella\ProcessLens.exe' -StartGui
```

Il percorso nell’esempio deve essere sostituito con quello reale. Senza `-StartGui` parte solo il misuratore. `-IntervalSeconds 1` aumenta la frequenza; il valore predefinito è **5 secondi**. `-DurationSeconds 180` limita la sessione a circa tre minuti; zero significa fino all’arresto manuale o al limite dei log.

Chiudere il monitor con **Ctrl+C**: non termina GUI, servizi o agenti. Ridurre o chiudere davvero la GUI non interrompe il monitor; può continuare a osservare i componenti della suite ancora attivi. Non è configurato alcun avvio all’accesso a Windows. Il monitor non avvia Guardian, MemoryAgent, profiler, acquisizioni ETW o processi bersaglio. Il launcher avvia la GUI solo con la scelta esplicita sopra.

## Che cosa registra

I file sono tutti sotto una sola radice:

`verification-r10r8\ram-monitor\<sessione-UTC-e-PID-monitor>\`

| File | Contenuto |
|---|---|
| `monitor.log` | Quadro generale leggibile, dettagli per PID, tre metriche separate, copertura ed errori |
| `totals.csv` | Serie dei totali, somme disponibili e campioni mancanti; valori grezzi in byte, numeri con formato invariabile |
| `processes.csv` | PID, PID padre, padre letto dall’handle, creazione, percorso, ragione di inclusione, contatori e limiti |
| `scope.json` | Percorsi accettati, regole di associazione, intervallo e limiti della sessione |
| `summary.json` | Numero di campioni, numero di campioni completi, media aritmetica e massimo campionato della RAM privata residente completa |

I file vengono aggiornati e svuotati sullo stream dopo ogni campione; l’arresto forzato della console può lasciare lo stato del riepilogo all’ultimo aggiornamento. Il riepilogo non accumula tutta la serie in memoria. Per contenere la crescita su disco, il monitor si ferma a circa **50 MiB complessivi dei tre log**, più l’ultimo campione, conservando tutto. Non cancella sessioni precedenti. L’EXE accetta anche `--max-log-mib`, da 1 a 1024.

Il valore principale è **la somma della RAM privata residente dei PID identificati nel perimetro**, non l’intero footprint fisico comprensivo di tutte le pagine condivise. Si registrano separatamente:

- working set totale: la sua somma può duplicare pagine condivise fra processi;
- commit privato: non equivale alla RAM fisica residente;
- consumo del misuratore: esplicitamente escluso dal totale della suite.

Non viene mai sostituito il working set o il commit alla RAM privata. Se un contatore, un’identità o l’enumerazione non sono disponibili, il totale interessato diventa **N/D / PARZIALE** e resta visibile la somma delle letture riuscite. Non basta un solo PID leggibile per dichiarare completo un gruppo. Un’enumerazione riuscita senza membri nel perimetro dà zero membri osservati; non dimostra che ogni possibile installazione di ProcessLens sia assente dal computer.

## Quali processi include

Le radici sono riconosciute mediante **percorso completo esatto**: GUI selezionata, percorsi standard di build/pubblicazione del progetto, MemoryAgent installato nell’account corrente e Guardian installato in Program Files secondo gli installer del progetto. Il profiler viene contato soltanto se già attivo. Un programma omonimo in un’altra installazione viene segnalato come fuori perimetro.

Sono inclusi anche i discendenti osservabili delle radici: PID padre del censimento, PID padre ottenuto dallo stesso handle del figlio, creazioni compatibili di padre e figlio. La query opzionale `NtQueryInformationProcess` è risolta dinamicamente: se manca o fallisce, non si afferma la relazione al figlio. I figli già osservati vengono seguiti per PID + creazione anche se il padre termina. Il misuratore e i suoi discendenti sono esclusi.

Queste sono associazioni osservate di processo, non una prova che ogni byte sia stato richiesto dalla GUI. Il campionamento può perdere processi brevi, picchi fra due letture e figli già orfani prima dell’avvio del monitor. Le letture dei PID non sono simultanee. Componenti avviati come `dotnet nome.dll` o installati in percorsi personalizzati non sono automaticamente riconosciuti: per un percorso aggiuntivo usare l’EXE del monitor con `--include-exe <percorso>` e controllare `scope.json`. Non viene letta la riga di comando per cercare credenziali o attribuire processi arbitrari. Per i processi non leggibili si conserva il limite; non si tenta elevazione automatica.

## Doppio clic e schede della pagina RAM

- Le quattro schede **Applicazioni e Browser**, **Sviluppo e Runtime**, **Sistema Windows**, **Servizi e Secondari** sono pulsanti: clic sul titolo/sfondo, oppure Tab e Spazio, apre l’elenco RAM della categoria. I valori restano selezionabili per la copia.
- Il filtro ora opera sul censimento completo restituito dalla lettura, senza tagliare prima la lista ai primi 80 PID. Non viene cambiata l’acquisizione Windows preesistente.
- Il doppio clic su una riga RAM cerca i servizi SCM associati ai PID del processo/gruppo. Il raggruppamento RAM resta quello esistente per nome eseguibile, non prova di appartenenza a un’unica applicazione. I PID sono confrontati esattamente e l’istanza viene controllata prima/dopo la lettura SCM; un processo nato dopo l’inizio della lettura RAM non può acquisire la navigazione della vecchia riga.
- Un solo servizio associato: si apre direttamente la finestra di dettagli. Più servizi, come per un gruppo `svchost`: si apre la sezione Servizi limitata agli host verificati, senza scegliere arbitrariamente un servizio. **Mostra tutti i servizi** rimuove il limite.
- Senza associazioni SCM si apre il dettaglio del processo/PID rappresentativo solo se identificabile; la categoria “Secondari” non basta a chiamare un processo “servizio Windows”. Un PID terminato/riusato non viene sostituito silenziosamente.
- Nella sezione Servizi, **doppio clic** o **Invio** su una riga apre una finestra con identità, stato, descrizione dichiarata, configurazione, dipendenze e limiti. Tutto è selezionabile e in sola lettura. Non si arresta il servizio.
- Il pulsante **Ispeziona anatomia RAM** conserva l’analisi RAM esplicita; il doppio clic informativo non la avvia.

La quinta scheda è ora **Memoria non attribuita ai PID**. Apre una spiegazione del residuo contabile: il valore non identifica separatamente Kernel, driver o DLL. È una correzione del testo della scheda toccata; i contratti e l’aritmetica del collector rimangono invariati.

## Qualificazione richiesta

Eseguire `powershell -NoProfile -File .\verify-candidate-r10.ps1` per tutte le build e suite, incluso `ProcessLens.RamMonitorTests`. Per il normale flusso completo, `build-dashboard.ps1` comprende il test del monitor e la sua pubblicazione separata. Nessuno di questi due test avvia automaticamente il monitor operativo.

Le regressioni aggiunte coprono totali parziali, zero realmente letto, metriche distinte, enumerazione fallita, omonimi, creazione/PID, associazioni ai discendenti, esclusione del monitor, CSV UTF-8 e riepilogo. La prova nativa Windows controlla anche la creazione del processo di test contro .NET; non avvia/termina processi bersaglio. I test WPF costruiscono la nuova finestra e le schede senza mostrarle o avviare polling. Qui tali test **NON sono stati eseguiti**.

Verificare manualmente: avvio con un percorso contenente spazi; GUI già aperta; monitor lasciato attivo mentre si riduce/riapre la GUI; servizi/agente già attivi; Ctrl+C e log leggibili; un contatore non leggibile; finestra dettagli singolo servizio; gruppo `svchost`; processo secondario senza servizio; clic su ogni categoria; doppio clic su header/spazio vuoto senza azioni; navigazione rapida verso un’altra scheda durante la lettura SCM; DPI 100/125/150/200% e culture it-IT/en-US. Confrontare working set e privato con le colonne equivalenti di Gestione attività, senza equipararle al commit.

Restano aperti i rilievi semantici RAM precedenti, salvo il testo del residuo qui corretto, e il rischio asincrono generale dei rapporti già registrato in R6. Il nuovo percorso servizi ha proprie guardie di navigazione; questo non qualifica gli altri percorsi asincroni.

Fonti Microsoft: [contatori EX2](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2), [GetProcessMemoryInfo](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo), [QueryFullProcessImageName](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew), [NtQueryInformationProcess e vincoli di compatibilità](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryinformationprocess). Consultate fra 18 e 19 ottobre 2026.
