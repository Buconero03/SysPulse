# ProcessLens R24 — riduzione a icona e RAM live

Base: R23 R1, SHA-256 `0ad04666380c489391c39230aa3998969358989fa69aaa7a2ed604f6be1b7f36`.
La revisione prosegue dalla candidata corretta, senza ripartire da R6. R22 e R23 R1 sono conservate.

## Problema e modifica

| Prima | R24 |
|---|---|
| La X cambiava `WindowState` dentro `Closing`; il ripristino arrivava da callback native. | La X annulla la chiusura e accoda la riduzione dopo il ritorno da `Closing`. Riduci/Mostra sono accodati e l'ultimo comando prevale; l'uscita annulla i comandi pendenti. |
| Solo controlli di sistema e menu Mostra/Esci. | Pulsante **Riduci a icona** in alto, stessa azione nel menu dell'icona. Restano Mostra, clic per riaprire e barra applicazioni come percorso alternativo. |
| CPU aggiornata ogni 2 s; RAM insieme all'inventario ogni 15 s o 45 s sotto carico. | Unico ciclo rapido esistente portato a 1 s nominale: CPU e tre contatori RAM, stesso handle per PID, lavoro sul thread di raccolta. Metadati e classificazione restano nel ciclo lento. |
| Righe e dettagli RAM dipendenti dal successivo inventario. | Aggiornamento delle proprietà delle righe esistenti e dei contatori del PID selezionato, con riordino CPU/Memoria. Nessuna rilettura WMI/SCM o classificazione aggiunta al ciclo rapido. |
| Possibili campioni duplicati nella storia dopo un overlay. | Istanza verificata con PID + creazione, tempo monotono per precedenza e scadenza a 6 s; storico senza duplicati o ordine inverso. |
| Su API vecchie, coda EX2 non compilata poteva sembrare zero privato. | Sentinella della coda EX2 e ripiego EX: Working Set totale/commit disponibili, privato N/D. Test indipendenti del layout SDK e di letture simulate. |

Working Set privato, Working Set totale e commit privato restano contatori distinti. Zero misurato e lettura fallita non sono equivalenti. Il campionamento rapido si sospende a finestra ridotta, scarta risultati pendenti della generazione precedente e riprende alla riapertura. Anche la memoria del dettaglio viene invalidata in caso di sospensione/errore.

Il campionamento nominale a 1 s non garantisce una scadenza rigida sotto carico. Nuovi processi e processi terminati seguono ancora l'inventario 15/45 s. Il confronto con Gestione attività richiede stessa istanza e contatore; gli aggiornamenti non sono sincronizzati. Il tooltip del contatore del dettaglio riporta l'ora del campione.

## Vincoli e costo

Nessuna nuova dipendenza, servizio, driver, installazione, scansione firme/file, modifica Guardian, MemoryAgent o profiler. Nessuna cattura di rete o azione sui processi aggiunta. Nessun nuovo timer: cambia la cadenza del timer CPU esistente. Il costo aggiunto è una lettura PSAPI per PID nel ciclo rapido; viene riutilizzato l'handle CPU. Storico sempre massimo 160 campioni per PID e massimo 5 minuti: alla cadenza di 1 s il limite numerico conserva circa 2 min 40 s. Nessun numero di risparmio RAM o latenza Windows dichiarato senza misura.

Le modifiche native sono limitate a WindowsMemorySampler, WindowsLiveCpuSampler e alla propagazione/verifica del campione in WindowsProcessCollector; sono necessarie per la richiesta RAM live. Le altre acquisizioni Windows restano identiche alla base. Gli attributi temporali in ProcessSnapshot sono aggiunte compatibili, non cambiano i contatori esistenti.

## Verifiche

- Test C# realmente eseguiti su Linux, Roslyn 8.0.426 / runtime 8.0.32: 9 suite, 344 asserzioni. CPU, RAM, istanze riciclate, errori/zero, campioni fuori ordine, storico limitato, EX2/EX, riduzione differita, copia, componenti Windows e nuova attività R23 R1.
- Due mutazioni intenzionali devono fallire nei test runtime: falso zero privato su EX e riduzione sincrona. Log ed hash inclusi.
- 31 fasi PowerShell di preflight + 17 controlli isolati del launcher. Ulteriori 14 mutazioni dei controlli sorgente sulle risorse.
- C# di Windows, Semantics, GUI e 5 suite WPF verificato contro riferimenti WindowsDesktop. Stub di soli campi XAML/InitializeComponent, esclusi dallo ZIP: **non equivale** a compilazione markup/BAML o esecuzione WPF.
- Le nuove suite LiveMetricsTests e LiveMetricsUiTests sono collegate alla build Windows normale prima della pubblicazione. LiveMetricsTests include una prova con il solo processo fixture su Windows; qui quel ramo non è eseguito.
- Differenza dalla base riproducibile con patch, `git apply --check`, applicazione effettiva e confronto dei byte. Manifest SHA-256 e integrità ZIP verificati.

**NON ESEGUITI QUI:** build WPF/MSBuild completa, esecuzione UI Windows, tray/Explorer, resa DPI, confronto dal vivo con Task Manager e misura reale CPU/RAM del prodotto. L'ambiente di preparazione è Linux. La candidata contiene tutti i test necessari alla compilazione Windows, ma il comportamento reale della shell resta da verificare.

## Prova Windows

1. Dall'icona della vecchia istanza scegli Esci da ProcessLens. Estrai tutto lo ZIP in una cartella nuova.
2. Avvia solo AVVIA_PROCESSLENS.cmd: la prima volta esegue i test e la build della candidata, poi apre R24. Gli avvii successivi usano la ricevuta R24. Nessun vecchio eseguibile viene sostituito a una build fallita.
3. Premi Riduci a icona, riapri dalla barra; ripeti usando X, clic sull'icona e menu Mostra ProcessLens. Prova anche da finestra massimizzata: deve riprendere lo stato precedente.
4. In Processi scegli un PID con RAM variabile. Osserva elenco, contatori nell'analisi e tooltip dell'ora. Controlla che l'ordine Memoria segua i cambiamenti conservando il PID selezionato.
5. Confronta la stessa colonna della scheda Dettagli di Gestione attività: Working Set privato con Working Set privato. Non confrontare commit, Working Set totale o gruppi con la colonna privata di un singolo PID.
6. Riduci per almeno 10 s, poi riapri: i contatori devono ripartire; CPU richiede due letture confrontabili. N/D è corretto per processi protetti o non leggibili.
7. Per misurare il costo reale usa il monitor RAM separato già presente, a parità di carico e numero di processi. Nessuna percentuale di miglioramento è stata misurata qui.
