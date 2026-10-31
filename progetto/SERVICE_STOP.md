# Arresto ordinato dei servizi — aggiornato v0.12.3

Il menu contestuale dei PID con un solo servizio ospitato offre **Arresta servizio Windows…**, non *Termina processo*.
In vista **Servizi**, scegli il nome esatto e usa **Arresta servizio…**.

- Comando inviato al **Service Control Manager** tramite OpenService, QueryServiceStatusEx e ControlService(SERVICE_CONTROL_STOP).
- Nessuna chiamata a TerminateProcess o terminazione di alberi per i servizi.
- Conserva le protezioni dei servizi essenziali e dei driver. Non blocca indiscriminatamente i servizi Microsoft o i servizi ospitati da svchost.exe: Windows decide se un servizio non critico accetta STOP.
- Richiede che SCM confermi `SERVICE_ACCEPT_STOP`; servizio e PID ricontrollati prima dell'azione.
- Chiede conferma; il manifest dell'EXE richiede automaticamente privilegi amministrativi con normale prompt UAC. Windows può comunque negare STOP.
- Un PID che ospita più servizi non viene arrestato automaticamente: seleziona un servizio specifico nella scheda Servizi.
- Interrompere AnyDesk può chiudere sessioni remote. Il servizio potrebbe riavviarsi.

Test deterministici in `tests/ProcessLens.ServiceStopTests`: non arrestano davvero servizi.
Prova manuale su Windows con un servizio non essenziale che puoi permetterti di interrompere.

Versione: 0.12.3
