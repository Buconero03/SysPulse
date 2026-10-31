# ProcessLens v0.13.1 — Guardian ETW build fix

## Causa identificata nel log Windows

In `EtwProcessMonitor.Dispose()` la versione 0.13.0 conteneva una riga con `_try` (identificatore inesistente) e `session` (variabile locale di `Start()`, non disponibile in `Dispose()`). Gli errori CS1002 / CS1513 provenivano da questa riga.

## Correzione

La chiusura ETW usa il campo `_session` sotto lock, gestisce eventuali eccezioni di rilascio e poi imposta `_session = null`. L'attesa del task di elaborazione ETW resta limitata a due secondi, senza interrompere il servizio a tempo indeterminato.

`build-guardian.ps1` ora esegue un controllo di build esplicito del servizio *prima* dei test SQLite, cosi' le eventuali diagnosi di compilazione non vengono presentate come fallimenti logici dello storage.

## Verifica su Windows

1. Estrarre l'archivio in una **nuova** cartella `ProcessLens-v0.13.1`.
2. In PowerShell nella cartella di progetto: `Set-ExecutionPolicy -Scope Process Bypass`
3. Eseguire `./build-guardian.ps1`.
4. Se build e test passano, controllare `dist/Guardian-win-x64/ProcessLens.Guardian.Service.exe`.
5. Solo dopo un esito positivo, *se desiderato*, installare da una nuova PowerShell amministratore con `./install-guardian.ps1` e confermare esplicitamente `INSTALLA`.

**Importante:** l'archivio e' stato verificato staticamente, ma non compilato o eseguito in questo ambiente. La versione 0.13.1 non installa servizi quando viene compilata.
