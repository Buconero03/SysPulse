# R31 R1 — riferimento di progetto mancante

Base: R31 consegnata, SHA-256 `72659efa186e4cfe49b019f1502ecc80dddf6eeaf2cd0c8243c4a17b46586929`.

Il log Windows dell'utente si ferma in Tests.ProcessLens.ProcessGroupsTests con CS0234 a Program.cs:361. Il test nuovo usa ProcessLens.Semantics.ProcessIntelligenceAnalyzer, ma il .csproj dichiarava solo Core. La precedente prova Roslyn forniva separatamente Semantics tra i riferimenti e non verificava questa parte del grafo MSBuild. Omissione nella candidata, non SDK/runtime mancante sul PC.

## Correzioni circoscritte

- ProjectReference esplicito a ProcessLens.Semantics nel .csproj dei test. Nessuna sostituzione delle asserzioni e nessun test saltato.
- Guardia esplicita path is null prima delle dereferenziazioni in WindowsProcessIconPolicy.SafeWindowsPath. Elimina CS8602; i casi di classificazione esistenti rimangono verificati.
- Preflight della build che richiede il riferimento dichiarato. Versione informativa e ricevuta launcher R31 R1, per non avviare una pubblicazione R31 precedente.
- UI, semantica, dati dei dizionari, Core, acquisizione nativa Windows, servizi, Guardian, CPU/RAM, profiler e politiche finestre/scorrimento byte identici alla R31 salvo i due file dichiarati sopra e la versione.

## Verifiche attuali

- 13 progetti portabili costruiti con il vero SDK/MSBuild 8.0.426 tramite dotnet build, poi eseguiti con runtime 8.0.32 su Linux: 822 asserzioni PASS. I riferimenti sono quelli dei .csproj e i contenuti knowledge vengono copiati da MSBuild; nessuna aggiunta manuale degli assembly al compilatore. Directory artifacts separate fuori dal pacchetto e sorgenti originali controllati per hash. Le invocazioni locali usano grafi seriali, compilazione non condivisa, sorgenti NuGet vuote e resolver dei workload non usati disabilitato nel solo ambiente dei test.
- Test negativo: rimossa solo la ProjectReference Semantics in una copia isolata, il vero dotnet build restituisce CS0234 alla stessa riga 361. La candidata originale conserva il riferimento e supera compilazione ed esecuzione dei 214 controlli di quel progetto. Prova che la nuova verifica vede l'errore mascherato dal precedente controllo.
- 39 controlli sorgente PowerShell, 17 fixture launcher. Il riferimento viene controllato anche dal normale build-dashboard.ps1.
- C# di 9 assembly WindowsDesktop verificato con reference assemblies e stub dei campi XAML fuori dal pacchetto. Qualifica separata: non e MSBuild WPF, generazione BAML o esecuzione della GUI. L'avviso nullable segnalato e assente.
- NOT RUN: esecuzione WPF/Windows, rami nativi Windows dei test, DPI/UAC, clic fisico di Explorer. NOT MEASURED: RAM. La normale build Windows proseguira alle prove successive, che non sono dichiarate qui gia superate.
- I tentativi di orchestrazione tramite figli Python su questo host hanno incontrato errori locali di lettura dei metadati /proc del processo prima dell'avvio di MSBuild. Le prove riportate usano invocazioni SDK dirette in processi indipendenti; nessuna modifica agli assembly SDK, agli algoritmi o alle asserzioni per aggirare errori di compilazione.

I log R31 precedenti rimangono storici. Non si include nello ZIP il log personale dell'utente.

## Riproduzione su Windows

Dalla directory progetto:

```powershell
dotnet build tests/ProcessLens.ProcessGroupsTests/ProcessLens.ProcessGroupsTests.csproj -c Release
dotnet run --project tests/ProcessLens.ProcessGroupsTests/ProcessLens.ProcessGroupsTests.csproj -c Release --no-build
```

Oppure avviare AVVIA_PROCESSLENS.cmd nella radice estratta. Nessuna reinstallazione proposta per correggere il riferimento.
