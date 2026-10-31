# Fix crash WPF v0.11.3

## Difetto riprodotto dal log

```
XamlParseException: È stata generata un'eccezione durante l'impostazione di connectionId
Inner: InvalidCastException: Unable to cast ContextMenu to Grid
IComponentConnector.Connect(...), MainWindow.xaml:line 78
```

Nel markup precedente lo stile globale `ProcessListItemStyle` conteneva `Setter Property="ContextMenu"` con un `<ContextMenu Opened="...">` e handlers `Click` direttamente nel `Setter.Value`. Questo ha causato un'assegnazione del connectionId compilato verso un campo Grid anziché verso il ContextMenu.

## Correzione

Rimosso il ContextMenu inline dal `Style.Setter.Value`; inserito `<EventSetter Event="Loaded" Handler="ProcessItem_Loaded"/>`. Il metodo crea per ogni riga il menu in C# e collega gli handlers, senza introdurre alcun ContextMenu nel BAML delle risorse della Window.

La selezione del PID al clic destro e le verifiche prima della terminazione non sono cambiate.

## Preflight e test sul sistema

```
Set-ExecutionPolicy -Scope Process Bypass
.\bootstrap.ps1
```

Il bootstrap verifica il markup e monitora per 10 secondi se esiste una finestra visibile WPF, oltre al processo. In caso di errore stampa le ultime righe di `%LOCALAPPDATA%\ProcessLens\logs\startup.log`.

*Limite*: XML/preflight non eseguono BAML. Validare l'avvio e il clic destro sul PC Windows dell'utente.
