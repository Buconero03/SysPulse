# ProcessLens v0.19.16 R8 — candidata con ottimizzazioni memoria

## Risultato e stato esatto

La candidata applica ottimizzazioni al sorgente fornito dall’utente nello ZIP `ProcessLens-v0.19.16-R7.zip`. Il processo da 116,9 MB nella schermata è ProcessLens, come confermato dall’utente. Quella cifra è un’osservazione puntuale di Gestione attività: non è una baseline temporale e non permette di quantificare il risparmio della R8.

**RAM R8 misurata su Windows: NON ESEGUITO. Risparmio in MB: NON DETERMINATO. Build, test .NET/PowerShell e visuale WPF: NON ESEGUITI qui.** Ambiente Linux senza dotnet/pwsh. Nessun profiler, installazione o intervento su processi reali eseguito.

Sono forniti sorgenti modificati, test eseguibili, controlli strutturali, patch esatta e procedura di confronto passivo. Non si dichiara una release stabile né una riduzione di RAM già ottenuta sul PC.

## Base e modifiche dell’utente

Lo ZIP ricevuto contiene differenze in sette file rispetto alla precedente R7 R2: MainWindow.xaml/.cs, ProcessRow.cs, ProcessTerminology.cs, ProcessBehaviorExplainer.cs, ProcessQueryEngine.cs e TerminologyTests/Program.cs. Questa consegna usa quei file come base. Cinque restano byte per byte invariati; in MainWindow vengono modificati caricamento e generazione dei controlli, conservando testi, dimensioni e stile della base ricevuta. Non viene ripristinata la precedente revisione linguistica sopra le modifiche dell’utente.

L’archivio originale è conservato. Dal nuovo pacchetto sono esclusi soltanto `bin`, `obj`, `publish` e risultati locali di build: eliminare copie su disco non riduce la RAM di un processo in esecuzione. La lista completa è nell’audit; nessun originale dell’utente è stato cancellato.

## Interventi applicati

| Punto | Prima | R8 | Conseguenza verificabile nel codice |
|---|---|---|---|
| Dashboard Guardian e Memoria | Due UserControl istanziati durante InitializeComponent, anche mai visitati | Host vuoti; creazione al primo ingresso; una sola istanza riutilizzata | Zero istanze di queste due viste all’avvio; prima apertura può richiedere più tempo. I servizi indipendenti non cambiano. |
| Righe classificazione | Clear e nuova costruzione ad ogni ShowDetails, anche a pannello chiuso | Generazione su apertura; riuso con stesso PID/insight; rilascio su chiusura | Evita i controlli generati durante polling nascosto. Nessun dato di classificazione eliminato. |
| Dettagli tecnici | Stringhe/evidenze composte anche con sezione chiusa | Composte con l’ultimo snapshot su apertura/aggiornamento della sezione aperta; controlli e testo derivati rilasciati su chiusura | L’audit originale si ricompone su richiesta, senza cambiare i dati strutturati. |
| Cache icone | Dizionario senza limite | LRU massimo 128 voci | I percorsi non possono trattenere icone indefinitamente; un miss rilegge Shell. Stessa risoluzione, Freeze e DestroyIcon conservati. |
| Cache deep inspection | Una voce per ogni PID esaminato ancora vivo | Massimo 8 | Ritorno a un PID evinto può richiedere una nuova lettura. Il collector non cambia. |
| Cache cronologia servizi | Una voce per ogni servizio visitato; TTL di 2 minuti per il riuso, non eliminazione | Massimo 16 voci, TTL originale conservato | Numero di report trattenuti limitato; un miss legge nuovamente la cronologia. |
| Storico RAM | Nuovo record MemoryObservation per ogni campione/PID globale | Campioni inline in coda di value tuple; record materializzati solo per il trend richiesto | Meno oggetti distinti trattenuti. Stessa finestra di 5 minuti, massimo 160, stessi timestamp/byte e N/D. |
| Liste processi/servizi | Virtualizzazione predefinita senza riciclo esplicito | Virtualizzazione e Recycling espliciti | I contenitori visivi possono essere riutilizzati; i dati dei processi non sono nascosti/rimossi. |
| Identità pubblicata | Impostazioni apphost implicite | UseAppHost=true, AssemblyTitle e Description ProcessLens | Il flusso pubblicato avvia ProcessLens.exe. Non si rinomina dotnet.exe e non si promette che il solo nome riduca memoria. |

Questi interventi riducono oggetti creati/trattenuti o impongono limiti. **Non dimostrano da soli un valore inferiore del Private Working Set:** runtime, GC, WPF, cache native, pagine condivisibili, carico e sequenza di utilizzo influiscono sul contatore finale. Dopo aver visitato le dashboard, le relative istanze restano conservate per mantenere stato e filtri; il beneficio iniziale non si applica integralmente a quel momento.

Non vengono forzati GC o svuotamenti del working set; nessuna funzionalità o acquisizione Windows è stata rimossa, nessuna soglia Guardian/MemoryAgent alterata. Core, Semantics, collector Windows, Guardian, MemoryAgent, profiler, reader e viste dashboard sono invariati rispetto all’allegato. Due controlli statici di navigazione sono aggiornati per i riferimenti ora null-safe, non il codice Guardian.

