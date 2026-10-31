# R31 — ricerca, host Windows e finestre dell'icona

Base: R30 consegnata, SHA-256 `6331971ce39d46cf3653abfec31d08b2d74fc0b50cec3d8f8e1cd6113564f5b5`.

## Riscontro ricevuto e interpretazione

Letti il testo completo e le due schermate fornite. Il log registra la X e il pulsante, entrambi seguiti da WM_SIZE 1, stato WPF Minimized e IsIconic=true anche dopo due secondi. Registra callback destri v4 0x0204, 0x0205 e 0x007B, poi la scelta Riduci su un HWND gia ridotto. Non registra callback sinistri. Quindi non prova un fallimento dell'API di riduzione o una cattura errata del clic sinistro. All'apertura del menu, il codice R30 chiamava SetForegroundWindow sul proprietario ridotto; nel log il proprietario diventa attivo pur restando iconico. Questo e un comportamento indesiderato verificabile nel sorgente, non una causa globale del problema dichiarata provata.

La seconda schermata mostra anche un log modeless aperto separatamente. Le domande opzionali su quale finestra restasse visibile e sul tipo di errore ricerca non hanno ricevuto risposta. Si e proceduto con correzioni circoscritte: focus del campo, query dichiarata, menu non attivante il proprietario e gestione esplicita della sola finestra diagnostica posseduta. Nessun cambio della politica X/tray/taskbar o della struttura generale.

Non si includono nello ZIP immagini o log personali ricevuti; questa sintesi conserva solo gli eventi utili.

## Modifiche

- La superficie Cerca porta il focus al campo quando il clic non e gia gestito dall'editor. Bordo e cursore visibili; Ctrl+F usa ApplicationCommands.Find e seleziona il testo. Clic e selezione dentro l'editor restano normali. Stato Nessun risultato con query visibile; nessuna query aggiunta ai log diagnostici.
- WindowsHostReference e un dizionario locale immutabile di quattro schede con fonti primarie Microsoft. Confronto esatto del nome eseguibile, non del percorso o di sottostringhe. Prevhost e SmartScreen hanno una regola esplicitamente qualificata dal nome e un motivo di avvio sconosciuto. Il contesto esistente aggiunge le fonti alle descrizioni; non cambia i dati dello snapshot o la provenienza SCM. Anche il titolo del gruppo svchost mostra i servizi SCM effettivi, e questi nomi sono ricercabili. Nelle righe dei servizi vengono mostrati i nomi disponibili (due con ellissi), non nomi dedotti. I titoli del dizionario sono ricercabili senza ampliare PID, padre, argomenti e percorsi. Per gli altri processi restano i criteri R30.
- Il catalogo finito Sistema Windows passa da 29 a 31 voci, con prevhost.exe e smartscreen.exe ai percorsi previsti; prevhost supporta anche il percorso SysWOW64 esplicito. Non si allarga il filtro a tutte le applicazioni presenti nel percorso Windows. Indici riservati System/Idle invariati; semantica RAM invariata.
- Il menu destro e accodato fuori dal callback e aperto una sola volta per la coppia right-up/context-menu. Il primo piano viene chiesto al popup del menu, mai al proprietario ridotto. La voce Riduci e disabilitata se proprietario e log sono gia ridotti/nascosti, con tooltip; Mostra resta disponibile.
- Il log viene nascosto esplicitamente quando si riduce il proprietario, e ripristinato con ShowActivated=false e il precedente stato normale/ingrandito. Anche le transizioni native del proprietario accodano una sincronizzazione limitata. Nessuna finestra estranea, dialogo di nuova attivita o altro processo viene chiuso. Nessuna nuova finestra nascosta o thread dedicato. Snapshot con numero di finestre possedute visibili e stato log; testo log con wrapping e caratteri piu grandi. Copia conserva il report integrale.

## Verifiche

- 13 suite, 822 asserzioni portabili su Linux con Roslyn SDK 8.0.426 e runtime 8.0.32. Inclusi lookup esatti/case insensitive, omonimi e percorsi respinti come nomi, titoli ricercabili, PID/argomenti non ampliati, sorgenti Microsoft, servizi mancanti e limiti di ruolo/avvio.
- 11 regressioni deliberate rifiutate dai test runtime: le dieci R30 e un lookup che ammette un nome contraffatto. Non viene sostituita l'esecuzione C# con un controllo testuale.
- 38 preflight PowerShell, 17 fixture launcher, 14 controlli risorse con mutazioni.
- 9 assembly WindowsDesktop verificati come C# con stub dei campi XAML fuori dallo ZIP. Non e MSBuild/XAML/BAML o esecuzione WPF.
- La fixture Windows della MainWindow include ora campo Cerca/Find, ricerca del titolo di una scheda senza processi estranei, stato nessun risultato, log aperto e riduzione/ripristino, menu destro su finestra iconica e voce Riduci disabilitata. E inclusa nella build, NON ESEGUITA su Linux. I callback sintetici non equivalgono al clic fisico di Explorer.
- Rendering, DPI, comportamento Shell/elevazione e RAM reali: NOT RUN / NOT MEASURED. Nessuna promessa di risparmio RAM o completa soluzione sul PC.

## Fonti tecniche primarie consultate

- https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring
- https://learn.microsoft.com/en-us/windows/win32/shell/preview-handlers
- https://support.microsoft.com/en-us/windows/runtime-broker-is-using-too-much-memory-ca6ed4e3-2a36-964c-4d2e-8c93980d8a98
- https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/

Il dizionario descrive architettura/funzione tipica. Una voce omonima non prova firma Microsoft, autenticita, sicurezza, proprietario o lavoro corrente. Nessuna ricerca online automatica o invio di percorsi/processi.
