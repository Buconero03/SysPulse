> R35 R2: corretti i due errori di ereditarietà WPF (CS0509 e CS0263). Traduzioni IT/EN invariate rispetto a R35 R1. Rapporto: `progetto/audit/R10_R35_R2/REVISIONE_R35_R2.md` (dalla radice del pacchetto).

> R35 R1: 134 traduzioni corrette, filtri e titoli delle finestre collegati alla lingua, controlli lessicali e sui formati tecnici aggiunti. Rapporto in `progetto/audit/R10_R35_R1/REVISIONE_R35_R1.md`.

# Analisi in italiano oppure in inglese — R35

Il programma mostra una sola versione dell’analisi, nella lingua scelta. Non aggiunge una traduzione inglese sotto il rapporto italiano.

Avvia `AVVIA_PROCESSLENS.cmd` e scegli **Italiano** oppure **English**. I due avvii diretti già presenti saltano questa domanda. Nell’app puoi usare **Lingua / Language**. Un’istanza già aperta viene ripristinata nella sua lingua corrente.

Il dizionario è `progetto/src/ProcessLens.Core/Localization/Analysis.en.json`. Le chiavi sono le frasi italiane originali; i valori sono le versioni inglesi. Comprende descrizioni, spiegazioni, diagnosi CPU/RAM/rete, origine, avanzamento, limiti, servizi e interfaccia. I testi italiani restano la fonte originale.

L’inglese non viene ottenuto sostituendo parole dentro i dati di un processo. I modelli di frase conservano separatamente nomi, PID, indirizzi, percorsi, comandi e misure. I metadati dichiarati dai file e gli errori originali del sistema possono rimanere nella lingua della fonte: non sono descrizioni riscritte dall’app. I vecchi log non vengono riscritti sul disco. I protocolli JSON, gli identificatori tecnici e i log grezzi restano compatibili.

Il monitor RAM e il profiler leggono la preferenza salvata all’avvio. Riavvia gli strumenti separati dopo aver cambiato lingua. I servizi di raccolta conservano i loro eventi originali.

## Per modificare il progetto

Modifica i sorgenti canonici accanto alla cartella `Localized`, non i file `.g.cs` o `.g.xaml`. Aggiorna anche il dizionario per le nuove frasi, quindi esegui da `progetto`:

```powershell
.\tools\refresh-localization.ps1
.\build-dashboard.ps1
```

Il generatore usa Roslyn incluso nello SDK .NET; non richiede un traduttore, un servizio online o un modello locale nell’app. Il controllo R35 blocca la build se sorgenti e copie generate non corrispondono; il test di localizzazione rifiuta ogni chiave mancante.

La verifica della candidata è documentata in `progetto/audit/R10_R35/REVISIONE_R35.md`.
