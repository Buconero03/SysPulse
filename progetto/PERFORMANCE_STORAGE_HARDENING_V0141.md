# ProcessLens v0.14.1 — Guardian Performance & Storage Hardening

## Base verificata

Basato integralmente sui sorgenti v0.14.0. Lo schema SQLite rimane identico; l'analizzatore v0.14.0 rimane disponibile. Il servizio v0.13.3 installato non viene alterato da build o report.

## Difetto di quota corretto

In v0.14.0, `GuardianStore.Maintenance` usava `new FileInfo(...).Length` dopo ogni DELETE: SQLite non riduce necessariamente il file fisico e il codice poteva eliminare 72 ore di storico nonostante la quantità di dati *logicamente* rimossa. v0.14.1 consulta `PRAGMA page_count`, `PRAGMA freelist_count`, `PRAGMA page_size`, protegge l'ultima ora, pone un limite di 24 passaggi per ciclo, compatta solo quando necessario e segnala esplicitamente il superamento della **quota software morbida**. La stima delle pagine vive non è un limite esatto: la frammentazione interna, le query concorrenti e il WAL possono lasciare lo storage oltre quota. Non si garantiscono né 14 giorni di conservazione in condizione di quota insufficiente né 256 MiB come limite rigido.

## Perdite di eventi distinguibili

Il collector conta separatamente eventi scartati dalla coda bounded (capacità 8192) e batch persi per errore di transazione SQLite. I contatori vengono trasformati in righe `diagnostics`, con mirror nel log del servizio Windows; se SQLite non consente il salvataggio della diagnostica, i contatori vengono conservati in memoria per un nuovo tentativo e l'errore viene registrato nel log. **ETW può comunque perdere eventi prima della callback**: non esiste una garanzia end-to-end al 100%, e le perdite non rilevabili da ETW non possono essere inferite dagli zeri nel database. Se il writer SQLite muore, il collector non continua a lungo a fingere di essere operativo.

## Report senza installazione

Dopo `build-guardian.ps1`:

```powershell
.\guardian-health.ps1 -Minutes 30
.\guardian-health.ps1 -Minutes 60 -OutputFile '.\guardian-health.md'
.\guardian-activity.ps1 -Minutes 30
```

Report e activity sono CLI separati in sola lettura dal database esistente `C:\ProgramData\ProcessLens\Guardian\guardian.sqlite3`; non richiedono aggiornamento del servizio installato. **Per attivare il nuovo comportamento di retention e la telemetria nel servizio continuo, serve eseguire `upgrade-guardian.ps1` come amministratore e confermare `AGGIORNA`** dopo la build e i controlli.

## Migrazione e sicurezza

- Nessuna migrazione dello schema, nessuna cancellazione del database durante il deploy.
- Lo script di upgrade crea un backup dei binari in `C:\ProgramData\ProcessLens\GuardianDeployBackups`.
- Il job di manutenzione può eliminare righe più vecchie dell'ultima ora sotto pressione di quota, come da policy; prima dell'aggiornamento si consiglia un backup SQLite online se lo storico è importante.
- All'avvio viene eseguita la normale manutenzione giornaliera: in sistemi con molte righe può introdurre latenza e backlog; monitorare `diagnostics`.
- Test di `Guardian.Core`, `GuardianStorageTests`, build e publish devono essere eseguiti sul PC Windows con .NET SDK 8. Il pacchetto non equivale a una validazione di 24 ore in produzione.
