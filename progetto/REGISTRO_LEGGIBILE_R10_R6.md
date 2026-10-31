# R10 R6 — registro delle verifiche più leggibile

La schermata del 17 ottobre mostra il pannello specifico **Registro verifiche RAM — cosa è stato misurato**. Le modifiche R3 avevano organizzato il rapporto e le evidenze di identificazione, lasciando questo registro come un unico SelectableText in Consolas 11,5 DIP. Questa omissione spiega la densità ancora visibile nella schermata; non si deduce la versione esatta in esecuzione dall'immagine.

La R6 parte dalla candidata R10 R5 e conserva le funzionalità precedenti, inclusi copia, avviso di uscita e tray. Nessuna revisione semantica già completata viene ricominciata.

## Prima e dopo

| Prima | Dopo |
|---|---|
| Un solo blocco tecnico molto denso | Una scheda per ogni voce del registro |
| Orario, VERIFICA RAM, fase e messaggio ripetuti sulla stessa riga | Orario e fase nella testata, messaggio sotto |
| Messaggi in Consolas 11,5 | Messaggi in Segoe UI 13,5 DIP; timestamp separati in Consolas 11,5 |
| Quasi nessuna distanza fra eventi | 14 DIP di padding per scheda e 14 DIP fra schede; 9 DIP fra testata e messaggio |
| Registro senza panoramica | Conteggio delle voci e ultima fase/orario, senza inventare un esito positivo |
| Copia attraverso il testo o il rapporto completo | Pulsante aggiuntivo Copia registro completo, testo originale ancora selezionabile |
| Testo sorgente sempre disteso | Testo originale in un espansore separato, inizialmente chiuso |

Il registro principale resta aperto inizialmente. La modifica non si limita quindi a nascondere il testo: ne cambia la struttura visuale. I controlli non hanno altezze fisse; orario, fase e messaggio restano selezionabili. Le proprietà usate sono supportate da TextBox/SelectableText; non viene introdotto LineHeight, che in passato aveva causato errori XAML.

## Dati conservati

`MemoryAnalysisAudit.cs`, `memory-analysis.log`, codici evento JSON e valori dei contatori restano invariati. Il testo originale del registro continua a essere la fonte di Copia rapporto completo e di Copia registro completo. Il nuovo adattatore legge soltanto il testo già esposto dalla GUI. Non legge nuovi file, non avvia acquisizioni e non aggiunge dati al log.

Il parser riconosce i formati RAM e profiler già generati dal codice: timestamp validato in formato invariante, fase e messaggio. Le righe di continuazione, i limiti e i messaggi non riconosciuti restano visualizzati. Non si ricavano percentuali di affidabilità o stato PASS dalle parole del messaggio. Il percorso del registro locale viene mostrato separatamente e resta anche nel sorgente originale. Le stringhe numeriche it-IT/en-US non vengono convertite o riformattate.

Il modello visuale è un adattatore transitorio: la valutazione del progetto propone come passo successivo l'uso diretto di eventi tipizzati nella GUI, riutilizzando le informazioni strutturate già disponibili, con test dedicati e senza rompere i contratti esistenti.

## Aggiornamento e risorse

Se il registro cresce mantenendo il prefisso precedente, si aggiungono solo le nuove schede: le schede precedenti e la loro selezione non vengono sostituite. Quando inizia un'altra analisi o cambia il testo già presente, la vista viene ricostruita per non mescolare registri diversi. Chiudendo l'espansore del registro si rilasciano le schede generate; riaprendolo si ricrea la vista dai dati correnti.

Il costo in memoria della nuova vista e l'effetto reale sulla leggibilità non sono stati misurati qui. I controlli aggiuntivi servono alla separazione del testo e vengono limitati alle voci del registro corrente. Non viene promessa una riduzione RAM.

## Verifica

Le regressioni pure fornite coprono orario/fase/messaggio, continuazioni Unicode, localizzazione del file, overview neutra, profiler, testo sconosciuto, timestamp malformati, numeri con virgola/punto e copia CRLF originale. Le regressioni WPF fornite controllano padding e dimensione dei caratteri, append senza perdita di selezione, sostituzione del registro e collegamento reale dei controlli XAML ai nuovi handler TextChanged/Expanded/Collapsed.

Eseguiti qui: controlli strutturali Python/XML, regressioni portabili preesistenti, confronto byte dei componenti conservati, replay reale delle patch, integrità ZIP e manifest SHA-256.

**NON ESEGUITO:** build e test .NET, PowerShell, test WPF appena aggiunti, selezione reale nella GUI, verifica visiva/DPI. Ambiente Linux senza dotnet/pwsh. Un controllo sul sorgente non dimostra il risultato di una compilazione né quello del rendering Windows.

Prima della nuova build uscire dalla GUI attiva: nella R5 scegliere Esci da ProcessLens dal menu dell'icona vicino all'orologio. Poi estrarre in una cartella nuova e dalla radice con `src` e `tests`:

```powershell
.\build-dashboard.ps1
```

Solo se riesce:

```powershell
.\run-release.ps1
```

Verificare il registro RAM mostrato nella schermata, non soltanto il rapporto: schede, spaziatura, testo originale e pulsante di copia; ripetere a pannello stretto e DPI 100/125/150/200%. La verifica non richiede terminazioni, profilazioni, installazioni o riavvii di servizi.

Audit corrente `audit/R10_R6/`, manifest corrente `SHA256SUMS_R10.txt`. Sorgenti, test e rapporti restano sotto un'unica radice. Le criticità tecniche già documentate restano aperte e sono riprese in `VALUTAZIONE_PROGETTO_R10_R6.md`.
