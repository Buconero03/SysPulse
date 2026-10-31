# ProcessLens 0.17.4 — Memoria: misure, evidenze e registro verifiche

## Cosa fa davvero
- Solo su richiesta dell'utente: verifica l'istanza PID/start time; rilegge il Working Set, il Private Working Set quando PSAPI lo permette, e il commit privato.
- Esegue una scansione `VirtualQueryEx` non invasiva e limitata (massimo 100.000 regioni / 2 secondi) delle regioni **virtuali impegnate**. Non legge il contenuto della memoria, non identifica cache, heap o oggetti applicativi.
- Interroga `GlobalMemoryStatusEx` per la pressione RAM totale e interpreta la storia disponibile per il medesimo PID/start time.
- Visualizza i passi reali e registra un log minimale in `%LOCALAPPDATA%\ProcessLens\logs\memory-analysis.log` (un file `.old` massimo, rotazione a 1 MiB). Include PID, tempi e contatori ma non registra linee di comando, percorsi di eseguibili, nomi di file o contenuti RAM.

## Confini
I motivi specifici del consumo RAM (heap di oggetti, librerie native, DOM, cache) NON sono misurati. Vengono presentati solo come ipotesi, mai come diagnosi certa. Il giudizio di eccesso RAM senza baseline specifica dell'applicazione resta preliminare. Un profiler del runtime richiede un'azione separata e non è implementato qui.

## Modalità d'uso
Seleziona un PID, apri `Analisi approfondita -> Perché occupa RAM?`, consulta il registro di letture alla fine del rapporto. Per provare la compilazione WPF usa `build-dashboard.ps1` oppure `AVVIA-TUTTO.cmd`. Non richiede aggiornamenti a Guardian.

## Verifica
Preflight `tests/check-memory-measurement-audit.ps1`, test semantica RAM e compilazione WPF devono passare sul PC Windows. La verifica di accesso ai contatori dipende dai permessi sul PID reale.
