# ProcessLens v0.19.16 R7 — candidata per verifica

**Base:** v0.19.16 R6 candidata. **Data:** 14 ottobre 2026.
**Stato:** revisione di sorgente e controlli strutturali eseguiti; build, suite .NET/PowerShell, scenari Windows e verifica visuale WPF **NON ESEGUITI**. Nessuna promozione stabile.

## Continuità e ambito

Sono stati riutilizzati l’inventario R6 di 3.406 voci, le decisioni R5/R6, il glossario di 95 voci e l’handoff. Le 34 correzioni R6 non sono state riapplicate alla baseline R5. Gli archivi consegnati restano originali; la candidata è una copia della R6.

La verifica ha seguito le descrizioni lungo i rami del classificatore, le precedenze della knowledge base, i formatter Core, le risposte alle query, i binding della GUI, i servizi, le misure CPU/memoria, l’origine, la rete, le descrizioni GPU, gli avvisi e la diagnostica. Sono inclusi i percorsi senza alert lessicale. Test, codici, SQL e stringhe interne sono distinti dal linguaggio normale dell’applicazione. La tabella di copertura comprende anche script CMD, controlli PowerShell e file non presenti nell’inventario iniziale.

La matrice completa conserva **3.406 riferimenti R6**, con testo originario, contesto/riga R7, prova, decisione, rischio e test. È una matrice di continuità, non un nuovo conteggio di messaggi R7: gli estratti ereditati possono spezzare stringhe interpolate o ripetere una posizione. La colonna `contesto_R7` contiene la riga completa del sorgente, non un nodo AST. Le nuove stringhe e i nuovi test sono tracciati anche nel diff completo. Non si dichiara di aver validato 3.406 messaggi mediante esecuzione né di aver provato tutti i percorsi Windows.

## Correzioni principali

| Prima in R6 | Dopo in R7 | Prova e motivo |
|---|---|---|
| «Una build o IDE .NET lo ha avviato…» | «VBCSCompiler.dll è indicato nei parametri… richiesta originaria e riuso corrente non osservati» | `ClassifyDotNet` riconosce un nome DLL, non il richiedente dell’avvio. |
| «Avviato da [padre] per eseguire…» | Modulo/script indicato e padre riportato; motivo non osservato | PPID/nome non identificano autore e intenzione; compatibilità temporale non è prova del creatore. |
| «Esegue script/assembly…» nella sintesi | «Script/assembly/file indicato…» | Parametri di avvio distinti dall’istruzione corrente. |
| Updater: «Controlla o applica aggiornamenti…» | «Possibile componente di aggiornamento software» | Nome o metadati non dimostrano aggiornamento in corso. |
| Crash handler: «Rimane pronto…» | Nome/metadati compatibili, stato interno non osservato | La regola non misura readiness o un crash. |
| «Coordina l’applicazione…» da finestra + figli | Finestra e figli omonimi, possibile processo principale | Non vengono osservate attività di coordinamento; confronto dei nomi diverso dall’identità dei file. |
| «Rete delegata rilevata» / «più attivo in rete» | Ruolo suggerito e PID con più connessioni rilevate | Tabelle socket non provano delega né quantità di traffico. |
| «Questo PID non possiede socket proprie» | Nessuna ESTABLISHED/UDP rilevata; LISTEN separato | Il ramo poteva essere raggiunto anche con una socket LISTEN presente. |
| CPU non disponibile mostrata come zero in alcuni dettagli/health | N/D nei dettagli; dati insufficienti nella risposta sul blocco | Flag di disponibilità già esistenti rispettati. Nessuna nuova acquisizione. |
| «Progresso misurato» da titolo/command line | «Possibile indicatore testuale» con fonte e limiti | Un parametro può essere iniziale o un obiettivo; il titolo può riferirsi a una fase. |
| ETA «ALTA/MEDIA» e «intervallo prudente» | Stima condizionata a due campioni testuali; scenario aritmetico −25%/+35%, non confidenza | Le soglie precedenti non calibravano attendibilità e intervallo. La formula è conservata. |
| «Tendenza stabile» / «sta diminuendo» dal saldo | Saldo quasi invariato/in diminuzione; possibili oscillazioni intermedie | Delta iniziale/finale non prova monotonia o stabilità. |
| «ProcessLens non modifica i servizi» | Arresto SCM su richiesta e conferma, senza terminare forzatamente il PID | GUI già dotata di `RequestStopServiceAsync`; testo precedente obsoleto. |
| «Oltre il 75/90%», ramo ≥75/90 | «Almeno il 75/90%» | Etichetta coerente anche all’uguaglianza. Policy 85/80 invariata. |
| Working Set confrontabile direttamente con Task Manager / Processi | Stessa istanza, contatore e intervallo; possibili colonne/aggregazioni diverse | Il contatore rimane esattamente Private Working Set; nessuna equivalenza universale promessa. |
| «Nativo / non determinato» | «Runtime non determinato» | Mancato riconoscimento non prova codice nativo. |

