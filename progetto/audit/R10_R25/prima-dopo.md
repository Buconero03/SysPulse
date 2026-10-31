# ProcessLens R25 — gruppi, filtro identità, porte e icona

Base completa R24, SHA-256 `62d14889b771b6921892f049063e90ad9f127637ca9c33eab7e90825650a6aa0`. Continuazione della candidata, senza ripetere la revisione R6–R24. I registri precedenti restano storici; i risultati qui si riferiscono ai sorgenti R25.

## Comportamento

| Problema | R25 |
|---|---|
| Chrome elencato soltanto con schede ripetitive per PID. | Vista predefinita a gruppi espandibili e righe compatte. Checkbox per tornare alla vista per PID. Ogni PID conserva selezione, analisi e menu propri. |
| Cercando Google comparivano altre app. | Filtro su nome effettivamente visualizzato ed eseguibile; query numerica come PID esatto. Esclusi padre, argomenti, owner stimato e descrizioni tecniche. Applicato dopo gli aggiornamenti e le sostituzioni da analisi asincrona. |
| RAM dei gruppi non disponibile. | Somma dei campioni dei PID mostrati, aggiornata nello stesso ciclo rapido R24. Privato e totale separati; copertura per campioni mancanti. |
| Clic icona e riduzione non affidabili secondo la prova utente. | Riduzione differita mantenuta, comando WPF/Win32 sul solo HWND posseduto, verifica IsIconic e ripiego SW_MINIMIZE. Un solo protocollo di callback per l'icona, filtro temporale sulle notifiche duplicate e sul doppio clic. Clic: riduci/riapri; menu Mostra/Riduci/Esci e pulsante Esci in alto. |
| Porte ferme al precedente approfondimento o attribuite per semplice omonimia. | Aggiornamento manuale dedicato; gruppo ricostruito da percorso e catena di avvio con tempi compatibili. PID/creazione/percorso confrontati prima e dopo. Se cambia l'identità del PID selezionato, scartati tutti i socket del vecchio gruppo. |
| Errori native TCP/UDP potevano diventare conteggi zero. | Errori conservati con famiglia/protocollo e codice Win32; quattro tabelle indipendenti, tentativi su variazione della dimensione, validazione di byte/righe e massimo 16 MiB per buffer. Stati di acquisizione parziale e socket N/D espliciti. |
| Intervalli locali e remoti poco leggibili. | Report con PID proprietario, protocollo/stato, indirizzo e porta locale distinti dal remoto. Gli altri stati TCP entrano nell'elenco senza essere contati come LISTEN/ESTABLISHED. |

Il raggruppamento è una regola di presentazione verificabile, non la replica dell'algoritmo interno di Task Manager. Stesso eseguibile con radici indipendenti resta separato. Percorso/creazione non leggibili impediscono il collegamento. Non vengono assegnati automaticamente helper con un altro eseguibile. Il confronto cronologico rende il padre compatibile con lo snapshot, non dimostra da solo la causa dell'avvio o l'autenticità del programma.

La ricerca usa il testo che l'utente vede. I totali del gruppo includono soltanto i PID corrispondenti: una ricerca PID può mostrare `1/N`. Un totale parziale di Working Set privato non incorpora i Working Set totali degli altri PID; la copertura resta visibile. La somma di Working Set totali può contare pagine condivise più volte. I campioni CPU hanno intervalli propri.

## Porte: copertura e costo

Lettura locale delle tabelle Windows, nessun Wireshark/Npcap, pacchetto, scansione, cattura o probe aggiunto. Le quattro tabelle non sono atomiche né sincronizzate con Task Manager. Fino a 512 candidati omonimi; eventuale troncamento o identità non disponibile produce una nota di copertura. I buffer nativi sono rilasciati in `finally`, massimo tre tentativi di riempimento, massimo 100.000 righe valide per tabella e 16 MiB per buffer. I campioni di endpoint sono limitati a 128 per PID durante l'acquisizione; il rapporto visualizza fino a 40 per il PID selezionato e 12 per ciascuno dei primi 32 PID correlati. I contatori LISTEN/ESTABLISHED/UDP si accumulano oltre il limite dei campioni, sulle sole tabelle riuscite. Il rapporto dichiara questi limiti.

L'approfondimento iniziale esisteva già; la nuova rilettura è soltanto manuale. Nessuna scansione delle porte entra nel ciclo rapido. Restano frequenza CPU/RAM nominale 1 s, inventario 15/45 s, pausa in riduzione, storico limitato e gestione identità R24. Non sono state misurate riduzioni di RAM o latenza su Windows.

## Icona: verifica e limiti

