# ProcessLens v0.19.16 R7 R2 — origine multiprocessing

Base: R7 R1 candidata. La revisione terminologica precedente e la correzione sulle porte sono conservate. R2 resta candidata.

Il nuovo log Windows fornito dall’utente conferma il completamento di BehaviorTests, comprese tutte le regressioni R7 e R1 su porta/porte e falsi positivi. TerminologyTests ha poi superato le verifiche precedenti e i primi casi R7, ma si è arrestato sull’asserzione del PID sintetico 704: «R7 launch limitation explicit». Le fasi successive sono NON ESEGUITE. Questi esiti si riferiscono a R1, non a R2.

La frase del ramo multiprocessing con ParentName presente diceva «la catena di avvio richiede verifica temporale». Non attribuiva al padre un’attività di avvio certa. Il test generico cercava la sola parola «non», quindi ha prodotto un falso negativo: il messaggio esprimeva il limite con parole diverse.

La frase R2 è: «Padre riportato: [nome]; il nome non verifica la catena di avvio né identifica autore o motivo dell’avvio.» Comunica precisamente il limite di ParentName nel classificatore; non nega un’eventuale verifica separata nel pannello Origine.

Il caso 704 ora verifica espressamente il limite sulla catena e sul richiedente. Sono aggiunti casi per --multiprocessing-fork, multiprocessing.spawn e multiprocessing.forkserver, con e senza ParentName, conservazione delle evidenze e invariabilità della spiegazione al variare di CPU/RAM. Il controllo negativo sulla causalità rimane. Non sono cambiati predicati di classificazione, punteggi, contratti, ID knowledge, collector, Guardian, MemoryAgent, profiler o diagnostica strutturata.

I controlli strutturali e la riproduzione della patch sono eseguiti localmente; non equivalgono a test .NET. Qui Linux non dispone di dotnet/pwsh. Build e test .NET/PowerShell/WPF R2: NON ESEGUITO.

Su Windows, estrarre nuovamente il pacchetto aggiornato e dalla radice che contiene src e tests eseguire:

```powershell
dotnet run --project .\tests\ProcessLens.TerminologyTests\ProcessLens.TerminologyTests.csproj -c Release
```

Con exit code 0, riprendere `./AVVIA-TUTTO.cmd` oppure eseguire `./verify-candidate-r7.ps1` per la suite completa senza avvio GUI/installazioni. Non è necessario modificare Guardian. Visuale WPF/DPI e fasi non completate restano pendenti.

Gli audit R7/R7_R1 sono storici. Il delta R2 è sotto audit/R7_R2; il manifest corrente SHA256SUMS_R7.txt copre tutta la candidata aggiornata. Una sola directory radice e un solo ZIP di consegna.
