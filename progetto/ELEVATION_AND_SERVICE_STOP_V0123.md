# ProcessLens v0.12.3 — avvio amministratore e arresto dei servizi

## Avvio amministratore automatico (con conferma UAC)

`src/ProcessLens.App/app.manifest` è associato al progetto tramite
`<ApplicationManifest>app.manifest</ApplicationManifest>` e dichiara
`<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />`.

- Ogni avvio dell'EXE richiede i privilegi di amministratore. Windows mostra una normale richiesta UAC o credenziali amministratore, anche dall'Explorer.
- Il consenso NON viene aggirato: se l'utente annulla, l'app non parte.
- `bootstrap.ps1` può essere eseguito da PowerShell non elevato: durante l'avvio dell'EXE apparirà il prompt UAC. Se UAC viene rifiutato, lo script spiega l'errore.
- `publish-release.ps1` produce un eseguibile single-file che incorpora lo stesso manifest; per pubblicare usa Windows e .NET 8 SDK.
- Avvio *con privilegi amministrativi* non significa *avvio automatico al login di Windows*: non viene aggiunto alcun servizio o attività pianificata.
- Un EXE non firmato potrebbe generare ulteriori avvisi di reputazione Windows SmartScreen; non disabilitare le protezioni.

## Arresto dei servizi

- Eliminato il divieto generalizzato per immagini sotto `C:\Windows`.
- Servizi Win32 non critici, inclusi quelli ospitati da `svchost.exe`, possono ricevere una richiesta di arresto per *nome del servizio*.
- Resta il blocco ProcessLens per servizi essenziali, incluso `mpssvc` (Windows Defender Firewall) e altri servizi di sicurezza/infrastruttura.
- Prima di procedere viene chiesta conferma; il controller rilegge i dati dal Service Control Manager e verifica PID, stato e `SERVICE_ACCEPT_STOP`. Il comando STOP è inviato solo se il servizio accetta STOP e Windows concede i diritti.
- I processi condivisi non vengono terminati forzatamente; i driver kernel non sono supportati.
- Amministratore non equivale a diritto universale STOP: alcuni servizi restano protetti tramite ACL, Protected Process Light e policy del sistema.
- Interrompere un servizio può far perdere funzionalità, sessioni remote o interrompere attività dipendenti; prova su un servizio non essenziale e non in uso.

## Verifiche

`bootstrap.ps1` esegue `tests/check-elevation.ps1`, preflight menu contestuale e `tests/ProcessLens.ServiceStopTests`; nessun test arresta servizi reali. Compilazione/runtime richiedono Windows .NET 8 SDK.
