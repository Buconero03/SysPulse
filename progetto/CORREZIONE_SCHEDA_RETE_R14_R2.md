# ProcessLens R14 R2 — inizializzazione della scheda rete

Il nuovo log conferma il completamento dei test del registro dispositivi e dell'integrazione Wireshark della R14 R1. Il build si ferma al primo aggiornamento di NetworkUiTests, un test WPF che crea la vista senza mostrarla.

La vista rete richiede la scheda cache selezionata per acquisire i dati. Nel markup non era dichiarata una selezione iniziale: l'aggiornamento prima del caricamento visivo dipendeva dal ciclo di selezione di WPF. La R2 imposta esplicitamente `SelectedIndex="0"` sul TabControl reale.

Non vengono avviate letture quando si apre o si cambia scheda: l'aggiornamento resta manuale. La scheda Wireshark non può richiedere l'aggiornamento della cache. Il test conserva i tre requisiti originali (una chiamata, una riga, nessun falso avviso) e li rende separati, con valori effettivi nel messaggio di errore. Verifica anche selezione e pulsante prima di Loaded e cambio scheda senza acquisizione automatica. Il cast al controllo rete gestisce esplicitamente il caso null, correggendo i due avvisi segnalati.

Il delta di produzione è un attributo XAML e l'identificativo della candidata. Il codice C# di produzione è identico alla R14 R1; la correzione del registro rimane inclusa.

## Verifica su Windows

Estrarre lo ZIP in una nuova cartella e, dalla radice del progetto, eseguire:

```powershell
.\build-dashboard.ps1
```

Non serve cancellare file o configurazioni locali. La sequenza ripete i controlli e prosegue oltre NetworkUiTests solo se il test aggiornato passa.

## Stato delle verifiche

Il controllo di sorgenti e XML verifica la selezione iniziale reale, le guardie di aggiornamento e le asserzioni, e rifiuta markup con selezione assente o errata. È stato controllato che gli altri sorgenti di produzione non cambino.

**Non eseguiti qui:** avvio o test WPF, test Windows reali, DPI e build completo PowerShell. L'host Linux impedisce anche l'avvio del build incrociato MSBuild per le informazioni del processo: non è stata ottenuta una compilazione WPF. Questa candidata corregge l'inizializzazione individuata nei sorgenti; la conferma del comportamento spetta all'esecuzione Windows del test aggiornato.

Il log fornito, la patch completa, il registro della modifica e i risultati dei controlli effettivi sono in `audit/R10_R14_R2`. I risultati R1 e precedenti sono conservati come storici.

## Riferimento tecnico

Nel [sorgente WPF Microsoft, TabControl.OnGeneratorStatusChanged](https://github.com/dotnet/wpf/blob/v8.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/TabControl.cs), la selezione automatica del primo elemento dipende dallo stato ContainersGenerated del generatore. Il test non mostra la vista né applica il template visivo. La selezione esplicita elimina questa dipendenza; questa revisione del sorgente non sostituisce il test WPF Windows.
