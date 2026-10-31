# ProcessLens R14 — Sistema Windows

La R13 è mantenuta, compresa l'integrazione Wireshark/Npcap. La R14 aggiunge **Sistema Windows** nella barra principale. Compilare in PowerShell con `.\build-dashboard.ps1`.

## Cosa trovi

Apri **Sistema Windows → Aggiorna elenco**. La vista è creata solo quando viene aperta; non avvia un nuovo servizio, un processo ausiliario o un timer. Gli aggiornamenti sono manuali.

- **Windows e voci interne**: PID riservati, eseguibili con percorso nella cartella Windows e nomi tipici di componenti Windows. Il criterio di raggruppamento è esposto per ogni riga. Non verifica firma digitale o autenticità.
- **Tutti i processi rilevati**: inventario senza il filtro di categoria della vista Processi, inclusi nomi sconosciuti e dettagli non accessibili.
- **Origine non verificabile**: righe senza percorso verificabile, esclusi i PID riservati 0 e 4.

Ogni riga conserva nome, PID, PID padre riportato, thread, handle, sessione, Working Set totale e stato di accesso ai dettagli. Il PID padre è un dato del campione: non prova che l'istanza del padre sia ancora viva. Un diniego o un processo terminato durante la lettura non elimina la riga enumerata.

Il campione ha un orario e non si aggiorna da solo. Ricerca numerica per PID esatto; ricerca testuale per nome. Tasto destro e Ctrl+C permettono di copiare la tabella; **Copia rapporto** include fonte, errori, memoria globale e dettagli selezionati.

## Voci interne e memoria

Le voci **System**, **System Idle Process**, **Registry**, **Secure System** e **Memory Compression** vengono mostrate se restituite da Windows. Non sono aggiunte artificialmente. La voce Idle con PID 0 rappresenta inattività della CPU; non è un'app che consuma la percentuale di CPU mostrata come inattiva da altri strumenti.

Una voce interna può non avere percorso, avvio o contatori accessibili. Il solo nome di Memory Compression non misura la dimensione dei dati prima della compressione. La sua assenza dal campione non dimostra che la compressione sia disattivata. Non è stato aggiunto un contatore della dimensione dello store compresso.

L'espansione **Memoria del sistema e limiti della lettura** mostra RAM fisica totale/disponibile, commit corrente/limite, pool kernel paginabile e non paginabile. I valori provengono da GetPerformanceInfo: pagine moltiplicate per la dimensione della pagina. Il pool paginabile non è tutto residente. Non sommare questi valori ai Working Set dei processi; neppure la somma dei Working Set è un totale fisico senza duplicazioni, perché comprende pagine condivise.

Driver, interrupt, DPC e cache non diventano per questo processi separati. Non è un enumeratore dei moduli kernel, delle routine interne o dei servizi di ogni sottosistema.

## Accesso e analisi

L'inventario usa `NtQuerySystemInformation(SystemProcessInformation)` e legge soltanto i membri pubblicati della struttura. Nessun significato viene ricavato dai campi riservati. Se la chiamata non è disponibile o restituisce un formato non valido, viene tentata l'enumerazione Toolhelp; la fonte alternativa e l'errore originale restano visibili. Le liste possono differire: non si certifica la completezza degli oggetti interni del kernel.

Percorso e avvio richiedono soltanto `PROCESS_QUERY_LIMITED_INFORMATION`. Nessuna modifica di privilegi o tentativo di aggirare processi protetti. L'avvio deve precedere il rilevamento e il nome deve corrispondere al percorso letto dallo stesso handle; in caso contrario quei dettagli restano N/D.

**Apri analisi del processo** richiede un avvio noto e confronta PID, nome e avvio con la vista Processi. Se l'istanza non è verificabile, i dettagli della riga rimangono consultabili in Sistema Windows. Non vengono avviati automaticamente profiler, terminazioni o interventi sulla memoria. Questa vista non campiona una nuova CPU istantanea: usa l'analisi ordinaria per la CPU.

## Consumo e limiti

Buffer nativo temporaneo massimo 16 MiB, fino a 8 tentativi e 16.384 righe; non sono limiti alla RAM totale del programma. Il buffer viene sempre rilasciato. La vista rilascia righe e report e scarta i risultati tardivi quando viene chiusa o la finestra viene ridotta. La riapertura richiede un nuovo aggiornamento. Restano i monitor già presenti nel programma, secondo le loro politiche.

L'aumento reale di CPU/RAM **non è stato misurato**. Confrontare R13/R14 a riposo, apertura, lettura e chiusura della sezione usando gli stessi contatori e almeno tre ripetizioni. Non si dichiarano risparmi o compatibilità verificata sul PC dell'utente.

## Verifica Windows

Eseguire `.\build-dashboard.ps1`: include le due nuove suite `ProcessLens.SystemProcessTests` e `ProcessLens.SystemProcessUiTests`. La prima usa fixture binarie indipendenti dagli offset del parser e, su Windows, un confronto in sola lettura con i dati del proprio processo. La seconda usa una sorgente simulata e verifica markup WPF, filtri, copia, selezione e risultati tardivi; non esegue acquisizioni reali.

Verificare manualmente elenco senza filtro e con filtro Windows, `csrss.exe`/processi protetti con dettagli N/D, eventuale Memory Compression, errori, ricerca PID, copia, passaggio fra Processi/Rete/Sistema, riduzione e riapertura. Confrontare PID e nomi con Gestione attività **Dettagli**, considerando che i due campioni non sono simultanei. Provare 950×680 e 1180×800 DIP a 100%, 125%, 150%, 200% DPI. Le intestazioni hanno scorrimento indipendente e la tabella è virtualizzata.

In questo ambiente: **.NET/C#, PowerShell, WPF, letture Windows reali e controlli DPI NON ESEGUITI**. I risultati effettivamente eseguiti sono registrati in `audit/R10_R14`; i risultati precedenti rimangono storici, non sono dichiarati nuovamente eseguiti.

## Riferimenti primari

- Microsoft: [NtQuerySystemInformation e layout pubblicato](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntquerysysteminformation). L'API può cambiare; fonte alternativa esplicita. SystemProcessInformation è usato qui per i contatori, non soltanto per i nomi.
- Microsoft: [Enumerazione dei processi](https://learn.microsoft.com/en-us/windows/win32/procthread/process-enumeration).
- Microsoft: [Toolhelp e limiti di accesso a Idle/CSRSS](https://learn.microsoft.com/en-us/windows/win32/toolhelp/taking-a-snapshot-and-viewing-processes).
- Microsoft: [GetPerformanceInfo](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getperformanceinfo) e [unità dei contatori](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information).
