# R23 — Esegui nuova attività

Richiesta confermata dall'utente: aggiungere Esegui nuova attività, per avviare programmi da ProcessLens. Non è stata richiesta una scorciatoia globale né una sostituzione di Gestione attività.

| Prima | R23 |
|---|---|
| Nessun comando di avvio programmi | Pulsante Esegui nuova attività nell'intestazione |
| Avvio da strumenti esterni | Finestra su richiesta con programma, argomenti e cartella iniziale |
| Nessun esito dell'avvio | Conferma con PID oppure errore correggibile nella finestra |
| Candidata R22 | R23 conserva catalogo di 29 componenti e tutta la copertura R22 |

## Comportamento

Il pulsante è disponibile da tutte le sezioni. La finestra modale viene creata soltanto al clic. Sfoglia sceglie un eseguibile EXE/COM; i nomi brevi come notepad.exe sono accettati. Gli argomenti e la cartella iniziale sono facoltativi. Programma e argomenti sono campi distinti, con esempi e descrizioni accessibili. Gli argomenti non vengono suddivisi, ricomposti o espansi da ProcessLens. Variabili d'ambiente sono espanse una sola volta nel programma e nella cartella, mai negli argomenti. Se la cartella è vuota, il figlio eredita la cartella corrente. Per script e comandi di shell l'utente sceglie esplicitamente il relativo interprete.

Process.Start usa UseShellExecute=false, senza shell intermedia, redirect, attesa della fine del figlio o richiesta aggiuntiva di elevazione. Il processo eredita il contesto di ProcessLens: l'app è normalmente amministratore, come dichiarato nella finestra. Non è implementata una modalità di avvio con privilegi ridotti. Il PID conferma la creazione, non l'inizializzazione corretta o la salute futura del programma.

Esegui resta disabilitato durante la richiesta; i doppi clic non duplicano l'avvio. Annulla prima dell'avvio non esegue nulla. Durante la breve richiesta di creazione la chiusura della finestra è sospesa: non si presenta Annulla come una revoca di un processo già in creazione. Gli errori mantengono aperti i campi. Dopo successo la finestra si chiude e compare un messaggio passivo con il PID. I campi vengono liberati alla chiusura. Nessuna cronologia su disco o log degli argomenti viene aggiunta; il normale inventario Windows continua a poter mostrare la riga di comando del processo secondo le sue regole preesistenti.

## Ambito e risorse

Nuovi file: modello di richiesta/risultato, launcher esplicito, finestra e due suite. Non modificati acquisizione Windows esistente, CPU/RAM, rete, Guardian, MemoryAgent, RamMonitor, profiler o knowledge ID. Nessuna nuova dipendenza, processo persistente, timer, driver o servizio. Il wrapper .NET Process rilascia l'handle senza terminare il figlio. Non è stata misurata qui la RAM della GUI su Windows.

## Verifiche eseguite

- 31 asserzioni portabili della nuova funzione: campi distinti, percorsi con spazi, Unicode, virgolette, metacaratteri, lunghezze limite, input invalidi, privilegi non inventati, esiti senza segreti e PID non fabbricati.
- Creazione reale di un solo processo fixture posseduto: riceve gli argomenti esatti, attende un segnale in una directory temporanea e resta vivo dopo Dispose dell'handle del genitore; nessun programma utente o di sistema avviato. Nessuna rete.
- 103 asserzioni del catalogo Windows R22 e 22 del parser di sistema, contro il Core corrente: PASS.
- 30 controlli PowerShell preliminari: PASS su PowerShell 7.4.21/Linux.
- 17 fixture del launcher di pacchetto: PASS, inclusi fallimenti prima della ricevuta, mancato avvio della vecchia GUI, cache R20 e avvio successivo senza ricompilazione.
- Type checking C# di Windows, Semantics, GUI e quattro suite UI contro reference WindowsDesktop: PASS. Campi XAML generati e InitializeComponent sostituiti da stub espliciti: non è compilazione markup o esecuzione WPF.
- Entrambe le nuove suite sono aggiunte alla pipeline normale build-dashboard.ps1.

## NON ESEGUITO qui

Build MSBuild Windows, caricamento BAML/XAML, esecuzione delle suite WPF, interazione con Sfoglia, tasti Invio/Esc, privilegi/token Windows reali, prova con programmi utente, resa 100/125/150/200% DPI e misura RAM reale. Gli esiti di compilazione C# non qualificano questi controlli. Le evidenze storiche nelle cartelle precedenti restano riferite alle loro revisioni.

## Verifica Windows

1. Dall'icona della vecchia GUI scegliere Esci da ProcessLens: la X la riduce soltanto. Estrarre tutto lo ZIP in una nuova cartella e aprire AVVIA_PROCESSLENS.cmd.
2. Il primo avvio verifica e compila R23 usando lo SDK già presente, senza installare SDK o driver. Se R20 è installata riusa runtime e cache NuGet locali; nessuna verifica vulnerabilità online è dichiarata per questa modalità. Gli avvii seguenti aprono direttamente R23.
3. Da Processi e poi da un'altra sezione aprire Esegui nuova attività. Premere Annulla o Esc: nessun programma deve avviarsi.
4. Se approvato dall'utente, provare notepad.exe senza argomenti. Deve comparire il programma e un messaggio con PID. Il PID non deve venire sostituito con zero in caso di errore.
5. Provare un percorso inesistente: correggere il campo e riprovare senza chiudere la finestra. Verificare Sfoglia e un proprio programma in un percorso con spazi.
6. Verificare campi leggibili, tabulazione, Invio, Esc, ridimensionamento e DPI indicati. Gli argomenti devono restare in campi separati e gli errori non devono ristamparli.
7. Chiudere e riavviare R23: il launcher non deve ripetere la compilazione. Per ricompilare dopo modifiche, eliminare solo supporto/build-r23.ok.

## Fonti

- https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-diagnostics-processstartinfo-useshellexecute
- https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessa
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/how-to-close-window-dialog-box
