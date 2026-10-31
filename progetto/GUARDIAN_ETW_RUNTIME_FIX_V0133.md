# ProcessLens Guardian v0.13.3 — correzione ETW di runtime

Il log Windows rilevava `System.Exception: The kernel provider must be enabled first and only once in a session.`

**Causa:** `EtwProcessMonitor.Start()` accedeva a `session.Source.Kernel` prima di `session.EnableKernelProvider`. Il getter `TraceEventSession.Source` crea la sessione se non è ancora avviata; il successivo `EnableKernelProvider` falliva perché l'handle ETW risultava già valido. Il codice è stato corretto abilitando il provider PRIMA di ottenere `session.Source`.

La sessione `Circular Kernel Context Logger` non è la causa dimostrata dall'eccezione e non deve essere fermata.

## Procedura sicura

1. Estrarre la v0.13.3 in nuova cartella.
2. In PowerShell normale: `Set-ExecutionPolicy -Scope Process Bypass`, poi `./build-guardian.ps1`. La build **non modifica** il servizio già installato.
3. Se tutti i test, build e publish riescono, in PowerShell **come amministratore** nella stessa cartella: `./upgrade-guardian.ps1`, confermare `AGGIORNA`. Solo il servizio ProcessLensGuardian verrà arrestato brevemente e riavviato; i dati rimangono in `C:\ProgramData\ProcessLens\Guardian`.
4. Consultare il registro Application: `Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=(Get-Date).AddMinutes(-10)} | Where-Object {$_.ProviderName -eq 'ProcessLens.Guardian.Service'} | Select TimeCreated,LevelDisplayName,Message | Format-List`.
5. Il messaggio `ETW kernel provider enabled; process event listener started` in diagnostics indica avvio del listener, non garantisce consegna perfetta di tutti gli eventi. Verificare avviando e chiudendo un processo breve; controllare eventuali messaggi d'errore o eventi persi.

Nessuna disinstallazione è richiesta; usare `uninstall-guardian.ps1` solo se si desidera rimuovere il servizio. `upgrade-guardian.ps1` non aggiorna la GUI e non cancella la cronologia.

**Limitazioni:** la compilazione su Windows e l'acquisizione ETW reale devono essere verificate sull'host; i controlli statici non sostituiscono il test runtime. Un intervallo minimo di eventi tra l'abilitazione del provider e la sottoscrizione potrebbe essere perso. 
