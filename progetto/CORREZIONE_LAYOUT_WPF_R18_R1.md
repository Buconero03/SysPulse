# R18 R1 — layout differito nel test del testo copiabile

Candidata basata sulla R18 completa. Il log ricevuto il 22 ottobre 2026 proviene invece dalla R17 (`D:\.A2\ProcessLens-v0.19.16-R10-R17-pktmon-wifi-native-candidate`). La R18 usa ancora lo stesso helper di layout, quindi la correzione viene applicata sulla versione più recente mantenendo la cattura IP locale, la conservazione degli errori e le spiegazioni dell’audit della R18.

## Errore segnalato

Il test `ProcessLens.CopyUiTests` ha completato le prove di copia Unicode e ha trovato il template nativo. Il primo Measure ha restituito `desired=210.00x17.29`, poi è fallita l’asserzione `multiline output measures at finite height`. Il test richiedeva più di 20 DIP per un testo di almeno tre righe.

Nel log precedente al blocco risultano completate le suite UI rete e sistema della R17 e le letture native Windows esplicitamente riportate. Non sono prove di esecuzione della R18 o di questa candidata. Le fasi successive al blocco non sono state eseguite.

## Diagnosi e correzione candidata

Il sorgente Microsoft di WPF 8 (`TextBoxView.FullMeasureTick`) limita il lavoro di formattazione di un singolo passaggio. Se esaurisce il tempo, conserva la misura del testo già analizzato e accoda `OnBackgroundMeasure` a priorità Background. Il primo Measure può quindi rappresentare soltanto una riga; `EndInit` non garantisce la conclusione del layout differito. Questa è una spiegazione coerente con la misura segnalata, non una riproduzione eseguita su quel Windows.

Il vecchio helper verificava l’altezza prima di Arrange, UpdateLayout e del lavoro differito. Il nuovo helper:

1. misura, dispone e aggiorna il controllo nativo;
2. elabora la coda Dispatcher fino a ContextIdle, così il lavoro Background già accodato può proseguire;
3. rimisura e ridispone dopo le eventuali invalidazioni;
4. attende dimensioni e numero di righe stabili tra passaggi, con limite di 16 passaggi e budget di attesa di 5 s;
5. applica tutte le asserzioni originali: template reale, altezza finita maggiore di 20, almeno tre righe, crescita a larghezza ridotta, testo e selezione Unicode invariati.

Il termine dell’attesa non dipende dal superamento della soglia di altezza. Timeout, timer e operazioni accodate vengono ripuliti anche su errore. I callback WPF in esecuzione non vengono interrotti forzatamente dal budget.

È aggiunta una regressione deterministica: il controllo parte con una riga e riceve tre righe tramite callback Background. Il test deve includere quel lavoro prima delle asserzioni multilinea, anche con cache dei font già calda. Il log diagnostico ora riporta misura iniziale/finale, numero di righe e passaggi.

Non sono introdotti template sostitutivi, altezze artificiali o soglie più permissive. Tutte le asserzioni precedenti restano presenti. Tutto il codice C# operativo e il markup XAML sono identici alla R18; cambiano il test, la versione della candidata, il controllo sorgente e la relativa riga nella pipeline.

## Verifiche

- Controllo tipi C# attuale: Windows, Semantics, App e tre progetti UI, sei assembly senza errori o avvisi, con le reference WindowsDesktop e gli stub espliciti dei soli campi generati. Non è compilazione BAML né esecuzione WPF.
- 260 controlli sorgente/XML/mutazione e conservazione passati, inclusi 25 specifici della correzione.
- 332 asserzioni portabili della R18 conservate con verifica degli hash di ogni input compilato; non rieseguite per questo cambiamento limitato al test WPF.
- **NON ESEGUITI qui:** test WPF corretto, compilazione completa Windows/PowerShell, rendering/DPI e cattura reale. L’ambiente disponibile è Linux. Nessuna riproduzione Windows o misura RAM viene dichiarata.

## Prova su Windows

Estrarre questa candidata in una cartella nuova e lanciare:

```powershell
.\build-dashboard.ps1
```

Per isolare la suite che si è fermata:

```powershell
dotnet run --project .\tests\ProcessLens.CopyUiTests\ProcessLens.CopyUiTests.csproj -c Release
```

Se fallisce ancora, il log deve conservare la riga completa con `first=`, `desired=`, `lines=` e `passes=`. Il test continua a fallire se la reale formattazione non soddisfa le asserzioni: non viene saltato né convertito in PASS.

## Fonti primarie consultate

- [WPF 8: TextBoxView e misura differita](https://github.com/dotnet/wpf/blob/release/8.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/documents/TextBoxView.cs)
- [WPF: Measure, Arrange e aggiornamento del layout](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/layout)
- [TextBox.LineCount e disponibilità del layout](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.textbox.linecount)

Audit attuale: `audit/R10_R18_R1`, con log Windows ricevuto, prima/dopo, patch completa R18→R18 R1, registro stringhe e manifest SHA-256.
