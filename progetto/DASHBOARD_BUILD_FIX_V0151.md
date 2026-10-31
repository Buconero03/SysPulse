# ProcessLens v0.15.1 — Correzione compilazione Dashboard

## Diagnosi sul log Windows (10 ottobre 2026)

`build-dashboard.ps1` superava i test preliminari e SQLite, ma la compilazione WPF falliva con 5 errori: `Path`, `File`, `FileNotFoundException`, `FileInfo` non risolti in `GuardianDashboardReader.cs`. Il file utilizzava i tipi di `System.IO` senza importarli esplicitamente. Compariva anche un avviso `CS8602` nel `finally` della funzione di arresto servizi, perché `ServiceStopButton` poteva essere `null`.

## Correzioni

- `src/ProcessLens.App/GuardianDashboardReader.cs`: `using System.IO;`.
- `src/ProcessLens.App/MainWindow.xaml.cs`: guardia non-null su `ServiceStopButton` prima di modificarne `IsEnabled`.
- `tests/check-guardian-dashboard.ps1`: preflight per verificare entrambe le correzioni.
- Versione app 0.15.1; il servizio Guardian installato rimane alla 0.14.1 e non deve essere reinstallato.

## Esecuzione su Windows

```powershell
cd "D:\.A2\ProcessLens-v0.15.1-guardian-dashboard-build-fix\ProcessLens-v0.15.1"
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build-dashboard.ps1
# Eseguire solo dopo il successo della build:
.\run-release.ps1
```

Gli errori C# identificati dal log sono stati corretti nel sorgente. La compilazione .NET 8 WPF sul computer Windows deve essere confermata prima di considerare la release validata.
