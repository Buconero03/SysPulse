# ProcessLens v0.19.16 R10 — testi copiabili e revisione delle modifiche

Questa candidata parte da `ProcessLens-v0.19.16-R7(1).zip`, caricato dall’utente il 17 ottobre 2026 (ora italiana). Conserva le nuove funzioni di quel ramo e integra le correzioni R8/R9 già consegnate. Non riapre la revisione terminologica storica. Non è una release stabile.

## Uso della copia

- Nei pannelli di analisi, dettagli del processo, cronologia servizi e stato delle dashboard: trascinare sul testo, poi **Ctrl+C**. **Ctrl+A** seleziona l’intero blocco che ha il focus.
- Tasto destro sul testo: **Copia selezione**, **Copia tutto**, **Seleziona tutto**. I dati rimangono in sola lettura.
- Nel risultato di un’analisi: **Copia rapporto completo** copia titolo, risposta integrale, base/limiti, evidenze già mostrate e registro verifiche quando presente. Include anche le sezioni RAM chiuse; non copia soltanto la prima sintesi.
- Nelle tabelle Guardian e RAM: tasto destro per **Copia cella**, **Copia righe selezionate** o **Copia tabella visualizzata**. Le righe sono quelle del campione filtrato attuale, non tutto il database. Ctrl+C mantiene il comportamento nativo della tabella e include le intestazioni.

Le liste principali Processi e Servizi conservano selezione della riga e menu delle azioni. Per selezionare una parte del nome o della descrizione, selezionare la riga e usare il corrispondente testo nel pannello dettagli. I campi di ricerca e domanda rimangono editabili. La copia non aggiunge metadati nascosti o nuove righe di comando: usa il rapporto corrente e le colonne visibili.

Sono stati convertiti 148 campi XAML e tutti i cinque punti che generano a runtime fatti tecnici, evidenze e sezioni RAM. I nuovi controlli usano TextBox WPF in sola lettura, senza storico Undo. I binding dei campi convertiti sono OneWay; non scrivono nei modelli. I menu sono costruiti in C#, evitando il problema BAML già documentato per menu con eventi dentro Style.Setter.

La copia della tabella conserva formattazione e ordine delle colonne, esclude quelle nascoste e quota correttamente tabulazioni, a capo e virgolette. Il menu fissa il testo all’apertura per evitare che un aggiornamento lo riferisca a un’altra riga. Un blocco temporaneo degli appunti viene segnalato senza chiudere l’applicazione.

Il rapporto da copiare viene azzerato durante nuove richieste, profilazioni e pulizia del pannello; il pulsante rimane disabilitato finché la risposta non è disponibile. Non viene avviata alcuna profilazione per copiare il testo.

## Continuità R8/R9

Il nuovo ZIP era un ramo R7 con 15 file modificati rispetto al precedente ZIP dell’utente. Mancavano ottimizzazioni e formattazione delle candidate R8/R9. È stato effettuato un confronto a tre versioni: base R7 precedente ricostruita mediante le patch già consegnate, candidata R9, nuovo ZIP. Non vi sono stati conflitti testuali. È stato corretto un conflitto di comportamento: l’evento `InspectProcessRequested` della nuova dashboard RAM viene collegato dopo la sua creazione differita, così non dereferenzia un controllo ancora nullo.

Restano le cache limitate, lo storico RAM compatto, i dettagli generati su richiesta, la virtualizzazione delle liste, la durata CPU leggibile e le unità/righe di rete corrette nella R9. I nuovi controlli selezionabili hanno un costo UI diverso da un TextBlock: **il consumo RAM non è stato misurato** e non viene promesso un risparmio ulteriore.

## Valutazione delle modifiche dell’utente

La direzione è utile: il collegamento dashboard → PID rende più rapida la diagnosi; la vista per nome e i filtri permettono un confronto più agevole; i picchi e i nomi dei file mappati aggiungono dati osservabili. Le nuove classificazioni rendono più chiari Roslyn/MSBuild e i componenti ProcessLens.

