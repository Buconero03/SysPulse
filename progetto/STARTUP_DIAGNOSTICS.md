# ProcessLens v0.11.2 - avvio e crash diagnostics

## Problema osservato

v0.11.1: compilazione .NET 8 completata e test deterministici superati;
ProcessLens.exe tornava immediatamente al prompt senza aprire una finestra.
Non e' stata ancora acquisita un'eccezione di Windows da quel PC: non attribuire
il guasto a una specifica API o a un controllo WPF prima di aver letto il log.

## Modifiche

- Registrazione di fasi `OnStartup`, costruzione XAML, `Show`, primo campionamento.
- Eccezioni non gestite WPF, AppDomain e task asincroni registrate localmente.
- Finestra con messaggio esplicito se l'avvio gestito fallisce, con percorso del log.
- Guardia agli eventi della lista, filtri, ComboBox e grafico prima di `Window.IsLoaded`.
- Fallimento del primo campionamento non chiude piu' l'intera finestra.
- Se il mutex e' occupato ma non si trova alcuna finestra, viene mostrato un avviso.
- `bootstrap.ps1` e `run-release.ps1` eseguono una verifica post-avvio.
- `diagnose-startup.ps1` mostra il log locale e eventuali eventi .NET Runtime / Application Error.

## Privacy

I log restano in `%LOCALAPPDATA%\ProcessLens\logs\startup.log` (massimo circa 1 MB
prima della rotazione). Eccezioni e stack trace possono contenere percorsi locali.
Non vengono inviati a server remoti.

## Prova manuale in Windows

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\bootstrap.ps1
.\diagnose-startup.ps1 -NoLaunch
```

Se l'eseguibile esce entro 3 secondi, leggere le ultime righe del log, e, in assenza
di log, gli eventi del registro Applicazione Windows. La disponibilita' della
finestra e il monitoraggio dei processi sono ancora da verificare sul PC reale.
