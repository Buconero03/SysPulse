# R28: priorità delle icone e comandi della finestra

Base R27, SHA-256 `554bad70574c36e6045bcfcf8fa93b815aff943f6892644ae51815f4d7aedd69`.
Questa copia è stata recuperata dalla R27 salvata e ricostruita dalle modifiche già definite nella sessione. I controlli presenti in R10_R28 sono rieseguiti sulla copia corrente, non copiati dai risultati temporanei precedenti.

## Icone

R27 assegnava il simbolo Windows anche agli eseguibili con icone proprie. R28 sceglie prima l'icona originale restituita dalla Shell. Il simbolo Windows resta un ripiego se l'icona originale manca o coincide con quella generica degli eseguibili, e se il criterio R27 classifica il componente come Windows. Esplora risorse e gli altri eseguibili con immagine distinta mantengono quella immagine.

La genericità viene confrontata dal contenuto BGRA32, non dal valore HICON o dall'identità dell'oggetto. Il confronto richiede dimensioni uguali, massimo 256×256 pixel, e un numero di byte limitato. Se il confronto non è disponibile si conserva l'icona nativa. Avviene al caricamento nella cache, non ad ogni campione CPU/RAM. Cache: 128 percorsi. Le immagini generiche vengono ricondotte allo stesso bitmap condiviso; il simbolo Windows è una sola DrawingImage congelata. L'HICON continua a essere rilasciato nel finally del resolver.

La classificazione Windows non cambia e non autentica un file. Nessuna nuova acquisizione di processi, firme, rete o moduli. Il resolver consulta la Shell per le immagini, con una sola icona generica di riferimento aggiuntiva. Nessun consumo o risparmio RAM misurato.

## Riduzione

Il problema dell'utente non è stato riprodotto su Windows nell'ambiente Linux; non si dichiara provata una singola causa o già confermato il risultato sul suo PC. Sono rimosse interferenze concrete dal percorso:

- Il clic non scrive più due registri sincronicamente prima di ridurre e non attende un ulteriore turno Dispatcher. I messaggi delle azioni sono accodati: massimo 8 pendenti, un worker, sink eseguito fuori dal lock della coda. Fallimenti di sink/scheduling e coda piena non impediscono l'azione.
- WM_SYSCOMMAND e WM_CLOSE passano a Windows/WPF. Il pulsante nativo «–» usa il comportamento standard. X passa da Closing, che annulla la chiusura e differisce la riduzione fino al ritorno del callback.
- Il pulsante dell'app usa subito WindowState.Minimized; soltanto Closing e callback nativi differiscono l'azione. La richiesta immediata annulla un vecchio ripristino pendente.
- Conferma e fallback leggono l'handle della finestra corrente. CloseWindow opera solo sull'HWND posseduto e riduce senza distruggere la finestra. IsIconic conferma lo stato effettivo. Un successivo ripristino annulla una vecchia verifica pendente.
- StateChanged/IsVisibleChanged accodano un solo aggiornamento della pausa del monitoraggio a Background. Non riscrivono tutte le righe durante il callback nativo della transizione.
- Un esito non confermato mostra un avviso e registra stato WPF, handle ed errore Win32 disponibile. Il precedente messaggio di successo incondizionato è rimosso.

La scrittura è best effort: una riga può mancare con coda piena, sink o scheduling non disponibili. Il timestamp del registro è quello di scrittura; lo stato descritto viene catturato dal callback GUI. ShowInTaskbar resta true: X riduce, Esci e fine sessione chiudono. Non è introdotta una sparizione senza ripristino dalla taskbar.

## Gate Windows sulla MainWindow reale

La build ordinaria esegue ora `Tests.ProcessLens.MainWindowShell`, invocando `ProcessLens.ProcessGroupsUiTests --window-smoke`. Usa la vera MainWindow, il vero TrayWindowController e i veri pulsanti, con 100 righe sintetiche e un dettaglio simulato. Raccolta all'avvio e ripresa del sampler sono disabilitate per la fixture; il reale ramo di pausa dell'interfaccia resta esercitato.

Controlla tre cicli di clic sul pulsante reale con IsIconic prima di un ulteriore turno Dispatcher, pausa/ripresa della UI, SC_MINIMIZE non intercettato, riduzione/ripristino da ingrandita, X e pulsante Esci. Opera solo sulla propria finestra/icona, senza processi/rete reali, SCM o clipboard. Mostra una finestra di prova e richiede il desktop Windows. Le attese/layout hanno un limite di cinque secondi ciascuna. Se fallisce, il launcher non crea la ricevuta R28 e non avvia un eseguibile precedente.

**Questo gate è incluso e controllato come C#, non eseguito nell'ambiente Linux.** La prova effettiva sul PC resta necessaria. Clic fisico sul tray, Explorer riavviato e DPI restano verifiche Windows.

## Verifiche correnti

Risultati: 737 asserzioni portabili, 35 controlli PowerShell, 17 fixture launcher, 4 controlli negativi, 14 controlli del guard risorse, 9 assembly verificati come C#. I risultati effettivi sono nei JSON e nei log di questa cartella. I registri R27 restano storici. La verifica comprende le 13 suite portabili, il preflight PowerShell e il launcher, i quattro controlli negativi su copie di produzione, il guard risorse, il controllo C# WindowsDesktop e il confezionamento. Non equivale a una build WPF/BAML o a esecuzione Windows.

I quattro errori introdotti appositamente sono: logo Windows sopra un'icona distinta, clic rimandato, vecchio ripristino mantenuto e scrittura sincrona del registro. Ognuno deve produrre il fallimento previsto.

Core, semantica, acquisizioni Windows, Guardian, profiler, MemoryAgent e monitor RAM sono identici alla R27. Layout XAML e guard di scorrimento sono identici. Cambiano priorità delle icone, comandi della finestra, diagnostica delle azioni e relativi test/script.

## Uso sul PC

Chiudere la vecchia GUI con Esci, estrarre in cartella nuova e aprire AVVIA_PROCESSLENS.cmd. Durante la prima build compare la finestra di prova e viene ridotta/riaperta. Nella GUI verificare icona di Esplora risorse e degli altri eseguibili con risorsa propria, host generici, Riduci, «–», tray, X ed Esci. Ripetere durante gli aggiornamenti CPU/RAM e con un processo selezionato.

Per ripetere il test dalla cartella progetto:

```powershell
dotnet run --project tests/ProcessLens.ProcessGroupsUiTests -c Release -- --window-smoke
```

Il log locale è `%LOCALAPPDATA%\ProcessLens\logs\startup.log`: cercare `Clic Riduci a icona R28` e `Riduzione R28: confermata=`. Mancanza di una riga non prova mancanza del clic perché la diagnostica è limitata e best effort. Non servono nuove installazioni.

Riferimenti Microsoft: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/ e https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-closewindow .
