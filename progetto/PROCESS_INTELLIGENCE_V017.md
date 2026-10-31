# ProcessLens v0.17.0 — Process Intelligence Engine: spiegazioni approfondite

## Problema affrontato

Il pulsante «Cosa sta facendo?» produceva spesso spiegazioni generiche anche quando erano disponibili evidenze migliori. Ora esegue una nuova analisi **on-demand** dell'istanza selezionata e produce una risposta articolata: identità, ruolo, operazione identificabile, attività effettivamente osservata, possibile origine e limiti della prova.

## Uso

1. Compilare `build-dashboard.ps1` o avviare `AVVIA-TUTTO.cmd` (richiede SDK .NET 8 per build).
2. Aprire la scheda **Processi**, selezionare un PID.
3. Fare clic su **Analizza a fondo cosa fa**, nella sezione domande del processo.
4. Leggere i blocchi «COS'È», «A COSA SERVE», «OPERAZIONE IDENTIFICABILE», «CHE COSA WINDOWS OSSERVA ORA», «CHI L'HA AVVIATO», «COSA NON POSSIAMO PROVARE» e le evidenze.

### Tecnologie

Adattatori locali selettivi: Git (verbo da command line), Python (`-m`, `.py`, `.pyw`, `-c`), Node.js (entrypoint JS/TS), dotnet (assembly DLL, watch run), PowerShell (`.ps1`, comando, payload codificato), curl (upload/header), ffmpeg (`-i`). Il motore generale continua a usare ruoli runtime/servizio/moduli, processo padre, contatori Windows, socket e finestre. Il solo nome non dimostra la funzione specifica.

Le righe di comando sono interpretate in memoria e **non** riprodotte integralmente; per script e assembly viene mostrato il basename. I dettagli sensibili presenti negli argomenti non vengono pubblicati. Non si esegue il programma target, non si intercetta il suo contenuto e non si trasmettono dati in rete. Nessun addestramento o LLM: questa fase è deterministica e verificabile.

Il servizio Guardian v0.14.1 e l'agente RAM rimangono indipendenti. L'analisi approfondita avviene soltanto dopo la richiesta, per non peggiorare il costo delle scansioni periodiche.

## Limiti e sviluppi futuri

- Produttore e prodotto provenienti dal file non sono certificati: firma Authenticode non verificata.
- Connessioni stabilite non dimostrano banda trasferita o operazioni applicative.
- Senza tracing più profondo, log applicativi/profiler/strumentazione del runtime non è possibile sapere quale funzione interna sia in esecuzione.
- Nomi di script o moduli non dimostrano cosa faccia il loro contenuto.
- La confidenza del classificatore è **euristica**, non una probabilità statistica calibrata.
- Fase successiva proposta: ricerca web opzionale su richiesta dell'utente, con revisione esplicita delle informazioni condivise e citazioni, oppure knowledge base locale versionata di firme affidabili; non inviare command line, IP o percorsi personali senza consenso.

## Test di regressione

`tests/ProcessLens.BehaviorTests` verifica il fallback senza invenzioni, `python -m`, Git status, lettura TCP diretta vs famiglia, payload PowerShell codificato e assenza di credenziali/testi sensibili nei report. Eseguito automaticamente da `build-dashboard.ps1` prima della compilazione WPF.

## Verifica in questo ambiente

Solo controlli statici dei sorgenti e test dell'archivio: build .NET 8/WPF **non** eseguibile qui. La compilazione e la prova reale vanno confermate su Windows.
