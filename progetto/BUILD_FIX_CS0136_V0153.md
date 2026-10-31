# v0.15.3 — CS0136: GuardianLineageReader.cs

## Causa

Nel blocco che conta ed enumera i figli del processo selezionato, il codice v0.15.2 dichiarava `using (var cmd = ...)` e subito dopo `using var cmd = ...` nello stesso ambito esterno. Il compilatore C# segnala `CS0136` sulla prima dichiarazione.

## Correzione

La query di conteggio usa una variabile distinta (`countCmd`) e mantiene il blocco `using` limitato. La query delle righe continua a usare `cmd`. Nessuna logica SQL, interfaccia o API è stata modificata.

## Validazione

* Preflight WPF esteso con controllo per la regressione.
* Test già presenti di lettura SQLite e identificazione di generazioni PID invariati.
* Build .NET non eseguita nell'ambiente di confezionamento: su Windows eseguire `./build-dashboard.ps1` e verificare 0 errori/0 avvisi.

## Deploy

Aggiornare soltanto la GUI. Non eseguire `upgrade-guardian.ps1` né reinstallare `ProcessLensGuardian`.
