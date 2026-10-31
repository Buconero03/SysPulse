# R10 R9 — correzione CS7036

Il log Windows fornito segnala CS7036 durante Tests.ProcessLens.CopyUiTests: il pulsante «Copia risorse GUI» chiamava ClipboardText.Copy con il solo testo. Il metodo richiede DependencyObject source e string text; source serve a individuare la finestra proprietaria se gli appunti sono occupati.

La chiamata ora è `ClipboardText.Copy(this, _selfResourceReport)`. Il controllo sul testo non vuoto resta presente. Firma del metodo, gestione appunti, misurazioni, servizi e componenti protetti restano invariati. L'unica altra modifica nei sorgenti applicativi è l'identificativo della candidata.

Il controllo portabile `audit/tools/test_clipboard_calls_r10r9.py` esamina tutte le chiamate dirette nei C# dell'app, ignorando commenti e stringhe. Verifica due argomenti non vuoti; include il rifiuto dell'esatta R8 consegnata e mutazioni con argomento mancante o aggiuntivo. È un controllo di sorgente, non un compilatore C# e non verifica gli appunti reali.

Il log dell'utente attesta il superamento dei test precedenti al blocco WPF nella R8; non attesta il completamento della build. Qui SDK .NET e PowerShell non sono disponibili: compilazione e suite Windows della R9 NON ESEGUITE. Nessun test disabilitato e nessun risultato Windows presunto.

Su Windows eseguire dalla cartella estratta:

```powershell
.\build-dashboard.ps1
```

La fase Tests.ProcessLens.CopyUiTests deve compilare la GUI e poi eseguire le verifiche WPF; devono completarsi anche le fasi successive. Dopo una build riuscita, verificare «Copia risorse GUI» con un campione disponibile e controllare il testo incollato. Il monitor separato e i suoi log rimangono quelli descritti in MONITOR_RAM_E_SERVIZI_R10_R8.md.

Audit corrente: audit/R10_R9. Confronto esatto con R8, patch applicata e confrontata byte per byte; manifest completo SHA256SUMS_R10.txt.
