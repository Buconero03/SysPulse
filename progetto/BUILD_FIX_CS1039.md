# ProcessLens v0.11.1 — Correzione build C#

## Problema risolto
In `src/ProcessLens.App/MainWindow.xaml.cs`, righe 1134–1154 della v0.11.0,
le descrizioni multilinea dei servizi contenevano ritorni a capo dentro
stringhe C# normali. Ne risultavano gli errori a cascata
`CS1039`, `CS1010`, `CS1002` e `CS1513`.

## Soluzione
Le sezioni `ServiceDetailDependencies` e `ServiceDetailTechnical`
sono ora costruite con `string.Join(Environment.NewLine, new[] { ... })`.
Nessun cambiamento ai dati letti dal Service Control Manager o alla cronologia eventi.

## Validazione
Sono stati effettuati controlli statici su sorgente, XAML, progetti e ZIP.
La build reale Windows/.NET non è stata eseguita nell'ambiente di produzione dell'archivio.

## Avvio
Da PowerShell nella cartella `ProcessLens-v0.11.1`:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\bootstrap.ps1
```

In alternativa, se la soluzione è già presente, eseguire
`dotnet build .\ProcessLens.sln -c Release`.