Ci sono però problemi reali nella contabilità e nell’interpretazione. La candidata mantiene le nuove acquisizioni e i contratti dell’utente; **questi problemi non sono stati corretti riscrivendo collector o modelli durante l’intervento di copia**. I dettagli con file/riga sono in `audit/R10/review-findings.csv`.

| Priorità | Problema osservato nei sorgenti | Conseguenza |
| --- | --- | --- |
| Alta | Il gruppo usa `Any(PrivateWorkingSetAvailable)` per considerare disponibile la somma privata. | Se solo uno dei PID è leggibile, una somma parziale viene presentata come RAM privata dell’intero gruppo. Anche la differenza Working Set − privato diventa inaffidabile. |
| Alta | La RAM in uso meno la somma privata leggibile è etichettata Kernel/driver/DLL. | Il residuo include anche copertura mancante e altre componenti: non è una misura diretta di quelle categorie. |
| Alta | Protezioni RW/RX/READONLY/PAGE_GUARD sono tradotte in heap, buffer attivi, JIT, costanti o stack. | Le API misurano regioni e permessi, non il contenuto o lo scopo delle allocazioni. La nuova presentazione attribuisce più di quanto osservato. |
| Alta | Raggruppamento per solo nome eseguibile o app attribuita; nella sintesi è chiamato consumo esatto dell’applicazione. | Programmi o sessioni distinti possono condividere un nome; non sono verificate tutte le appartenenze e possono mancare processi. |
| Media | `Take(maxRows)` precede il filtro per categoria nella dashboard. | Un filtro mostra soltanto gli elementi della categoria già presenti nella classifica globale; i totali delle schede riguardano un insieme più ampio. |
| Media | Una diminuzione del Working Set viene attribuita a rilascio/GC; il consiglio di approfondire richiede crescita e RAM oltre l’85%. | I contatori non provano la causa della diminuzione; non bisogna attendere entrambe le condizioni per indagare un problema. |
| Media | Le nuove regole classificano dal nome o da sottostringhe dei parametri. | Sono ipotesi di funzione, non verifica dell’identità del file o dell’attività corrente. Alcune spiegazioni dell’avvio sono tornate troppo assertive. |
| Media | Il conteggio delle letture fallite è chiamato numero di processi protetti di sistema. | Le eccezioni gestite includono anche processi terminati o non leggibili per altri motivi. |

Un ulteriore punto di qualifica è la compatibilità EX2 e i suoi fallback sulle versioni Windows supportate. Gli script originali di build/avvio erano anche stati modificati per forzare la chiusura di ogni processo chiamato ProcessLens. Questa parte è stata **corretta**: nessuna chiusura automatica; la pubblicazione si interrompe prima di sostituire l’output se l’utente deve chiudere un’istanza attiva. Il normale avvio conserva il meccanismo single-instance dell’applicazione.

Il rapporto è un esame dei sorgenti e del diff, non una certificazione delle nuove misure su Windows. Le nuove funzioni non erano accompagnate da modifiche ai test nel ramo ricevuto. La presenza di binari e cartelle obj nello ZIP non dimostra che i sorgenti attuali abbiano superato tutte le suite.

## Prove tecniche di riferimento

- [TextBox WPF in sola lettura](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-make-a-textbox-control-read-only): selezione/copia con dati che l’utente non può modificare.
- [VirtualQueryEx](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtualqueryex): attributi e dimensioni delle regioni; queste informazioni non identificano gli oggetti interni.
- [PROCESS_MEMORY_COUNTERS_EX2](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2): significato di Working Set, picchi, commit e PrivateWorkingSetSize; requisiti di versione.
- [GetProcessMemoryInfo](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo): permessi richiesti e successo/fallimento della lettura.
- [DataGridColumn.OnCopyingCellClipboardContent](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.datagridcolumn.oncopyingcellclipboardcontent): uso del contenuto previsto dal binding clipboard delle colonne.

