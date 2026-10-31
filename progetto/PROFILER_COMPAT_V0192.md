# ProcessLens v0.19.2 — Compatibility verification (candidate)

## Problema riscontrato durante la revisione statica

La classe `GcDumpHeapStatParser` della v0.19.1 riconosce l'intestazione storica `Object Bytes Count Type` e cerca i totali `GC Heap bytes` e `GC Heap objects`. La documentazione ufficiale Microsoft per `dotnet-gcdump` 10 presenta invece `Size (Bytes) Count Type` e non mostra quei totali. Non e' prova che il tool installato sull'utente produca esattamente quel formato; da verificare su Windows.

Fonte: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump

## Correzione limitata al profiler

- Riconoscere entrambi i formati della tabella heapstat.
- Se mancano i totali espliciti, ricavarli dalla somma delle **tutte** righe parse (non soltanto i top-N).
- Se l'output del processo di diagnostica supera il limite, rifiutare il report, senza valori parziali presentati come completi.
- Aggiunti test deterministici per il formato documentato da Microsoft e per la regressione del parser precedente.

## Validazione richiesta sul PC Windows 11 x64

1. Estrarre lo ZIP completo in una cartella **nuova**, senza sovrascrivere v0.19.1.
2. Da PowerShell, nella cartella contenente `build-dashboard.ps1`:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build-dashboard.ps1
```

3. Confermare tutti i `PASS` dei test RAM, Behavior e Profiler, `dotnet build` di GUI e profiler e il publish.
4. Verificare `dotnet-gcdump --version`, `dotnet-gcdump report --help` e `.\profiler-status.ps1` (nessun dump).
5. Profilare un processo .NET di test **non critico** soltanto dopo consenso esplicito, acquisendo CPU/RAM prima/durante/dopo, durata, esito, timeout, cleanup e accuratezza del report. Non profilare Guardian né servizi di sistema.
6. Se la build fallisce, conservare il log integrale: nessun test va disabilitato.

## Precisazione

La compatibilita' con il formato pubblicato da Microsoft e' controllabile staticamente. La compatibilita' con l'eseguibile `dotnet-gcdump` 10.0.750501 e il corretto funzionamento Windows vanno ancora verificati sul computer di destinazione. Nessun profiler reale e' stato avviato in questa revisione.
