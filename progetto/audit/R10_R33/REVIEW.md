# ProcessLens R33 — icone e immagini dei gruppi

Base esatta: R32 R2, SHA-256 4eb61f3c4e6954dff8bc2e1ec8ca79155a835664f1734ba185d69c72242ec6b2. L'utente ha confermato il corretto avvio della R32 R2.

## Cambiamento

Il recupero delle icone originali resta invariato: risorse del file, poi associazione Shell; i simboli sono usati quando l'originale manca o è riconosciuto come generico. Rimossi i riquadri con iniziali e il simbolo Windows condiviso dalla selezione visiva.

Nuovo catalogo incorporato: 61 descrittori, 99 nomi eseguibili esatti e 41 forme vettoriali. Sono simboli di ProcessLens, non una libreria di loghi ufficiali né una verifica dell'identità o della firma. I componenti Windows e gli strumenti comuni hanno immagini riconoscibili per console, servizi, accesso, sicurezza, desktop, audio, stampa, aggiornamenti, codice e così via. Il tooltip dichiara la fonte effettiva dell'immagine.

Per i nomi non presenti nel catalogo si disegna un distintivo a celle derivato dal nome normalizzato. Stabile tra aggiornamenti e riavvii, case-insensitive, senza iniziali. Non è un identificatore crittografico e non garantisce assenza di collisioni; nessun verdetto di sicurezza è derivato dal simbolo. I PID dello stesso eseguibile possono condividere l'icona come nelle normali liste di processi.

Per i gruppi: quando il titolo è un alias esplicito, per esempio Git, e il primo eseguibile è Bash, l'illustrazione segue il titolo del gruppo. Il tooltip lo dichiara. Quando il nome dell'eseguibile corrisponde al gruppo, la sua icona originale mantiene la priorità. Nessuna modifica a raggruppamento, ricerca, classificazioni, PID o contatori.

## Limiti di risorse

Le cache restano limitate: 128 risoluzioni native e 128 disegni, come i precedenti limiti. Disegni congelati e condivisi; catalogo letto una sola volta dal manifest dell'assembly, limitato a 128 KiB. Nessun accesso web, scansione di cartelle, enumerazione di finestre/processi o apertura di handle aggiuntiva. Nessun font necessario per disegnare le iniziali. Nessuna nuova dipendenza.

Le modifiche R32 R2 all'icona principale incorporata e ai comandi finestra/tray restano byte per byte identiche, così come i sorgenti Core, Semantics, Windows, Guardian, MemoryAgent e monitor RAM.

## Verifiche attuali

- Due progetti reali SDK/MSBuild compilati ed eseguiti: IconResourceTests 40 asserzioni e ProcessGroupsTests 226 asserzioni, PASS.
- Pubblicazione reale framework-dependent single-file Linux del test delle risorse, eseguita da una cartella senza Assets: 42 asserzioni PASS. Copre anche il nuovo catalogo incorporato.
- Totale 308 asserzioni attuali, non una ripetizione delle 834 storiche della base.
- 43 controlli statici PowerShell e 17 fixture del launcher: PASS.
- Nove assembly C# WindowsDesktop verificati con gli import WPF valutati da MSBuild e campi XAML simulati. Non è compilazione BAML né esecuzione WPF.
- Fixture WPF aggiornata: parsing di ogni geometria senza tollerare fallback silenziosi, immagine congelata con dimensioni limitate, System diverso da Idle, icona Git diversa da Bash e sorgente esplicita.
- Anteprima SVG generata dalle medesime geometrie del catalogo e renderizzata con Inkscape; controllata visivamente. Non è una schermata dell'app WPF.
- Controllo XML, nomi delle risorse realmente valutati da MSBuild, patch applicata e confronto byte, manifest e ZIP.

Avvio, rendering WPF, DPI e memoria RAM della candidata devono essere verificati su Windows. Non è dichiarato un risparmio di RAM né che tutti i processi possiedano un proprio logo originale recuperabile.

## Prova Windows

Chiudere con Esci, estrarre in una cartella nuova e aprire AVVIA_PROCESSLENS.cmd. Verificare Bash, Git e i componenti Windows, i tooltip e il gruppo Git. Controllare che Chrome/Node o altri processi con icona originale la mantengano, che le icone restino stabili durante gli aggiornamenti e che il comportamento del tray resti quello della R32 R2.
