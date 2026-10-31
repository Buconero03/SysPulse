# ProcessLens — valutazione tecnica e priorità al 17 ottobre 2026

La direzione che consiglio è consolidare l'affidabilità delle conclusioni, rendere uniforme la lettura e qualificare il comportamento Windows. Il progetto dispone già di enumerazione processi, contatori CPU/RAM, analisi dei ruoli, rete, viste servizi/Guardian, mappa virtuale, campionamento delle pagine e profiler separato su richiesta. La quantità di funzionalità rende particolarmente utile una base di dati e di verifica coerente fra tutte queste viste.

Questa valutazione riprende le criticità già registrate in `audit/R10/review-findings.csv`, le confronta con i punti del codice coinvolti e aggiunge l'osservazione della schermata del registro. Non è una nuova revisione integrale o una misura delle prestazioni. Le candidate qui preparate non sono state eseguite su Windows: build, test .NET/WPF, tray e DPI restano da qualificare. I log Windows dell'utente già archiviati sono evidenze delle revisioni cui si riferiscono, non attestazioni della candidata attuale.

## Priorità e criteri di chiusura

| Priorità | Intervento | Perché serve | Criterio verificabile |
|---|---|---|---|
| P0 | Qualificazione Windows della candidata corrente | Hotfix di sorgente, selezione, lifecycle WPF e Shell non sono equivalenti a comportamento verificato | Build e suite complete, manifest della build verificata, prove riproducibili di copia, pannelli stretti, tray, riapertura e uscita |
| P0 | Correzione della contabilità RAM e dichiarazione della copertura | Alcuni totali e residui sono presentati con significato più forte delle misure disponibili | Fixture con PID leggibili/non leggibili, copertura esplicita, nessuna somma parziale etichettata completa |
| P0 | Isolamento delle risposte asincrone per istanza e interrogazione | I passaggi audit aggiornano la GUI dopo await e il controllo della selezione non copre tutti i punti di pubblicazione | Test con ritardi controllati e cambio PID: nessun audit o risultato dell'analisi A nel pannello del processo B |
| P1 | Separazione sistematica fra osservazione e attribuzione | Protezione di una regione, nome eseguibile e andamento WS non dimostrano da soli scopo interno, causa di avvio o GC | Ogni conclusione indica fonte e limite; test indipendenti da etichette e regole che producono la stessa conclusione |
| P1 | Modello di presentazione tipizzato e riduzione di MainWindow | La finestra concentra oltre 2.200 righe e combina acquisizione, orchestrazione e rendering; alcuni log devono essere interpretati come stringhe | Presenter dedicati con test puri; eventi e misure passati come dati, senza ricostruire la semantica dal testo |
| P1 | Misure di costo e durata reali | L'aggiunta di cache e controlli differiti non quantifica da sola il risparmio o il costo | Confronto riproducibile fra candidate con privato residente, working set, commit, allocazioni GC, handle e durate |
| P2 | Vocabolario e gerarchia visuale condivisi | Le correzioni locali hanno lasciato differenze fra rapporto, evidenze e registro | Una stessa struttura sintesi → misure → evidenze → registro, stili condivisi e prove visive a DPI diversi |

## 1. Le criticità RAM più importanti

In `WindowsPhysicalMemoryReader.cs` il gruppo usa `items.Any(x => x.PrivateWorkingSetAvailable)` insieme alla somma dei soli membri disponibili. Un gruppo con un PID letto e uno non letto può quindi essere presentato come disponibile senza esplicitare il contributo mancante. Il prossimo intervento dovrebbe distinguere misurati, attesi e non disponibili e qualificare il totale come parziale quando occorre. Questo riguarda R10-01 e il raggruppamento delle applicazioni R10-05.

Nello stesso file la differenza `usedPhysical - totalPrivWs` alimenta `KernelAndSharedPhysicalBytes`. Dalla formula applicata nel codice non segue una misura diretta della memoria kernel e condivisa: il residuo risente anche di copertura, contatori diversi e campionamenti non simultanei. Lo presenterei come residuo non attribuito con il relativo limite, affiancando eventuali contatori specifici raccolti e verificati separatamente. È la criticità R10-02, ancora aperta.

In `WindowsAllocationInspector.cs` le etichette includono Codice compilato JIT / Runtime a partire da RX/RWX. Le protezioni sono osservabili, lo scopo interno non viene stabilito da quelle sole protezioni. VirtualQueryEx descrive stato, protezione e tipo di regioni virtuali; non è un profiler degli oggetti o delle cause di allocazione. Le etichette dovrebbero quindi riportare le protezioni effettive e distinguere qualsiasi ipotesi di scopo. Questo riguarda R10-03/R10-04. Non lo considero risolto dalla sola nuova formattazione del registro.

Anche il calo del working set non dimostra di per sé rilascio delle allocazioni o garbage collection; la pressione di sistema e la crescita di un singolo processo devono essere valutate separatamente. Per la top list va inoltre chiarito il limite applicato prima dei filtri. Questi casi hanno già riferimenti R10-06/R10-07 e fixture richieste nel registro delle criticità.