## Verifica e limiti

Controlli strutturali eseguiti in Linux: `audit/R10/structural-results.json`. Diff completo rispetto allo ZIP nuovo, confronto con R9, matrice file/riga, replay della patch e manifest sono inclusi in `audit/R10` e `SHA256SUMS_R10.txt`. I manifesti e audit R7/R8/R9 sono storici, non attestano i sorgenti R10.

**NON ESEGUITO:** compilazione .NET, suite C#, controlli PowerShell, test WPF e prova visuale a più DPI. In questo ambiente mancano .NET/PowerShell e Windows. Il parsing XML non sostituisce compilazione XAML/BAML o test dei comandi. **NON MISURATO:** RAM reale e costo dei nuovi controlli. Nessuna installazione, acquisizione profiler, terminazione o modifica ai servizi è stata eseguita qui.

Le nuove suite sono:

- `ProcessLens.CopyTests`: risposta completa, paragrafi senza titolo, Unicode, registro, culture e TSV con caratteri speciali.
- `ProcessLens.CopyUiTests`: routing dei comandi, selezione Unicode, lettura sola, copia delle colonne visibili, snapshot immutabile e caricamento del vero XAML. Il test inietta una destinazione fittizia: non tocca gli appunti dell’utente. Crea i pannelli senza mostrarli né attivare refresh, SQLite o profiler.
- `check-copy-ui-r10.ps1`: integrazione markup/codice, binding e assenza delle chiusure forzate.

Le suite R8/R9 sono conservate e quelle nuove sono collegate a `build-dashboard.ps1`.

## Verifica Windows riproducibile

Chiudere manualmente l’istanza precedente, estrarre in una cartella nuova e lavorare nella radice con `src` e `tests`:

```powershell
.\build-dashboard.ps1
```

Se termina con successo:

```powershell
.\run-release.ps1
```

La verifica estesa senza avvio del monitor, installazione o cattura profiler è `verify-candidate-r10.ps1`. Esegue tutti i progetti di test; la fixture del profiler viene soltanto compilata. La nuova suite WPF costruisce le viste senza mostrarle. Le criticità della dashboard elencate sopra restano aperte anche se le suite passano.

Prova manuale da registrare:

1. Analizzare un PID e una domanda RAM. Selezionare una parte del nome, un nome API e più righe del registro; provare Ctrl+C e i menu di copia. Incollare in Blocco note e verificare cifre, accenti e a capo.
2. Chiudere alcune sezioni del rapporto RAM e premere Copia rapporto completo: verificare che siano incluse. Cambiare PID o iniziare una nuova analisi: il vecchio rapporto non deve essere copiabile durante l’attesa.
3. In Servizi, selezionare e copiare titolo e dettagli di una voce di cronologia. In Guardian, copiare una cella e le righe selezionate; con un filtro attivo Copia tabella deve riguardare il solo campione mostrato.
4. Aprire un menu di copia tabella, attendere un aggiornamento e copiare: il testo deve riferirsi al campione fissato all’apertura. Le azioni su processi e servizi devono mantenere i loro precedenti target; non eseguire terminazioni per questa prova.
5. Ripetere a 100/125/150/200% DPI e con finestra 1180×800 e 950×680 DIP dove possibile. Verificare wrapping, selezione, focus, scorrimento e pulsanti. Le configurazioni non provate restano NON ESEGUITO.
6. Passare fra Processi, Servizi, Guardian e RAM. Provare il nuovo passaggio dashboard → PID e l’apertura/chiusura dei dettagli differiti. Non avviare la profilazione solo per verificare la copia.

Tutti i materiali restano in una sola radice con sottocartelle per sorgenti, audit, test e release. Sono esclusi dal pacchetto i binari e gli obj ricevuti; l’originale ZIP rimane intatto.
