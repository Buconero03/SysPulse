# R30 — callback dell'icona nell'area notifiche

Base: R29 consegnata, SHA-256 `4cb332965fb2e8fec37056547d0202e7448edd7fecf16eb738f9bb99683d04a7`.

## Problema e riscontro

L'utente chiarisce, con screenshot del riquadro notifiche, che il problema riguarda il clic sull'icona di ProcessLens accanto all'orologio. La precedente indagine era centrata sul pulsante della MainWindow. Il log R29 fornito mostra INPUT/CLICK/controller e stato Minimized/IsIconic=true in circa 10 ms, ancora ridotto al controllo dopo 2 s. Mostra inoltre un comando del menu tray su una finestra gia ridotta e successivamente SC_RESTORE. Non contiene TRAY_INPUT per un clic sinistro. R29 registrava solo gli eventi gia accettati: questa assenza non distingue callback non ricevuti da callback ignorati. Non si conclude che Explorer non abbia spedito il messaggio.

Non si include nello ZIP la schermata o la copia del log personale. Questa sintesi conserva solo il risultato tecnico utile.

## Modifica

Nuovo TrayCallbackRouter puro: separa il formato legacy (ID in wParam, notifica in lParam) dal formato v4 (notifica LOWORD(lParam), ID HIWORD(lParam), coordinate in wParam). Valida l'ID 1 posseduto e rifiuta bit superiori malformati. La negoziazione della versione rimane registrata, ma non decide una whitelist che esclude WM_LBUTTONUP.

La notifica di rilascio sinistro e gestita anche nel formato v4. Restano NIN_SELECT, NIN_KEYSELECT e doppio clic. Il gate temporale preesistente, rimasto identico, sopprime le notifiche duplicate entro GetDoubleClickTime; l'azione differita mantiene le transizioni fuori dal callback nativo. Per decidere ripristino/riduzione si usa lo stato WPF insieme a IsIconic sul HWND corrente. Il menu destro resta separato. Nessun messaggio della barra del titolo viene intercettato.

Il controller registra TRAY_CALLBACK prima di filtrare, inclusi eventi senza azione, formato, ID, versione negoziata e parametri grezzi. WM_MOUSEMOVE non riempie il journal. TRAY_DECISION spiega l'azione, il duplicato o il mancato riconoscimento. Ogni azione accettata o scelta del menu avvia un tentativo dedicato e il probe dopo 2 secondi. TRAY_FILTER e TRAY_REGISTER espongono esiti API effettivi. NATIVE_RESULT dichiara ora se il fallback CloseWindow e stato realmente chiamato: errore zero non era prova di chiamata. La conferma resta nei campi di stato Windows.

Nessun HWND/processo/polling aggiuntivo, nuova libreria o acquisizione nativa di processi/rete. Layout principale e recupero delle icone eseguibili R29 invariati. Il testo iniziale del rapporto separa in tre righe il percorso tray da quello del pulsante.

## Verifica e confini

- 13 suite portabili, 787 asserzioni su Linux con SDK Roslyn 8.0.426 e runtime 8.0.32. 32 nuove asserzioni coprono formato packed/legacy, coordinate negative, rilascio sinistro, selezione, tastiera, ID diversi, bit malformati, hover/balloon/menu e sequenze duplicate in entrambi gli ordini attraverso il vero gate e DeferredWindowActions.
- 10 controlli negativi runtime: 7 R29 piu rimozione WM_LBUTTONUP, accettazione ID diverso e mancata estrazione LOWORD. I test falliscono con la regressione attesa.
- 37 preflight PowerShell e 17 fixture launcher; 14 controlli risorse con mutazioni.
- C# di 9 assembly WindowsDesktop verificato con reference assemblies e stub dei campi XAML, fuori dal pacchetto. Non e compilazione MSBuild/XAML/BAML o esecuzione WPF.
- Fixture della vera MainWindow ampliata: invia i callback documentati al proprio HWND/hook, verifica una riduzione per coppia up/select, ripristino al clic successivo, formato legacy, tastiera, ID diverso e hover. Inclusa nel gate Windows esistente, NON ESEGUITA su Linux. Non simula un clic fisico in Explorer e non ne certifica l'esito. La fixture non mostrata usa MainWindow(false), evitando scritture diagnostiche e confronti instabili con contatori background. L'attesa tra clic sintetici rispetta il tempo di doppio clic configurato da Windows con deadline limitata.
- Shell/tray, elevazione, DPI e consumo RAM reali restano da verificare su Windows. Non viene dichiarata risolta la situazione specifica del PC prima del riscontro fisico.

## Riferimenti primari

- https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataw : layout wParam/lParam v4 e legacy, mouse notifications e ID.
- https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyicona : NIM_SETVERSION, ritorno bool, notifiche selezione/tastiera.
- https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-lbuttonup : notifica di rilascio sinistro.

## Prova sul PC

Chiudere la vecchia istanza con Esci, estrarre in cartella nuova, avviare AVVIA_PROCESSLENS.cmd. Provare proprio l'icona nel riquadro accanto all'orologio: un clic per ridurre, uno successivo per riaprire. Se non risponde, attendere 3 s, aprire il rapporto dalla barra applicazioni e copiare dopo Aggiorna. Leggere callback, decisione e stato senza dedurre una causa dalla sola assenza di eventi.
