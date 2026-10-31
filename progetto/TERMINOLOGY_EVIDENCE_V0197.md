# ProcessLens v0.19.7 - Terminologia e limiti di interpretazione

## Obiettivo

Migliorare la leggibilita' della v0.19.6 senza cambiare i contatori Windows, il servizio Guardian,
la conservazione SQLite, MemoryAgent o gli strumenti invasivi di profiling.

## Vocabolario GUI

| Vecchio termine | Nuovo termine | Limite dichiarato |
|---|---|---|
| RUOLO CERTO | Funzione riconosciuta da indizi | Regola forte, non probabilita' calibrata |
| RUOLO PARZIALE | Funzione identificata in parte | Evidenze insufficienti per dettagli ulteriori |
| APPARTIENE A | App associata | Associazione stimata da Windows/metadati |
| PRECISIONE DELL'ANALISI | Forza degli indizi | Punteggi euristici, non accuratezza misurata |
| PERCHÉ È ATTIVO | Perche' puo' essere in esecuzione | Descrizione della funzione, non osservazione istantanea |
| Background | Secondari | Categoria interna ancora denominata Background, non indica inattivita' |
| SE LO CHIUDI | Se termini il processo | Gli effetti sono possibili e non testati nel PID |
| RAM PRIVATA | RAM privata residente | Private Working Set quando API disponibile |
| MB da byte/1048576 | MiB | Unita' binaria corretta per le misure mostrate |

## Tracciabilita'

`ProcessTerminology` centralizza intensita' degli indizi, etichette della funzione e categoria
secondaria mostrata. I punteggi 0-100 rimangono nei dettagli tecnici senza segno `%`.
`RoleClassifier` continua ad assegnare i punteggi stessi; la v0.19.7 non li calibra.
`ProcessInsight.Evidence` conserva le prove per analisi e audit.

Chrome con `--type=renderer`: il flag identifica la funzione dichiarata, non la scheda
specifica, l'attivita' corrente o la causa della RAM. TCP ESTABLISHED non prova traffico.

## Verifica

Da Windows 11: doppio clic su `AVVIA-TUTTO.cmd`; lancia il preflight sintassi PowerShell,
i controlli statici, i test .NET inclusa la nuova suite TerminologyTests, e la build WPF.
Questa candidata non e' stata compilata su Windows in questo ambiente.

Non avviare il profiler .NET su servizi critici e non reinstallare Guardian.
