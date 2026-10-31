# ProcessLens v0.9.2 — Metriche memoria Windows

La v0.9.1 mostrava `Process.WorkingSet64` con etichetta generica "MEMORIA": questo comprende pagine condivise e non coincide con il dato in Gestione attività / Processi.

La v0.9.2 interroga la API Windows `GetProcessMemoryInfo` con `PROCESS_MEMORY_COUNTERS_EX2` per ogni PID vivo:

* **Privata residente (Private Working Set)**: pagine private attualmente residenti. È il dato usato come riferimento per la colonna Processi di Gestione attività (a parità di PID e momento).
* **Residente totale (Working Set)**: pagine residenti sia private sia condivise.
* **Commit privato**: memoria privata impegnata (può non essere tutta residente in RAM). Non equivale né a Working Set né a Private Working Set.

Per la lista, ProcessLens preferisce il private working set. Se EX2 non è accessibile, mostra il Working Set *con l'etichetta diversa* "RAM RESIDENTE". Se nessun contatore è disponibile, mostra N/D: mai 0 MB inventati.

## Comparazione corretta

1. Attiva la scheda Dettagli di Gestione attività, mostra la colonna PID e cerca lo **stesso identico PID** di ProcessLens.
2. Confronta "Memoria (working set privato)" con "PRIVATA RESIDENTE" di ProcessLens. Per confrontare la memoria residente totale, usa la colonna "Working Set" in Dettagli.
3. Le rilevazioni sono in momenti diversi; scarti minori sono normali; l'utilizzo può cambiare rapidamente.
4. Il totale del gruppo app e il working set di un singolo processo non sono equivalenti. I working set condivisi non si sommano come se ogni pagina fosse una copia indipendente.

## Efficienza

La lettura della memoria non esegue una scansione pagina per pagina; legge i contatori riassuntivi tramite l'API di sistema. Il deep inspector rimane selettivo.

## Limiti

Windows può negare la lettura di processi protetti; Private Working Set EX2 richiede una build Windows sufficientemente aggiornata. Quando non disponibile, ProcessLens mostra espressamente il fallback.

Documentazione: https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2


## v0.9.3: test indipendenti dalla lingua

Nella v0.9.2 il test attendeva `26.0 MB` ma in ambiente italiano il formato è `26,0 MB`: ora vengono verificate entrambe le culture senza modificare i contatori.
