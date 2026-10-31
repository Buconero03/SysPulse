# ProcessLens R32 R2 — icona incorporata e facoltativa

Base R32 R1: ff5e50322cd46997ad056b118b0abd76f27d88854bbdf8bbeac32db963053909.

## Problema osservato

Il log utente registra XamlParseException in MainWindow.InitializeComponent, con ArgumentNullException per path1 da ContentFilePart.GetStreamCore. La finestra non arriva alla creazione del controller tray. Non è una prova di stallo nel campionamento CPU/RAM o nel tray. La dichiarazione Icon relativa nel XAML e la doppia classificazione Resource/Content dell'ICO introdotta in R32 permettevano il percorso ContentFilePart; i test della DLL prima della pubblicazione non esponevano il comportamento del file unico.

publish-release.ps1 pubblica già con PublishSingleFile=true. Non è modificata questa scelta. Non sono inclusi log o screenshot personali nell'archivio.

## Correzione

- Eliminato l'attributo Icon dal XAML della finestra.
- L'ICO resta identico, ma è una sola EmbeddedResource con LogicalName ProcessLens.AppIcon. Nessuna dichiarazione Content/Resource WPF per l'icona.
- ProcessLensIcon carica i byte dal manifest della propria assembly, decodifica con OnLoad, congela l'immagine e la condivide. Tutte le operazioni facoltative sono protette: errore icona non impedisce l'apertura della finestra.
- Il tray seleziona la risorsa ICO da 32 pixel e usa CreateIconFromResourceEx, senza cercare Assets sul disco. In caso di errore conserva il ripiego Windows. HICON posseduto rilasciato con DestroyIcon come prima.
- Validazione ICO limitata a 256 KiB e 64 immagini, con controlli offset/lunghezze e dimensione richiesta. Solo la risorsa dell'assembly, nessun file utente letto.
- Nuovo test nel build-dashboard per l'incorporamento effettivo e il formato dell'icona. Aggiornata la fixture WPF per immagine congelata e risorsa opzionale assente/malformata.
- Ricevuta R32 R2 e versione aggiornate per evitare l'avvio della precedente pubblicazione.

Nessuna modifica ai classificatori, ai contatori CPU/RAM, alle acquisizioni native dei processi/rete, a Guardian, alla struttura della UI o al comportamento delle azioni di nascondimento e ripristino.

## Prove eseguite qui

- SDK/MSBuild sul progetto reale: l'ICO è EmbeddedResource con il nome dichiarato e non è Content.
- Nuovo progetto IconResourceTests, effettivamente compilato con SDK8/MSBuild ed eseguito: 17 asserzioni PASS, zero warning/errori.
- Pubblicazione reale framework-dependent single-file linux-x64 del medesimo test e avvio da cartella isolata senza Assets: 19 asserzioni PASS. Assembly.Location è vuota e i byte incorporati restano leggibili. Non è un'esecuzione WPF Windows.
- 42 controlli statici PowerShell e 17 fixture launcher: PASS.
- Nove assembly C# WindowsDesktop verificati con import WPF valutati da MSBuild e campi XAML simulati: PASS. Non è compilazione BAML o esecuzione dei test WPF.
- Patch applicabile e confronto byte, XML, manifest e integrità ZIP.

Le 834 asserzioni storiche della R32 non sono rieseguite. La decodifica WPF/nativa, l'avvio dell'app pubblicata Windows e il clic fisico restano da verificare sul PC dell'utente. Nessuna misura RAM o latenza dichiarata.

## Verifica Windows

Chiudere il messaggio con OK, estrarre in cartella nuova, aprire AVVIA_PROCESSLENS.cmd. Verificare l'apertura della finestra e poi il pulsante Nascondi accanto all'orologio e il menu Mostra ProcessLens. Non serve reinstallare le dipendenze.

## Riferimenti tecnici Microsoft

https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-createiconfromresourceex
