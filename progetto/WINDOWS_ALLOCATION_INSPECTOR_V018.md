# ProcessLens 0.18.0 - Windows Allocation Inspector

Nuova API C# Win32/PSAPI, in sola lettura, eseguita **solo** su richiesta esplicita
`Analisi approfondita > Perche occupa RAM?` per un singolo PID selezionato.

## API reali
- `OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ)`: handle minimo per la lettura.
- `GetProcessTimes`: verifica dell'istanza sullo stesso handle per non confondere PID riutilizzati.
- `VirtualQueryEx`: regioni virtuali `MEM_COMMIT`, gruppi per allocation base e tipo (`MEM_PRIVATE`, `MEM_IMAGE`, `MEM_MAPPED`).
- `QueryWorkingSetEx`: **campione di pagine**, distingue i bit `Valid` e `Shared` per le pagine del campione. I conteggi non vanno estrapolati a byte fisici esatti.

## Limiti espliciti
- Massimo 25.000 regioni enumerate e 2.500 pagine campionate; tempo di circa 2 secondi per scansione e prova. Report parziale se necessario.
- I byte `MEM_COMMIT` **non** sono byte RAM residenti. `MEM_PRIVATE` **non** equivale a heap gestito.
- QueryWorkingSetEx conferma la residenza delle sole pagine campionate, non del totale del processo.
- Le regioni di allocazione non identificano oggetti, stack, singoli buffer, cache o funzioni responsabili. Un profiler specifico di .NET, V8, JVM, CPython o heap nativo richiede ulteriori strumenti autorizzati e analisi del runtime.
- Processi protetti, terminati o con PID riutilizzato possono risultare inaccessibili: non riportare zero fittizio.
- Nessuna iniezione di codice, lettura dei contenuti delle pagine, congelamento, terminazione o modifica della memoria.
- Misure dal solo istante interrogato; frequenza/tempo possono variare in funzione del carico.

## Interfaccia e verifiche
- La spiegazione RAM aggiunge una sezione espandibile **ALLOCAZIONI WINDOWS — PROVE NATIVE** con gruppi ordinati per spazio impegnato.
- `Registro verifiche RAM` registra API consultate, numeri di regioni e pagine campionate, tempi ed errori. Non scrive indirizzi o contenuti delle regioni nel log su disco.
- Test: `tests/check-windows-allocation-inspector.ps1` e `ProcessLens.MemoryTests`, poi `build-dashboard.ps1`.
- `AVVIA-TUTTO.cmd`: avvio guidato, senza modificare il servizio Guardian installato.

Il motore e scritto in **C# con P/Invoke** verso API di sistema native. C++ non e necessario per interrogare queste stesse API Windows. Un motore heap runtime, in-process o ETW sara una fase ulteriore separata con consenso e compatibilita esplicita.
