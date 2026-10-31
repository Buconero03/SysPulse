# ProcessLens Guardian v0.13 — Collector Service (Windows x64)

## Stato

**Implementazione della fase A**: servizio Windows separato dalla GUI ProcessLens v0.12.3, ETW process start/stop + riconciliazione snapshot, CPU e RAM ogni 15 s, inventario TCP/UDP IPv4/IPv6 ogni 60 s, SQLite locale e rapporto giornaliero Markdown. Nessuna installazione avviene durante una build. Nessuna terminazione automatica dei processi, nessuna classificazione automatica di malware.

Questo pacchetto è **sorgente**. I moduli Guardian e i pacchetti NuGet richiedono compilazione sul tuo Windows con .NET 8 SDK e accesso a NuGet. Verificare build e test prima dell'installazione. La GUI non mostra ancora i dati di Guardian: questa è l'evoluzione della successiva fase B.

## Avvio guidato (PowerShell, nella root del progetto)

1. Compila la GUI opzionalmente: `./bootstrap.ps1` (avvia ProcessLens, **non** installa Guardian).
2. Costruisci Guardian ed esegui test: `./build-guardian.ps1`. Crea `dist/Guardian-win-x64/ProcessLens.Guardian.Service.exe` autonomo e la configurazione.
3. **Apri PowerShell come amministratore**, esegui `./install-guardian.ps1`, leggi cosa verrà installato e digita esattamente `INSTALLA` quando richiesto. Windows creerà e avvierà `ProcessLensGuardian`, **avvio automatico al boot**.
4. Verifica con `./guardian-status.ps1` (come amministratore). Dopo mezzanotte UTC leggi il rapporto precedente in `%ProgramData%/ProcessLens/Guardian/reports/YYYY-MM-DD.md`. Gli orari del database sono **UTC**.
5. Rapporto immediato (anche oggi): `./guardian-report.ps1` da PowerShell amministratore, opzionalmente `./guardian-report.ps1 -UtcDate 2026-10-09`.
6. Per rimuovere: `./uninstall-guardian.ps1` (come amministratore) e digita `RIMUOVI`. **I dati vengono conservati**, a meno che non si usi `-DeleteData` con ulteriore conferma `CANCELLA DATI`.

Per una prima prova **non disabilitare antivirus, firewall o servizio di sicurezza**. Guardian raccoglie osservazioni e non apre porte d'ascolto.

## Protezioni e limiti

- Account Windows: **LocalSystem**, privilegi elevati richiesti per provider kernel ETW. Questo è un componente di elevata fiducia; installare solo dopo verifica dei sorgenti. Binario sotto `%ProgramFiles%/ProcessLens/Guardian`; configurazione modificabile solo dagli amministratori.
- Database/report sotto `%ProgramData%/ProcessLens/Guardian`; lo script applica ACL solo SYSTEM/Administrators. Non trasmette dati a terzi.
- **ETW** monitora avvii e terminazioni. Se la sessione kernel non può essere creata (conflitto o privilegi), Guardian continua a intervalli regolari; i processi rapidissimi possono essere persi. Le anomalie di raccolta vengono salvate in `diagnostics`.
- All'avvio Guardian crea l'inventario dei processi e delle porte già presenti: è etichettato `ObservedAtStartup`, **non** come avvio/apertura avvenuti in quel momento.
- **Rete**: IP Helper registra *fotografie* di porte TCP/UDP, IPv4/IPv6; eventi nuovi/chiusi sono **differenze tra campioni**, non un tracciato completo di tutti i pacchetti. Stato TIME_WAIT/CLOSED escluso per evitare false attribuzioni e rumore. Gli IP remoti non vengono archiviati per impostazione predefinita (ma le porte remote sì).
- **CPU**: percentuale rispetto alla capacità CPU logica totale. Se i contatori non sono leggibili viene salvato NULL, mai un falso zero. La prima misura CPU per PID non può essere disponibile.
- **RAM**: usa `Process.WorkingSet64` (residente **totale**, inclusa memoria condivisa) e `PrivateMemorySize64` (**commit privato**); la v0.12.3 GUI usa anche il private working set quando disponibile. Non confondere le grandezze.
- **Relazioni PID**: ETW può emettere un record senza start timestamp con sentinella 0; il campionamento aggiorna l'identità. Un parent PID da Toolhelp da solo non prova la causa o l'origine dell'avvio.
- **Privacy**: process names, porte e indirizzi locali sono dati sensibili; in futuro si potrà prevedere un'interfaccia per escludere processi. Per questa versione non raccoglie command line, contenuti di file, keystrokes, payload di rete o stack delle chiamate.
- Conservazione configurabile `RetentionDays: 14`, quota **soft** `MaxDatabaseMb: 256`. Il WAL può temporaneamente superare la quota, che viene ricalcolata durante la manutenzione giornaliera. Nessun tetto assoluto garantito in caso di traffico/eccezioni anomali.
- Non vengono analizzati i pacchetti di rete e non viene attribuita intenzione malevola in base a un solo indizio. Report informativi; nessuna risposta automatica.

## Schema database

- `process_events`: PID, parent PID, timestamp UTC, start timestamp quando noto, start/stop/inventario + fonte ETW o poll.
- `resources`: bucket 5 minuti, PID, start ticks, CPU sum/count/peak e picchi RAM residente totale/commit privato.
- `port_events`: porta locale, protocollo e stato, PID, porta remota, opzionalmente IP remoto.
- `diagnostics`: ETW non disponibile, perdita di eventi in coda, errori di scansione.
- Il report UTC precedente viene scritto in Markdown all'avvio e alla prima raccolta del giorno UTC successivo.

## Test

`build-guardian.ps1` esegue `GuardianTests` (regole e riconciliazione processi/porte, PID reuse, CPU, limiti) e `GuardianStorageTests` (SQLite/UPSERT/report/retention) prima del publish autonomo. **Occorre comunque un test reale sul tuo PC e confronto di overhead**. Eventuale crash: Visualizzatore eventi Windows → Applicazione / .NET Runtime / ProcessLensGuardian.

## Parametri

`src/ProcessLens.Guardian.Service/guardian.settings.json` prima della pubblicazione. Valori min/max applicati lato runtime. `CaptureRemoteAddresses = true` archivia IP remoti soltanto dopo modifica esplicita del file, necessaria una nuova installazione.

**Nota**: la v0.13 è una fondazione e non sostituisce un EDR o un antivirus. Le vere regole di valutazione delle anomalie e la consultazione grafica delle serie storiche sono pianificate per v0.14/v0.15.