I significati dei contatori restano distinti: PrivateWorkingSetSize è working set privato; PrivateUsage è commit privato; VirtualQueryEx riguarda regioni virtuali. Non proporrei somme o confronti fra questi valori senza dichiararne il significato e la copertura.

Fonti primarie di riferimento:

- https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2
- https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtualqueryex
- https://learn.microsoft.com/en-us/windows/win32/memory/working-set

## 2. Separare i dati dalla loro formulazione

Il progetto contiene già flag di disponibilità nei ProcessSnapshot e DiagnosticEvent con OperationId, PID, API, Stage e altri metadati. Riutilizzerei e renderei coerenti queste informazioni nella GUI. Non occorre creare un secondo sistema diagnostico parallelo.

Per ogni misura la presentazione dovrebbe avere valore/unità, API, istante o intervallo, identità dell'istanza, disponibilità e motivo del limite. Per ogni valutazione dovrebbe essere esplicito se deriva da una misura o da una regola. Il testo italiano rimarrebbe una vista di questi dati. In questo modo cambiare una parola o un separatore nel log non cambierebbe il comportamento del programma.

La R6 risolve la leggibilità con un adattatore per i formati già esistenti, mantenendo il generatore byte per byte invariato. È una soluzione circoscritta e compatibile con i vincoli della revisione. Il passaggio successivo a eventi tipizzati richiede un intervento dedicato, preservando contratti, identificativi e file JSON e verificando le equivalenze con il testo originale.

Un altro punto da qualificare riguarda il routing asincrono: in MainWindow il callback locale AuditStep pubblica sul pannello senza verificare la selezione corrente. Esiste un controllo `_selectedPid != row.Pid` prima di un successivo Task.Run sul motore query, ma la pubblicazione ShowQueryResult dopo quell'await non ripete il controllo. Dal codice emerge quindi un rischio concreto di risposta o audit tardivo sotto una selezione diversa; non è stato riprodotto qui con una GUI Windows. Userei una chiave di pubblicazione composta da id interrogazione e identità PID/timestamp, verificata al momento di ogni aggiornamento della GUI. La cancellazione può ridurre lavoro inutile, ma non sostituisce il controllo prima della pubblicazione. Questo non viene corretto implicitamente da un renderer che separa soltanto le righe del log.

Sposterei progressivamente i presenter del registro, del rapporto memoria, dell'identificazione e dello stato finestra fuori da MainWindow, seguendo la separazione già avviata con ReportLayout/ReportView e i nuovi componenti del registro. L'orchestrazione resterebbe distinta dai collector. Eviterei una riscrittura simultanea dei motori e dell'interfaccia: piccoli passaggi con equivalenze verificabili rendono più facile individuare una regressione.

## 3. Qualificazione e prestazioni

La verifica più utile adesso è una procedura Windows ripetibile sulla stessa candidata: suite .NET, controllo XAML, build di tutti i componenti pertinenti e smoke test WPF. Le prove dovrebbero includere dati mancanti, processi usciti, riuso PID, rete parzialmente accessibile e fallback dei contatori. La GUI richiede anche selezione/copia, ridimensionamento, ripristino e uscita. I test di sorgente rimangono utili come guardie, ma non coprono questi comportamenti.

Per le prestazioni riutilizzerei la strumentazione già presente e il percorso `measure-processlens-memory.ps1`, verificandone gli scenari supportati prima di estenderlo. Confronterei avvio a freddo, finestra visibile, processo selezionato con analisi, schede RAM/Guardian e finestra nascosta. Annoterei numero di processi e durata del campionamento. Le durate dovrebbero essere riportate come distribuzioni o almeno mediana e percentile alto su più campioni, non da una singola osservazione.

La misura deve distinguere RAM privata residente, working set totale e commit. Un calo apparente ottenuto spostando pagine fuori dal working set non dimostra la rimozione di allocazioni né un beneficio complessivo. Non sono disponibili qui numeri che certifichino risparmi in MiB delle revisioni dalla R8 alla R10 R6.

## 4. Sequenza pratica consigliata

Prima qualificare su Windows la candidata corrente e correggere eventuali regressioni effettive. Poi intervenire in modo dedicato su R10-01/R10-02, con test di copertura e contabilità, e su R10-03/R10-04 per eliminare attribuzioni non dimostrate. Quindi uniformare i modelli di misura/presentazione e consolidare stili e presenter. Ogni passaggio dovrebbe terminare con una sola candidata completa, manifest coerente e distinzione esplicita fra eseguito e non eseguito.

In questo intervento sono state implementate soltanto la nuova presentazione del registro e le relative verifiche. I cambiamenti ai collector, alla semantica e ai componenti protetti descritti come priorità non sono stati introdotti. Non sono stati terminati o profilati processi e non sono stati modificati servizi.