Il registro `audit/R7/CHANGELOG_DETTAGLIATO.csv` descrive **235 modifiche guardate**, che non sono 227 difetti indipendenti: una correzione può interessare più frasi o occorrenze. Il diff completo comprende i sorgenti, i test, il verificatore Windows e la documentazione della revisione; gli audit sono aggiuntivi.

## Le 41 priorità R6

Tutte le 41 voci hanno un riferimento stabile e una decisione in `audit/R7/DECISIONI_41_PRIORITA_R6.csv`:

- 20 voci ricadono nelle correzioni R7;
- 18 sono conservate con limite esplicito: punteggi tecnici non probabilistici, domande legittime, intent o limiti negativi;
- 1 voce Guardian è conservata come componente escluso: il limite sull’ordine nello stesso secondo è corretto;
- 2 voci sono asserzioni di test conservate, non messaggi normali dell’applicazione.

Queste categorie si riferiscono alle **voci dello screening**, non al numero di bug. La revisione ha individuato problemi anche fuori dalle 41 voci. I punteggi 0–100 e gli identificativi delle regole rimangono nel pannello tecnico; il rapporto normale non trasforma il punteggio in probabilità o forza astratta degli indizi.

## Confini preservati

Confronto byte per byte dei componenti protetti in `audit/R7/static-results.json`: Guardian, MemoryAgent, collector Windows, profiler, reader/view Guardian, modelli/policy protetti e diagnostica strutturata invariati. Contatori, campi dei modelli pubblici, ID `knowledge:`, ID delle regole, punteggi, categorie, condizioni di matching, SQL e codici diagnostici non modificati. Nel JSON cambiano esclusivamente descrizioni/etichette ed evidenze editoriali.

Le variazioni di controllo sono circoscritte alla presentazione: guardia sui dati CPU mancanti, selezione della dicitura N/D e qualificazione di progresso/ETA. Il prefisso della sintesi Python e il formatter che ne ricava il nome sono aggiornati insieme. I contatori non vengono ricostruiti o stimati.

Nessuna profilazione, installazione, terminazione, avvio di servizio o acquisizione su processi reali eseguita. Il verificatore Windows compila i componenti e la fixture ma non avvia la fixture, non lancia la GUI e non installa servizi/agenti/tool.

## Verifiche e limiti

**20 controlli Python strutturali: PASS.** Sono verificati XML ben formato, nomi XAML univoci, invarianti dei modelli e dei predicati JSON, byte dei componenti esclusi, collegamenti/diciture mirati, versioni e assenza di azioni operative nel verificatore. Questi controlli non sono una compilazione C# né un parser completo PowerShell/XAML.

Aggiornati i test dipendenti dalle stringhe in TerminologyTests, BehaviorTests e MemoryTests; aggiunte regressioni anche in ServiceTests. Coprono i nuovi limiti su avvio, nomi updater/crashpad, coordinating inference, disponibilità CPU, rete parziale/LISTEN, progresso testuale, ETA condizionato, oscillazioni di memoria, soglie e capacità SCM. Il loro esito è **NON ESEGUITO**. La procedura esegue anche TerminologyTests senza la knowledge copiata per esercitare i fallback C#.

**Build .NET, tutti i test .NET, PowerShell, WPF, AVVIA-TUTTO e layout a DPI 100/125/150%: NON ESEGUITI.** Sistema Linux, `dotnet` e `pwsh` assenti. Non sono stati installati prerequisiti.

Le etichette di sezione possono andare a capo; la terna memoria usa altezze minime comuni lasciando la crescita naturale; il titolo della guida RAM è abbreviato. Questa è prevenzione strutturale, **non una verifica visuale**. Rimangono da verificare dimensioni minime, menu, scrollbar, testi lunghi, DPI e schermi dove la dimensione minima della finestra supera l’area utile.

Vedere `VALIDAZIONE_WINDOWS_R7.md`, `audit/R7/test-matrix.csv` e `PROBLEMI_APERTI_R7.md`. R7 rimane candidata finché build, suite e verifiche visuali non sono completate.

## Fonti e prove

La prima prova è il sorgente della R6 e il predicato effettivamente usato da ciascun ramo. Documentazione primaria consultata per confermare i confini, senza attribuire a Microsoft le proposte editoriali del glossario:

- [PROCESS_MEMORY_COUNTERS_EX2](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2): PrivateWorkingSetSize, WorkingSetSize e PrivateUsage in byte; commit diverso dalla residenza.
- [Working Set](https://learn.microsoft.com/en-us/windows/win32/memory/working-set): pagine residenti e memoria condivisibile.
- [MIB_TCPROW_OWNER_PID](https://learn.microsoft.com/en-us/windows/win32/api/tcpmib/ns-tcpmib-mib_tcprow_owner_pid): stato, indirizzi, porte e PID; nessun contatore di byte in questa struttura.
- [UpdateProcThreadAttribute](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute): un processo diverso dal chiamante può essere specificato come padre.
- [MEMORYSTATUSEX](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex): memoria fisica totale/disponibile.

La conclusione applicativa «questo dato non prova l’operazione interna» deriva dal confronto tra queste capacità e i campi realmente letti da ProcessLens; è un’analisi del progetto, non una frase certificata dalla documentazione.
