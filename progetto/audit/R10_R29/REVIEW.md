# R29: diagnostica della riduzione e seconda lettura delle icone

Base: archivio R28 consegnato, SHA-256 `a70a8c2948e137df85f93203a820fd47c6418ccf6906063bb361c6369d01698c`.
Richiesta: il problema di riduzione persiste. Rendere osservabile il percorso del clic e migliorare le icone originali. Non ripetere la revisione precedente.

## Risultato e limiti

Nessuna causa del problema Windows viene dichiarata accertata. R29 aggiunge osservazione, senza sostituire ancora la politica R28 di riduzione, ripristino e scorrimento. I comandi nativi restano non intercettati; gli osservatori non impostano Handled.

La MainWindow possiede un journal e un osservatore. Gli eventi in input sono osservati anche se marcati gestiti. Si registra solo il tipo di input, non il testo dei tasti o dei controlli. I dati comprendono timestamp dell'evento, ordine, sessione, tentativo, stato WPF, HWND corrente e IsIconic/visibilita/primo piano dell'owned window, stato del pulsante e finestre figlie. Il controller aggiunge richiesta, ingresso/ritorno, conferma o ragione di verifica superata e origini note del ripristino. Le eccezioni del clic riportano tipo e HRESULT, senza argomenti del processo.

Un solo Timer posseduto viene armato dopo il tentativo, a 2 secondi. Il callback background registra UI_CHECK_QUEUED prima di postare il controllo alla GUI; UI_CHECK arriva solo quando il dispatcher lo esegue. Non legge DependencyObject dal worker. Se un altro tentativo lo supera, non gli attribuisce lo stato del successivo. Il Timer e gli handler vengono rilasciati alla chiusura. Nessun heartbeat o campionamento continuo.

Il rapporto mostra le ultime 256 righe, resta selezionabile e copiabile, e si aggiorna su richiesta. La finestra e modeless: non disabilita il proprietario. Il file locale window-actions.log usa una coda background limitata a 256 eventi, ruota sopra 1 MiB e mantiene una sola copia precedente. Il report mostra righe scritte, scarti e ultimo errore del writer. I timestamp vengono catturati al momento dell'evento, non al drain del writer. Un errore del file non elimina il journal in memoria. La scrittura e best effort; non si promette ogni evento dopo un crash.

L'icona distinta dello Shell ha precedenza. Quando manca o coincide con la generica, un resolver lazy prova ExtractIconExW sul percorso assoluto di unita dell'eseguibile presente nel campione. Nessun percorso dedotto dal nome, download, scansione, processo avviato o privilegi aggiuntivi. Gestisce ritorno 0/UINT_MAX, congela la bitmap e rilascia l'HICON in finally. Un'icona incorporata generica non viene dichiarata originale. Cache 128 anche per risultati mancanti. In mancanza di risorse resta il ripiego dichiarato; non viene inventato un marchio.

## Verifica

- 13 suite portabili, 755 asserzioni, eseguite su Linux con Roslyn SDK 8.0.426 e runtime 8.0.32: dati sintetici/fixture possedute.
- 7 regressioni deliberate rifiutate dai test runtime: le 4 R28 piu precedenza errata dell'icona generica, journal illimitato e formato temporale dipendente dalla cultura.
- 36 preflight PowerShell, 17 fixture launcher; 14 controlli risorse con mutazioni.
- C# di 9 assembly WindowsDesktop verificato con reference assemblies e stub generati fuori dal pacchetto. Non e MSBuild/XAML/BAML ne esecuzione WPF.
- La prova Windows esistente della vera MainWindow e ampliata: log controller/stato, probe a 2 s, comando nativo osservato, finestra del log modeless. Inclusa nella build, non eseguita qui.
- Layout grafico, clic fisico, shell/tray/DPI, risorse icona reali e consumo RAM restano da verificare su Windows. Non si dichiarano risparmi RAM.
- Manifest, scope, patch e risultati nei JSON/CSV di questa cartella. I registri R28 e precedenti sono storici, non nuove esecuzioni.

## Fonti tecniche primarie consultate

- https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-extracticonexw : ritorni 0/UINT_MAX e obbligo DestroyIcon.
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/preview-events : AddHandler handledEventsToo osserva gli eventi gestiti senza modificarli.

## Riproduzione

Vedere LEGGIMI.txt. Dopo un tentativo attendere almeno tre secondi; aprire Log finestra, Aggiorna, Copia tutto. Se la GUI resta bloccata, il file locale puo contenere l'ultimo passo e UI_CHECK_QUEUED senza UI_CHECK. Questo localizza il tratto mancante ma non dimostra da solo la causa.
