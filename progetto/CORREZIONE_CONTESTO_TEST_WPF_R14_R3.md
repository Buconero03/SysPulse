# R14 R3 — contesto asincrono dei test WPF

Il log Windows della R2 conferma il superamento dei test registro dispositivi e Wireshark, e della selezione iniziale della scheda rete. Il test UI fallisce poi in `UpdateButtons`, dopo il primo aggiornamento asincrono: accesso a un controllo da un thread diverso da quello che lo ha creato.

## Causa e modifica

`[STAThread]` non installa da solo un `DispatcherSynchronizationContext`. Nei test console il metodo `RefreshAsync()` viene avviato prima che `Complete` pompi il Dispatcher. Senza contesto, il seguito dell'await può eseguire sul pool di thread e accedere ai controlli WPF.

I test rete e sistema ora installano il contesto sul thread principale, prima di qualsiasi chiamata asincrona. Un task inizialmente incompleto viene atteso prima di accodare il worker: tre verifiche controllano l'installazione del contesto, il completamento da un thread diverso e la ripresa sul Dispatcher proprietario. Il contesto precedente viene ripristinato in finally. Restano tutti i controlli funzionali e le eccezioni originali.

Le viste e tutto il codice operativo restano identici alla R2. Cambia solo l'identificativo della candidata nel progetto App. Nessuna nuova acquisizione, processo o timer operativo.

## Verifica su Windows

Estrarre la candidata completa in una cartella nuova ed eseguire `./build-dashboard.ps1` da PowerShell. Il test rete deve superare il primo aggiornamento, il salvataggio e il cambio scheda; il test sistema deve completare aggiornamento e rifiuto dei risultati tardivi. I nuovi controlli R14 R3 devono comparire in entrambe le suite.

La pipeline deve terminare tutte le fasi. Questo archivio è una candidata sorgente: WPF, PowerShell, pubblicazione Windows e DPI non sono stati eseguiti su questo host Linux. I controlli Python verificano sorgenti, conservazione delle asserzioni, XML e patch; non sostituiscono il test WPF. Nessuna misura di RAM o CPU nuova.

Audit corrente: `audit/R10_R14_R3`. Gli audit precedenti restano storici. Manifest dell'intero archivio: `SHA256SUMS_R10.txt`.
