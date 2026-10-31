# Build fix CS1009

La build di v0.9.0 si fermava in `MainWindow.xaml.cs`, riga 336, per tre errori CS1009 causati da `d\.hh\:mm\:ss` in una stringa interpolata C#.

La v0.9.1 usa invece `:c`, formato standard per `TimeSpan`, che produce ad esempio `01:23:45` oppure `1.01:23:45` per una durata superiore a un giorno.

La formattazione non cambia l'algoritmo di campionamento e calcolo CPU.

## Build Windows

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\bootstrap.ps1
```

Se la build segnala nuovi errori, conservarne l'output completo: questi altri errori non possono essere esclusi senza compilazione Windows/.NET SDK.
