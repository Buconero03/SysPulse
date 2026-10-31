# R10 revisione 10 — spazio di analisi, diagnostica Guardian e componenti Windows

Si continua dalla candidata completa R10 R9, senza ripetere la revisione già conclusa. La schermata del 20 ottobre mostra Guardian, con le schede compresse e contatori grezzi nella diagnostica. La versione esatta della GUI nella schermata non è accertata. La richiesta sui software di sistema è interpretata come approfondimento dei componenti Windows, all'interno di ProcessLens.

## Prima e dopo

| Area | Prima | Candidata corrente |
|---|---|---|
| Dettaglio processo | Colonna più stretta dell'elenco, dimensioni bloccate | Colonna più ampia; divisore trascinabile o utilizzabile con la tastiera |
| Analisi lunga | Poco spazio orizzontale | «Espandi dettagli» usa la larghezza disponibile; «Mostra elenco» ripristina la precedente larghezza dell'elenco |
| Domande e rapporti | Dopo diversi blocchi di identificazione | Analisi su richiesta vicina all'inizio del pannello |
| Intestazione | Titolo e contatori GUI sempre estesi | Margini e titolo compatti; risorse GUI in una sezione richiudibile, inizialmente chiusa |
| Servizi | Divisione elenco/dettagli fissa | Divisore trascinabile |
| Schede Guardian | Intestazioni native piccole e ravvicinate | Schede con padding, stato selezionato e indicazione del focus tastiera |
| «Salute ETW / SQLite» | Nome interpretabile come giudizio generale | «Diagnostica ETW / SQLite»: riguarda raccolta e archivio |
| `health` | `accepted=… saved=… working_set_mib=…` | Riepilogo italiano e dettaglio selezionabile per campo; messaggio originale in una sezione distinta |
| Limiti Guardian | Stato apparentemente generale | Contatori cumulativi della singola esecuzione, RAM residente totale del solo servizio e limiti della copertura chiariti |
| Assenza di ETW | «nessun evento ETW nello storico» | «nessun evento ETW nella finestra selezionata» |
| Perdite SQLite | «batch persi segnalati» | «segnalazioni di perdita SQLite», coerente con il conteggio delle voci diagnostiche |
| Informazioni Windows | Descrizione generica del classificatore | Scheda di contesto separata: ruolo tipico, riscontri del PID, approfondimenti, limiti e riferimento Microsoft |

Nella diagnostica il divisore orizzontale regola lo spazio tra tabella e dettaglio. Il messaggio originale mantiene caratteri, interruzioni di riga e formattazione numerica della sorgente. Copiare la tabella copia il riepilogo visualizzato; per il record grezzo usare «Messaggio originale». Non vengono riscritti record SQLite. Se le voci non cambiano, la GUI evita di ricreare l'elenco diagnostico e la relativa selezione.

## Approfondimento dei componenti Windows

La scheda «Componente Windows · ruolo e riscontri» appare per cinque nomi documentati: svchost, services, lsass, winlogon e dwm. È disponibile anche per snapshot con servizi associati o flag critico; per nomi non coperti dichiara che manca un ruolo architetturale specifico. La scheda mostra una sintesi del ruolo; «Apri rapporto Windows completo» e il menu Analisi approfondita richiamano il rapporto in capitoli, copiabile integralmente.

Il ruolo è una descrizione del componente omonimo: il solo nome non autentica l'istanza. I metadati azienda/prodotto/descrizione sono dichiarazioni del file, separati dai nomi dei servizi già raccolti. Non vengono esposte nuove righe di comando. Il prospetto non verifica firme o reputazione, non ricava operazioni in corso dal ruolo e non formula un giudizio di sicurezza. La voce «Origine e sicurezza» diventa «Origine e impatto della chiusura», coerente con le azioni del menu.

Riferimenti Microsoft Learn consultati il 20 ottobre 2026, usati solo come documentazione architetturale:

- Service host grouping: https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring
- Service Control Manager: https://learn.microsoft.com/en-us/windows/win32/services/service-control-manager
- Credentials processes / LSA / Winlogon: https://learn.microsoft.com/en-us/windows-server/security/windows-authentication/credentials-processes-in-windows-authentication
- Desktop Window Manager: https://learn.microsoft.com/en-us/windows/win32/dwm/dwm-overview

