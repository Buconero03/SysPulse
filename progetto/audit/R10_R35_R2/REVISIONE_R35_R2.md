# R35 R2 — correzione della compilazione WPF

Base: R35 R1, SHA-256 `eed90e6eee7aaee7789d5b3385f21ec9395fc52ef8e21104d03d8ae390e474f5`.

## Errore segnalato e causa

Il log Windows allegato interrompe la compilazione con CS0509: `MainWindow` non può derivare dalla classe sealed `LocalizedWindow`. La radice del markup generato era `LocalizedWindow`, mentre il controllo base era stato dichiarato sealed. I controlli precedenti con soli campi XAML sostitutivi non riproducevano la dichiarazione della classe base generata da WPF e non intercettavano questo errore.

Riproducendo anche tale dichiarazione sono stati osservati due errori, conservati negli allegati di questo audit:

1. `before-fix.log`: CS0509, classe base sealed.
2. `nonsealed-only.log`: CS0263, dopo aver reso la base ereditabile il codice della finestra continuava a dichiarare `Window`, mentre il markup dichiarava `LocalizedWindow`.

## Correzione

- `LocalizedWindow` è una classe pubblica ereditabile. La registrazione dei metadati resta sul tipo derivato proprio dell'app; non viene modificato alcun tipo base WPF.
- Il generatore allinea il tipo base dei code-behind `.xaml.cs` alla radice localizzata del relativo markup. `MainWindow` ora deriva da `LocalizedWindow` in entrambe le dichiarazioni parziali. Il sorgente italiano canonico resta `MainWindow : Window`.
- La verifica C# ricostruisce il tipo base di ogni `x:Class` dalla sua radice XAML, oltre ai campi nominati. Questo riproduce entrambi gli errori precedenti e passa dopo la correzione.
- La build Windows esegue un nuovo controllo preliminare: radici localizzate ereditabili e classi base coerenti. Una fixture verifica sia i due casi errati sia quello corretto.
- Il test UI include una finestra derivata dalla base localizzata, un controllo sul tipo base effettivo di `MainWindow` e il cambio di lingua del titolo. La suite è stata compilata; l'esecuzione WPF richiede Windows.
- Versione e ricevuta del launcher passano a R35 R2 per evitare di riutilizzare una pubblicazione R35 R1.

Il dizionario inglese è identico byte per byte a R35 R1. Nessun testo italiano, contatore o criterio di analisi è stato riscritto per questa correzione.

## Verifiche di questa revisione

- PASS: riproduzione di CS0509 e CS0263 prima delle rispettive correzioni.
- PASS: compilazione C# dell'app e delle sette suite UI con riferimenti WindowsDesktop e dichiarazioni sostitutive dei campi e dei tipi base XAML.
- PASS: 372 controlli di localizzazione eseguiti con il catalogo e l'inventario attuali.
- PASS: 32 controlli statici PowerShell, compresa la verifica degli hash dei sorgenti e delle copie generate.
- PASS: fixture del controllo delle radici XAML, con rifiuto della classe sealed, rifiuto dei tipi base diversi e accettazione della coppia corretta.
- PASS: 17 controlli della fixture del launcher, senza avviare app o UAC.

NOT RUN: compilazione WPF/BAML e funzionamento delle finestre su Windows. I controlli con riferimenti e dichiarazioni sostitutive non equivalgono a tali prove. Un tentativo MSBuild nel contenitore è stato interrotto da un errore dell'ambiente in `Process.get_ProcessName`; la compilazione C# e i test portabili sono stati eseguiti direttamente con Roslyn dello SDK .NET 8.0.426. Il relativo log resta allegato.

I risultati delle altre suite nell'audit R35 R1 sono storici e non vengono presentati come rieseguiti in questa revisione. Nessuna acquisizione di rete o modifica a Guardian è stata eseguita.

## Avvio su Windows

Chiudi la versione precedente con **Esci**, estrai tutto lo ZIP in una cartella nuova e avvia **AVVIA_PROCESSLENS.cmd**. Scegli Italiano oppure English. Il primo avvio esegue test e compilazione; l'esito completo sul tuo Windows resta la verifica conclusiva del pacchetto.