## Test e limiti

`ProcessLens.MemoryEfficiencyTests` verifica cache LRU sotto churn/concorrenza, confronto case-insensitive, aggiornamenti/rimozioni, retention a 160 campioni/5 minuti, limite temporale esatto, indipendenza dell’esportazione e parità del trend. Il confronto delle allocazioni gestite usa un carico sintetico di 1.000 PID con 20 campioni; il relativo vincolo sulle allocazioni è per x64, piattaforma pubblicata dal progetto. Quel confronto non misura la GUI WPF, gli oggetti nativi o RAM fisica.

La suite nuova è collegata a `build-dashboard.ps1`; il verificatore R8 scopre tutti i progetti di test e tutti i controlli PowerShell. I test sintattici esistenti, la suite semantica dell’allegato e i controlli su navigazione/consenso restano necessari.

I controlli Python eseguiti sono in `audit/R8/structural-results.json`. Coprono byte dei componenti esclusi, XML, nomi/handler, creazione differita, limiti cache, riciclo, conservazione dei sorgenti personalizzati, assenza di GC/trim introdotti e integrazione test. Non sono un compilatore C# o un renderer WPF. La patch è applicata a una copia della base ricevuta e i byte risultanti sono confrontati tramite SHA-256.

## Compilazione Windows

Estrarre il pacchetto in una sola radice. Sorgenti, audit, test, pubblicazione e misure restano sotto quella radice.

```powershell
dotnet run --project .\tests\ProcessLens.MemoryEfficiencyTests\ProcessLens.MemoryEfficiencyTests.csproj -c Release
.\verify-candidate-r8.ps1
```

Con suite positiva, compilare/pubblicare e avviare l’eseguibile:

```powershell
.\build-dashboard.ps1
.\run-release.ps1
```

Il manifest UAC del progetto è conservato. La GUI precedente va chiusa normalmente prima di avviare la nuova, altrimenti il controllo di singola istanza può riattivare quella già aperta. Non installare/riavviare Guardian o agenti per verificare questa candidata.

## Confronto RAM prima/dopo

Lo script `measure-processlens-memory.ps1` legge passivamente i contatori di un PID scelto. Non induce GC o profilazioni, non termina processi e non avvia la GUI. Il Private Working Set proviene da GetProcessMemoryInfo/EX2; se la lettura fallisce il valore resta vuoto con errore registrato. Working Set e commit hanno colonne distinte. La raccolta si interrompe se il processo termina o cambia istanza.

1. Tenere la versione R7 fornita come riferimento ed eseguirla nelle condizioni abituali. Usare l’eseguibile pubblicato anche per R7, per confrontare la stessa modalità di esecuzione. Attendere 60 secondi prima di misurare.
2. Dalla radice R8, leggere il PID della R7 e raccogliere 3 minuti senza cambiare schermata:

```powershell
.\measure-processlens-memory.ps1 -TargetPid 1234 -Label R7 -Scenario processes-idle
```

3. Chiudere la GUI R7 normalmente; avviare la R8, attendere 60 secondi e ripetere sostituendo il PID:

```powershell
.\measure-processlens-memory.ps1 -TargetPid 5678 -Label R8 -Scenario processes-idle
```

4. Ripetere a parità di sequenza dopo avere aperto Guardian e Memoria (`-Scenario dashboards-visited`), dopo molte selezioni di processi/servizi e dopo chiusura dei dettagli. Evitare compilazioni e carichi diversi durante il confronto. Ripetere almeno due sessioni se i risultati oscillano.

**1234 e 5678 sono esempi, non PID rilevati.** Output in `verification-r8/`. Confrontare mediana e picco dello stesso contatore su intervalli equivalenti. Non sommare privato/totale/commit; non confrontare direttamente la sola cifra 116,9 MB dello screenshot con colonne dal significato diverso.

| Scenario | R7 PWS mediana/picco | R8 PWS mediana/picco | Esito |
|---|---|---|---|
| Processi, nessuna dashboard visitata | NON MISURATO | NON MISURATO | PENDENTE |
| Guardian e Memoria visitate | NON MISURATO | NON MISURATO | PENDENTE |
| Churn selezioni/processi/servizi | NON MISURATO | NON MISURATO | PENDENTE |

Per la verifica visuale controllare primo accesso e ritorno alle dashboard, filtri persistenti, grafici, apertura/chiusura dettagli, cambio PID a pannello aperto, ridimensionamento e DPI 100/125/150%. Controllare menu contestuali dopo lo scroll con riciclo, annullando eventuali dialoghi di terminazione/arresto. Nessuna operazione sui processi è necessaria per il test dei menu.

## Riferimenti

- Microsoft, [WPF Controls performance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls): virtualizzazione e riciclo dei contenitori; la virtualizzazione UI non è rimozione dei dati.
- Microsoft, [WPF Application Resources](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-application-resources): costo degli oggetti e risorse ripetuti.
- Microsoft, [Publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/): apphost ed esecuzione tramite il proprio eseguibile.

Le dimensioni del risparmio sono da misurare sul progetto, non dedotte da queste pagine.
