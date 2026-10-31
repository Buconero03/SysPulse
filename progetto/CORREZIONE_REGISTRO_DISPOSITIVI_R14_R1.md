# ProcessLens R14 R1 — registro dispositivi

Corregge l'eccezione `File troppo grande.` di NetworkDeviceTests riportata sulla R14. Lo stesso difetto riguardava schema sconosciuto, lista non valida o duplicati: InvalidDataException non era compresa nel filtro degli errori gestiti dal lettore.

Il file viene rifiutato con motivo esplicito, senza interrompere l'applicazione o trasformare i riconoscimenti in dati validi. Il limite resta **524.288 byte** e il massimo resta **512 dispositivi**. Non viene eliminato o riscritto un file non valido. Un file JSON valido esattamente al limite resta ammesso.

La correzione di produzione consiste nell'aggiunta esplicita di InvalidDataException al filtro catch, oltre al numero della candidata. Sono conservate la sezione Sistema Windows, la rete e l'integrazione Wireshark della R14.

## Riprovare su Windows

Estrarre questo ZIP in una nuova cartella e lanciare dalla radice del progetto:

```powershell
.\build-dashboard.ps1
```

Non occorre cancellare riconoscimenti, log, Guardian o configurazioni locali. Le fasi successive devono completarsi prima di considerare verificato l'intero build Windows.

## Controlli eseguiti qui

- Compilazione diretta Roslyn .NET 8: Core, NetworkDeviceTests, SystemProcessTests, PacketCaptureTests e sorgenti Windows collegati alle suite.
- **NetworkDeviceTests: 81 asserzioni superate**, con letture/scritture reali su file temporanei e culture it-IT/en-US. File oltre il limite, schema non supportato, elementi nulli, duplicati, numero eccessivo, JSON vuoto e soglia esatta; verificata la conservazione dei byte e l'assenza di file temporanei residui.
- **SystemProcessTests: 22 asserzioni portabili superate**; ramo di lettura Windows reale saltato.
- Stesso eseguibile di test rete contro il Core originale R14: riprodotta la stessa eccezione non gestita; con il Core corretto la suite passa.
- PacketCaptureTests compilata, non eseguita. Nessuna cattura o driver avviato.

È stata usata compilazione diretta perché il normale avvio della CLI SDK incontra una restrizione sulle informazioni di processo dell'host. Questo non equivale a eseguire il progetto con MSBuild, gli analizzatori del progetto o il build completo Windows.

**Non eseguiti:** build-dashboard.ps1, PowerShell, avvio/test WPF, API Windows reali, DPI e misura RAM. Rapporti, log, patch e hash in `audit/R10_R14_R1`. I rapporti R14 e precedenti rimangono storici.
