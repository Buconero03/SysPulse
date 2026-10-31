# ProcessLens v0.12.2 — menu contestuale per la scheda Servizi

Seleziona **Servizi**, fai clic destro sulla riga del servizio.

- **Arresta servizio…**: verifica l’ammissibilità, chiede conferma e invia il comando `SERVICE_CONTROL_STOP` al *Service Control Manager*. Non termina forzatamente il PID ospitante. Se il servizio è protetto o fermo, il menu è disabilitato e la motivazione appare al passaggio del mouse.
- **Mostra processo ospitante**: passa alla scheda Processi, filtrata per PID. Per i servizi arrestati o senza processo questa funzione non è disponibile.
- **Copia nome servizio**: copia il nome tecnico SCM.

Il servizio da arrestare viene fissato come oggetto sulla voce del menu quando si fa clic destro; il controllo `WindowsServiceStopController` interroga di nuovo SCM dopo la conferma, inclusi stato e PID. Una modifica dell’elenco o della selezione non deve far arrestare un servizio diverso.

## Sicurezza

- Un servizio e il processo `svchost.exe` che lo ospita sono entità differenti. Arrestare il processo ospitante può interrompere altri servizi.
- La policy v0.12.3 mantiene il divieto sui servizi essenziali e sui driver, ma consente di richiedere STOP per servizi Microsoft non critici, anche se ospitati in svchost.exe.
- Windows può rifiutare `STOP`, per esempio per permessi o perché il servizio non accetta il comando. All'avvio il manifest dell'EXE richiede privilegi amministrativi con conferma UAC; anche un amministratore può ricevere un accesso negato dal SCM.
- AnyDesk e altri servizi remoti possono terminare immediatamente sessioni in corso.

## Verifica

`bootstrap.ps1` esegue `tests/check-wpf-contextmenu.ps1` prima della build, per rilevare il precedente crash XAML da menu associato a uno Style.Setter. Sono necessari build e test su Windows .NET 8, oltre alla prova con un servizio di test non essenziale.
