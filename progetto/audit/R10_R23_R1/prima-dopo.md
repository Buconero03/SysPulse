# R23 R1 — validazione prima dell'espansione Windows

## Errore ricevuto e causa

La build Windows R23 si è fermata in ProcessLens.NewTaskTests, sul secondo caso bad?.exe dopo il caso con LF. Quel secondo input contiene NUL (bad\0.exe). È un fallimento di test a runtime, non un errore sintattico C# o un SDK mancante. Dal log utente, le fasi successive non risultano eseguite.

R23 controllava i caratteri dopo Trim ed Environment.ExpandEnvironmentVariables. Su Windows .NET usa ExpandEnvironmentStrings con stringhe terminate da NUL: bad\0.exe può diventare bad, un nome breve ammesso dal validatore. La prova Linux precedente non attraversava quel backend Win32. Il difetto era nella validazione di ProcessLens.

| Prima | R23 R1 |
|---|---|
| Controllo dopo Trim ed espansione | Controllo sui campi originali prima di entrambe le operazioni |
| NUL può sparire sul backend Windows | Input rifiutato prima della chiamata al backend |
| LF e NUL hanno la stessa etichetta bad?.exe | Marcatori LF/NUL distinti nel test |
| Test sul solo comportamento del sistema ospite | Anche backend sintetico Win32 e verifica dell'ordine delle chiamate |

Restano i controlli dopo l'espansione, i limiti di lunghezza e il TAB ammesso negli argomenti. Nessun test originale rimosso o indebolito. La firma pubblica TryCreate è invariata; l'helper interno iniettabile e InternalsVisibleTo limitato alla suite permettono di provare la differenza tra piattaforme senza chiamate native o programmi utente.

## Verifiche eseguite qui

- 48 asserzioni NewTaskTests, incluse le 31 precedenti: PASS su Linux/.NET 8.0.32, compilazione diretta Roslyn SDK 8.0.426.
- 17 nuove asserzioni: modello di troncamento, NUL nel programma/cartella/argomenti, CR/LF prima del trimming, nessuna chiamata al backend su input invalidi, rivalidazione senza espansione, percorsi validi e controllo del risultato espanso.
- 103 asserzioni WindowsComponentTests e 22 SystemProcessTests contro il Core corrente: PASS. Totale portabile: 173.
- Riproduzione negativa: tolta soltanto la guardia iniziale da una copia privata, il test fallisce esattamente sul caso Win32 simulato. La copia difettosa NON è inclusa. Evidenza LAB/SIMULATED, non esecuzione Windows.
- 30 controlli preliminari PowerShell e 17 fixture launcher: PASS su PowerShell 7.4.21/Linux.
- Type checking C# di Windows, Semantics, GUI e quattro suite WPF: PASS con reference WindowsDesktop e stub espliciti dei campi XAML/InitializeComponent. Non è compilazione markup o esecuzione WPF.
- Diff applicabile, confronto byte dopo applicazione e manifest SHA-256 integrale verificati dal confezionamento.

## Ambito e limiti

Modificati soltanto validatore, test pertinente e identificazione della candidata nel csproj. Wrapper e istruzioni aggiornano revisione e ricevuta build-r23r1.ok, per non riusare una pubblicazione R23. MainWindow, finestra Nuova attività, launcher dei processi, acquisizione Windows, Guardian, CPU/RAM, profiler, MemoryAgent e rete rimangono identici a R23. Nessuna nuova dipendenza. Pipeline e asserzioni restano attive.

NON ESEGUITI: backend Win32 nativo, build MSBuild Windows completa, XAML/BAML, suite WPF reali, interazione utente, DPI e RAM Windows. La simulazione copre la causa osservata ma non sostituisce queste prove. I PASS nelle cartelle audit precedenti restano storici.

## Ripetizione su Windows

1. Uscire dalla GUI precedente tramite icona -> Esci da ProcessLens; la X riduce soltanto.
2. Estrarre tutto lo ZIP in una cartella nuova e aprire AVVIA_PROCESSLENS.cmd.
3. La prima esecuzione riusa SDK/cache disponibili ed esegue la pipeline completa. Devono passare bad<LF>.exe, bad<NUL>.exe e le nuove regressioni R23 R1.
4. Solo dopo successo viene scritta build-r23r1.ok; gli avvii seguenti sono diretti. Il completamento delle fasi WPF e successive va verificato dal nuovo log Windows.

## Fonti

- https://learn.microsoft.com/en-us/windows/win32/api/processenv/nf-processenv-expandenvironmentstringsw
- https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Environment.Windows.cs
