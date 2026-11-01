# Revisione R35 — analisi in italiano oppure in inglese

Base: archivio `ProcessLens-Leggero.zip` allegato, revisione R34 con le modifiche di localizzazione dell’utente. Questa candidata continua da quel codice. Non riparte dalla R6.

## Problema trovato

La base conteneva un dizionario parziale, sostituzioni di sottostringhe e un secondo rapporto inglese aggiunto sempre sotto quello italiano. Alcune spiegazioni inglesi seguivano inoltre regole diverse da quelle italiane: per esempio deducevano attesa o lavoro interno dalla sola CPU. I pannelli dinamici potevano riutilizzare il primo testo memorizzato quando cambiava la lingua.

## Modifica

- Scelta Italiano / English prima della finestra principale. Gli avvii diretti IT ed EN restano disponibili; il pulsante della lingua resta nell’app.
- Una sola analisi nella lingua scelta. Rimosso il secondo rapporto inglese automatico; conservato il testo italiano delle spiegazioni.
- Dizionario JSON incorporato: **5882 chiavi esatte**, incluse frasi, frammenti, testi inglesi già presenti e identificatori tecnici mantenuti invariati. Il censimento corrente contiene **8676 occorrenze**, **5756 testi distinti** e **459 chiavi XAML**. Il numero di chiavi non è un conteggio di frasi revisionate manualmente.
- Localizzazione di descrizioni, classificazione, diagnosi CPU/RAM, rete, origine, avanzamento/ETA, effetti della chiusura, servizi, dettagli e messaggi dell’interfaccia. Il monitor RAM e il profiler ereditano la preferenza salvata quando vengono avviati. Anche l’agente degli avvisi RAM legge la preferenza all’avvio.
- Nomi, metadati dei file, indirizzi, comandi, percorsi, valori e contatori restano dati originali. Un nome italiano può quindi apparire dentro un rapporto inglese. I campi modificabili non vengono tradotti.
- Le frasi composte conservano il modello e gli argomenti separatamente. Nessuna ricerca/sostituzione di parole dentro percorsi o altri dati del processo. Le associazioni di presentazione usano riferimenti deboli, non uno storico permanente di stringhe per PID.
- Esportazioni diagnostiche JSON: tradotti i valori descrittivi riconosciuti; conservati nomi delle proprietà, dati originali e numeri. CSV, protocolli, identificatori e vecchi log non vengono riscritti.
- Correzioni editoriali mirate a committed memory, working set, LISTEN, contatori, campioni, limiti e negazioni. La traduzione non deve aumentare la certezza rispetto al testo italiano.

La localizzazione WPF usa controlli derivati che mantengono i template nativi. Non modifica i metadati già registrati dei tipi WPF originali. Il cambio lingua invalida la risposta già composta: la domanda può essere ripetuta nella lingua scelta.

## Architettura e manutenzione

Sono presenti nove progetti di produzione. La trasformazione di presentazione interessa **135 sorgenti** in App, Core, Semantics, Windows, RamMonitor, MemoryAgent e Profiler; produce inoltre otto viste XAML. I servizi Guardian conservano gli eventi originali e i loro protocolli di raccolta. La localizzazione delle spiegazioni dei servizi avviene nella GUI e nel Core.

I sorgenti canonici italiani restano accanto a `Localized`. Il generatore Roslyn aggiunge alle copie compilate le informazioni necessarie a tradurre modelli e argomenti senza cambiare i valori italiani impiegati dalle regole. I progetti compilano queste copie, non due versioni della stessa classe. La logica di lettura, CPU, RAM, identità dei PID, finestre, icone e cattura non viene sostituita da un motore inglese separato.

Dopo un cambiamento dei sorgenti o del dizionario eseguire `tools/refresh-localization.ps1`. Il manifesto registra gli hash dei sorgenti e delle copie generate; una modifica non rigenerata blocca la build. Il test di localizzazione richiede tutte le chiavi del censimento e risolve le chiavi XAML. Nessun modello di traduzione o servizio online è necessario nell’applicazione.

## Verifica della candidata

Gli esiti finali e i log di questa revisione sono nella presente cartella. Le cartelle di audit delle revisioni precedenti restano materiale storico, non prova della R35.

- Suite portabili: consultare `portable-results.json`. Esecuzione di codice C# compilato con riferimenti .NET 8; dati sintetici e processi fixture posseduti, secondo ciascun test. I rami riservati a Windows possono risultare SKIP nei singoli log.
- Test di localizzazione: copertura esatta del dizionario, chiavi XAML, ritorno byte per byte all’italiano, nomi che coincidono con una voce del dizionario, composizione dei rapporti, risposte complete per sei tipi di processo e nove domande, domande equivalenti in inglese ed esportazione JSON. I casi verificano anche il mantenimento di PID, porte e memoria misurata.
- Controlli PowerShell: vedere `static-results.json`; includono sintassi, controlli storici della UI e nuovo controllo R35. Fixture dell’avvio: 17 controlli con file sintetici, senza UAC o avvio dell’applicazione.
- C# WPF: controllo contro riferimenti WindowsDesktop e campi XAML sostitutivi esplicitamente dichiarati. Compilati App, Windows, Semantics e sette suite UI. Controllati inoltre RamMonitor, MemoryAgent e Profiler.
- **Non eseguiti qui:** compilazione reale BAML/WPF, esecuzione delle finestre su Windows, DPI, menu nell’area di notifica, collaudo sul PC dell’utente, installazione Guardian, cattura reale di rete e misura del consumo RAM della nuova candidata. I controlli C# con campi sostitutivi non equivalgono a queste verifiche.

Il normale `build-dashboard.ps1` include i nuovi test C# e WPF, oltre ai controlli esistenti. Su Windows questi devono passare prima della pubblicazione. La candidata non contiene una pubblicazione Windows già validata.

## Osservazioni sul progetto

La separazione fra Core, lettori Windows, semantica e interfaccia è utile: consente di mantenere invariati contatori e dati originali mentre cambia la presentazione. La copertura dei test sintetici su identità dei PID, indisponibilità dei dati e distinzione fra working set e commit è ampia.

Restano importanti due limiti della documentazione precedente: “sola lettura” descrive la raccolta, non tutte le azioni dell’app, perché esistono comandi espliciti di terminazione/arresto; le affermazioni su costo nullo o precisione assoluta non sono dimostrate dai test. Non ho introdotto stime del consumo RAM della localizzazione. Un controllo automatico di copertura linguistica non certifica da solo la correttezza editoriale di ogni frase né sostituisce il collaudo visivo su Windows.

## Pacchetto

Pacchetto sorgente leggero: esclusi `.git`, `bin`, `obj`, `publish`, ricevute di build e l’archivio sorgente annidato da circa 125 MiB. Conservati sorgenti, risorse, test, script e documentazione. Non sono inclusi SDK, runtime, driver, librerie Python o modelli usati durante la preparazione.

Avvio: estrarre in una cartella nuova e aprire **AVVIA_PROCESSLENS.cmd**, quindi scegliere la lingua. Il primo avvio compila con lo SDK .NET 8 disponibile sul PC. Se esiste già un’istanza, usare il suo pulsante Lingua oppure uscire prima.