La candidata precedente aveva superato controlli di logica e C#, ma la prova Windows dell'utente ha mostrato che ciò non bastava. R25 aggiunge stato HWND verificato e log del clic, oltre al cambio di proprietà WPF. Le eccezioni UIPI sono limitate ai messaggi tray/restore di questa finestra elevata; nessun filtro globale viene modificato. Non si attribuisce senza prova il difetto precedente a UIPI.

Il test `--shell-smoke` opera su una finestra e un'icona fixture possedute: riduzione, ripristino, stato massimizzato, X e uscita. È opzionale, non eseguito automaticamente durante la build e **NOT RUN qui**. Il clic reale sull'icona e il riavvio di Explorer restano da verificare manualmente su Windows. Nessun processo utente viene terminato da questi test.

## Regressioni e controllo del perimetro

Durante l'esecuzione della suite Behavior esistente, i percorsi Windows nei riepiloghi degli script venivano trattati come nomi completi dall'host Linux. `RoleClassifier` ora ricava il basename accettando entrambi i separatori; il testo originale dei parametri resta invariato nei dati tecnici. Le regole di classificazione non cambiano. Otto asserzioni aggiuntive, culture it-IT/en-US e percorsi Windows/POSIX, verificano questa proprietà. Gli altri test originali restano attivi.

Guardian, database, MemoryAgent, profiler, monitor RAM, campionatori CPU/RAM R24, soglie e acquisizioni di rete diverse da WindowsDeepProcessInspector sono identici alla base. Nessuna nuova dipendenza. I campi rete aggiunti a ProcessSnapshot sono compatibili e non modificano i contatori esistenti. La ricevuta del launcher è R25: una pubblicazione vecchia o una build fallita non può sostituire la nuova candidata.

## Risultati effettivi

- **PASS:** 645 asserzioni C# eseguite su Linux, 13 suite, Roslyn SDK 8.0.426 / .NET 8.0.32. Include R24, nuova attività/NUL, componenti Windows, copia, CPU/RAM, presentazione, terminologia, comportamento e 41 controlli nuovi su gruppi/ricerca/tabelle/tray.
- **PASS:** quattro mutazioni runtime indipendenti: ricerca negli argomenti, raggruppamento per solo nome, errore tabella trattato come vuota e notifiche tray duplicate. Ogni difetto introdotto in una copia privata deve fallire sul controllo corrispondente. Copie/binary esclusi dal pacchetto.
- **PASS:** 32 fasi PowerShell sorgente e 17 fixture launcher; 14 mutazioni di controllo sorgente delle risorse.
- **PASS limitato:** C# di Windows, Semantics, GUI e sei suite WPF contro riferimenti WindowsDesktop con stub espliciti di campi XAML/InitializeComponent. **Non è** compilazione markup/BAML, esecuzione WPF o controllo visivo. Stub esclusi dal pacchetto.
- **NOT RUN:** build MSBuild/WPF Windows completa, nuove suite WPF a runtime, shell/tray/Explorer, DPI, fixture Windows con listener loopback posseduto, confronto live con Task Manager e misura reale CPU/RAM del prodotto.
- Le nuove suite sono incluse nella normale build prima della pubblicazione. Il ramo Windows del test porte apre soltanto un proprio listener loopback, senza traffico esterno.
- Patch dalla R24 verificata con `git apply --check`, applicazione e confronto esatto; manifest SHA-256 e integrità ZIP verificati.

## Prova pratica

Esci dall'istanza precedente, estrai tutto in una nuova cartella e avvia soltanto AVVIA_PROCESSLENS.cmd. Prima compilazione completa; avvii successivi diretti. Controlla versione R25 nel rapporto Risorse GUI.

1. Cerca `google`: confronta i nomi visibili durante almeno tre aggiornamenti dell'inventario. Un'altra app con Google soltanto nel padre/argomenti non deve comparire.
2. Espandi Chrome, seleziona un PID e confronta il suo Working Set privato con la stessa colonna e istanza di Task Manager. Controlla totale del gruppo e copertura. Prova ricerca PID e ritorno alla vista non raggruppata.
3. Riduci con pulsante, X, icona e barra applicazioni; ripristina anche dalla finestra massimizzata. Esci deve chiudere l'istanza. Se fallisce ancora, `startup.log` ora distingue clic ricevuto, comando e stato HWND effettivo.
4. Dopo l'approfondimento premi Aggiorna porte: verifica ora, protocollo, PID proprietario, porta locale/remota e limiti. Un renderer senza socket non eredita i socket del servizio di rete come se fossero diretti.
5. Da `progetto`, dopo la build, prova opzionalmente:
   `dotnet run --project tests/ProcessLens.ProcessGroupsUiTests -c Release -- --shell-smoke`
   La fixture non sostituisce la prova manuale dei callback di Explorer.

L'anteprima grafica mostrata in chat è una proposta, con numeri e porte illustrativi dichiarati: non è una cattura dell'app R25 in esecuzione né evidenza runtime.
