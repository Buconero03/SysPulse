# ProcessLens Guardian v0.14.0 — Process Activity Intelligence (fase 1)

**Scopo:** analizzare automaticamente lo storico ETW/poll raccolto da Guardian v0.13.3, senza interrompere il servizio, modificare lo schema o acquisire nuove categorie di dati sensibili.

## Funzioni
- `guardian-activity.ps1 -Minutes 30` interroga lo SQLite esistente in modo read-only e stampa un report.
- `guardian-activity.ps1 -Minutes 60 -OutputFile .\activity-report.md` salva un report facoltativo.
- Analisi degli avvii nell'ultima finestra di 5 minuti, confronto con i 5 minuti precedenti, totale finestra.
- Classifica dei processi piu' avviati e delle coppie PPID/eseguibile figlio.
- **Attribuzione prudente:** ricostruisce nome probabile del padre attraverso gli eventi storici precedenti; marca **AMBIGUO** se rileva transizioni di vita sul medesimo PID. Non spaccia un PID per identita' permanente.
- CPU e RAM da campioni gia' disponibili (per nome, NON sommate o imputate ai genitori); avvertenze sulla granularita'.
- Lettura SQLite con `SqliteOpenMode.ReadOnly` e `PRAGMA query_only=ON`, nessuna migrazione o scrittura nel database.

## Primo utilizzo, senza aggiornare il servizio
1. Estrarre lo ZIP in nuova cartella.
2. Eseguire `Set-ExecutionPolicy -Scope Process Bypass -Force` e `./build-guardian.ps1`. Il processo di build non installa nulla.
3. In PowerShell amministrativa nella cartella appena compilata, eseguire `./guardian-activity.ps1 -Minutes 30`.
4. Inviare il report, se si desidera analizzare il comportamento dei processi.

**Non e' richiesto eseguire `upgrade-guardian.ps1` per usare l'analizzatore**: legge il database dell'installazione precedente. L'eventuale aggiornamento del servizio e' separato ed esplicito.

## Limitazioni importanti
I timestamp storici sono salvati in secondi, i PID vengono riutilizzati e i campioni CPU/RAM in bucket di cinque minuti non sono attribuiti ai processi padri. L'assenza di un evento non e' una prova di non-esecuzione. Non si acquisiscono righe di comando o percorsi repository; le ipotesi su Git, Claude o altri tool richiedono ulteriori evidenze prima di essere presentate come fatti. Il rapporto si basa sulla disponibilita' del database e sui dati gia' osservati.

## Test
Aggiunto test di analisi read-only con eventi sintetici e casi di PID riutilizzato in `tests/ProcessLens.GuardianStorageTests/Program.cs`. E' necessario verificare build/test reali in Windows con .NET 8 SDK, non disponibile nell'ambiente di preparazione.