L'interpretazione di `accepted`, `saved` e `working_set_mib` deriva invece dal codice consegnato di GuardianWorker: incrementi sull'accodamento e sulla scrittura del batch; WorkingSet64 del servizio. È una proiezione di presentazione; il servizio non viene modificato. Il formato non riconosciuto, incompleto, futuro o di altra origine viene conservato senza aggiungere campi. I decimali non vengono riparsati usando la cultura della GUI.

## Controlli e limiti

Eseguiti qui: controlli Python/XML della candidata corrente, regressioni di sorgente CPU/ripristino e monitor/navigazione, confronto esatto dei componenti esclusi con R9, applicazione effettiva della patch R9→corrente, confronto byte per byte e verifica del manifest/ZIP. I risultati storici sotto audit/R10 e audit/R10_R1…R9 restano storici: non equivalgono a una nuova esecuzione.

Aggiunte regressioni C# per culture it-IT/en-US, contatori Guardian, record grezzi/malformati, nomi Windows esatti/omonimi, metadati assenti e servizi parziali. La suite WPF verifica il caricamento del markup, il pulsante Espandi/Mostra elenco e i binding del dettaglio Guardian senza avviare raccolte o usare gli appunti reali. La nuova verifica PowerShell è collegata alla build.

**SDK .NET, PowerShell, compilazione, test C#/WPF, rendering e verifica visuale DPI NON ESEGUITI in questo ambiente. Consumo RAM/CPU della candidata NON MISURATO.** Nessun miglioramento prestazionale quantitativo o corretto layout Windows viene dichiarato come dimostrato dai controlli di sorgente.

Per qualificare su Windows, dalla cartella estratta:

```powershell
.\build-dashboard.ps1
```

Poi verificare:

1. A 1180×800 e 950×680 DIP, ridimensionare elenco/dettagli; espandere l'analisi e tornare all'elenco senza perdere il rapporto. Provare anche i servizi.
2. Ripetere con scala 100%, 125%, 150%, 200%, entro le dimensioni disponibili del desktop; controllare tasti, testi e barre di scorrimento. Il limite minimo storico della finestra può richiedere una superficie di lavoro maggiore sui display ad alta scala: questa candidata non qualifica automaticamente quei casi.
3. Aprire Guardian: navigazione con Tab e frecce, schede selezionate riconoscibili, selezione di una voce health, lettura del dettaglio, copia dei decimali e del messaggio originale.
4. Attendere due aggiornamenti Guardian: con diagnostica invariata, selezione e testo selezionato devono restare disponibili; con dati nuovi, la stessa voce deve essere riselezionata se ancora presente.
5. Aprire i componenti Windows supportati e un processo sconosciuto; verificare riscontri, limiti, servizi e copia completa senza attribuire autenticità dal nome.
6. Riprovare «Copia risorse GUI» aprendo la relativa sezione: la correzione CS7036 della R9 resta inclusa.

## Priorità successive proposte, non implementate qui

1. Qualificare prima build, ripristino e layout reali: sono i punti che hanno già interrotto il flusso di uso.
2. Estendere le schede documentate ad altri componenti Windows, mantenendo ruoli e riscontri distinti e regressioni sui nomi omonimi.
3. Aggiungere su richiesta verifiche di firma digitale e identità del file, con esiti «verificata», «non valida», «non eseguita» e «non disponibile»; evitare un punteggio di sicurezza generico.
4. Progettare una scheda del sistema operativo (build, architettura, durata dall'avvio e capacità di acquisizione) con fonti e disponibilità esplicite. È un approfondimento ulteriore rispetto alle schede dei processi di questa revisione.
5. Misurare la suite con il monitor RAM già incluso, su una sequenza ripetibile, prima di altre ottimizzazioni; aggiungere poi I/O e tempo di aggiornamento dove misurabili, senza equipararli automaticamente ai contatori di Gestione attività.

## Esclusioni e consegna

Invariati byte per byte rispetto a R9: Guardian service/ETW/SQLite reader, MemoryAgent, profiler, acquisizione Windows, Core, Semantics, contratti e knowledge ID; monitor RAM, contatori CPU e politica di ripristino precedenti mantenuti. Non sono stati avviati arresti di processi/servizi, installazioni o profilazioni.

La consegna è uno ZIP sorgente completo. Registro prima/dopo e stringhe: audit/R10_R10. Patch esatta dalla R9 e manifest SHA256SUMS_R10.txt inclusi. Una sola cartella sorgente attiva; baseline consegnata conservata, cartelle di confronto temporanee rimosse dopo verifica.
