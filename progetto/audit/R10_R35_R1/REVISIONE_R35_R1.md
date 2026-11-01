# R35 R1 — continuazione della revisione della lingua

Base: candidata R35 consegnata, SHA-256 `c9c3c0bbcb93846fee1c971e53b0433f554a2a5c5c370405c1a7582807698e2f`.

## Correzioni

Il dizionario R35 conteneva ancora traduzioni automatiche errate: testi fuori contesto in etichette brevi e descrizioni di componenti, oltre a termini tecnici tradotti come parole comuni. La presenza di una voce nel dizionario non provava che la sua traduzione fosse corretta.

Questa revisione corregge **134 valori inglesi**, senza eliminare alcuna chiave italiana. Esempi: porta → port, coda → queue, arresto del servizio → service stop, appunti → clipboard, lettura alle → read at; rimossi testi estranei nelle descrizioni di PostgreSQL, MongoDB, Chromium, MSVC, Rust, CMake, Telegram, Guardian e componenti Windows. Font, formati data, opzioni di comando, nomi API e frammenti di protocollo restano identificatori.

Il registro completo delle differenze è `catalog-diff.json`. `reviewed-terms.json` contiene le aspettative editoriali verificate dai test. Le chiavi italiane restano la fonte originale; non viene aggiunto un secondo blocco di analisi inglese.

## Collegamenti all’interfaccia

- I titoli delle finestre Nuova attività e Dettagli servizio seguono la lingua scelta.
- Anche titoli e descrizioni dei filtri di OpenFileDialog/SaveFileDialog vengono localizzati. Maschere `*.exe`, `*.com`, `*.pcapng` e percorsi non cambiano.
- L’elenco delle istanze dei componenti Windows usa un elemento testuale localizzato per la propria etichetta.
- La sorgente di cattura ha una proprietà di presentazione localizzata distinta dal nome originale e dalla chiave usata per l’acquisizione.
- La ricevuta di compilazione R35 R1 impedisce al launcher di riutilizzare per errore l’eseguibile R35.

Le finestre native di Windows possono conservare pulsanti o elementi nella lingua del sistema operativo. Nomi reali, metadati dei file, indirizzi, percorsi e messaggi originali del sistema restano dati originali. Nessun comando o criterio di classificazione viene tradotto prima della sua esecuzione.

## Controlli eseguiti

- **29 suite portabili, 1679 controlli PASS**.
- **372 controlli di localizzazione**: confronto delle voci revisionate, scansione dell’intero dizionario incorporato, corrispondenza con il catalogo testato, assenza di prose estranee note, opzioni CLI intatte, formati data/font, filtri dei file, identità della sorgente di cattura, italiano invariato nei rapporti e dati misurati preservati.
- **31 controlli statici PowerShell PASS** e fixture del launcher senza avviare Windows, UAC o processi applicativi.
- Compilazione C# dei sorgenti WPF contro riferimenti WindowsDesktop con campi XAML sostitutivi dichiarati, incluse le sette suite UI. Controllati anche RamMonitor, MemoryAgent e Profiler.
- Aggiunti al test WPF i controlli della finestra nativa di scelta programma, senza mostrarla: titolo/filtri inglesi, opzioni di validazione e ritorno esatto all’italiano.

**NOT RUN:** compilazione BAML reale e collaudo delle finestre su Windows, DPI, area di notifica, selezione file reale e cattura di rete. I controlli C# non equivalgono a queste prove. Nessuna nuova misura di consumo RAM è stata eseguita.

La scansione lessicale intercetta le classi di errore riscontrate; non sostituisce una revisione umana riga per riga di tutte le 5884 voci. Gli audit R35 e precedenti restano evidenza storica: per questa revisione usare gli esiti nella cartella R10_R35_R1.

## Avvio

Chiudi la versione precedente con Esci. Estrai tutto in una cartella nuova e avvia **AVVIA_PROCESSLENS.cmd**, poi scegli Italiano oppure English. Il primo avvio compila ed esegue i controlli con lo SDK .NET 8 disponibile. Non sono inclusi SDK, driver, modelli o pubblicazioni precedenti.

Per sviluppare: modificare i sorgenti canonici e il dizionario; eseguire `tools/refresh-localization.ps1` e `build-dashboard.ps1`. Le copie generate sono protette dal manifesto degli hash; non modificarle direttamente.
