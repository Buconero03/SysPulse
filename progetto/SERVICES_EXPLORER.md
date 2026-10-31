# ProcessLens 0.10.0 — Servizi Windows (sola lettura)

Questa versione introduce la vista **Servizi**, separata dalla vista **Processi**.

## Evidenze e fonti

- **SCM / EnumServicesStatusExW**: enumera i servizi Win32 installati (attivi e arrestati), il loro stato e il PID ospitante se disponibile.
- **HKLM\SYSTEM\CurrentControlSet\Services**: tipo di avvio, avvio automatico ritardato, percorso binario, account e dipendenze.
- **QueryServiceConfig2W**: descrizione del fornitore quando il registro non fornisce una descrizione utilizzabile.
- **SHLoadIndirectString**: localizza, se accessibile, le descrizioni Windows espresse tramite risorse indirette `@...`.
- **ProcessLens process snapshot**: usa PID -> nome per collegare servizi e processi. Un `svchost.exe` può ospitare più servizi.

## Visibilità e sicurezza

L'elenco è **read-only**: non cambia configurazione né arresta/avvia servizi. La vista si aggiorna su richiesta e al massimo ogni ~20 secondi; lo snapshot processi continua ad aggiornarsi in modo indipendente.

* `Automatico` NON prova che Windows abbia avviato il servizio durante il boot corrente: l'impostazione potrebbe essere cambiata.
* `Manuale` NON vuol dire che un utente abbia avviato manualmente il servizio: trigger, servizi dipendenti e applicazioni possono farlo.
* PID del servizio identifica il processo host, NON l'applicazione che ha ordinato l'avvio e NON identifica necessariamente quale sottoservizio usa CPU in un processo host condiviso.
* Le dipendenze dichiarano requisiti di configurazione, NON una catena di causalità osservata.
* Senza storia ETW/event log non si può attestare l'iniziatore o l'ora esatta dell'ultimo avvio del servizio.
* L'API SERVICE_WIN32 copre i servizi Win32, non l'elenco dei driver kernel (`SERVICE_DRIVER`).

## Come usare

1. Clicca **Servizi** nella barra principale.
2. Cerca per nome visualizzato, nome tecnico, PID, descrizione, stato o tipo di avvio.
3. Filtra **Tutti / Attivi / Arrestati** e ordina per stato+nome, nome, avvio o PID.
4. Seleziona un servizio e leggi ruolo dichiarato, stato, perché può essere in esecuzione, percorso, account e dipendenze.
5. Usa **Mostra processo ospitante** per tornare ai processi.
6. Dalla vista **Processi** puoi usare **Mostra servizi Windows ospitati dal PID**.

Se il servizio non ha una descrizione leggibile, ProcessLens lo dichiara, senza inventare una funzione.
