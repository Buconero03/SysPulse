# ProcessLens v0.19.13 — Structured Diagnostics & Technical Logging

**Stato:** candidata da compilare e validare su Windows 11 x64 con .NET 8. Le verifiche statiche non equivalgono alla compilazione WPF o a test runtime.

## Obiettivo

Il logging deve separare testo leggibile, evento tecnico verificabile e informazioni non disponibili. Non sostituisce l'osservazione Windows con inferenze e non aggiunge polling o servizi residenti.

## Registro eventi — codici ProcessLens, NON errori Microsoft

| ID | Componente | Significato |
|---|---|---|
| PL-GUI-1001 | GUI | fase di vita dell'interfaccia |
| PL-GUI-1002 | GUI | eccezione nella GUI |
| PL-MEM-2001 | RAM / MemoryAgent | fase di misura / fallimento lettura RAM |
| PL-MEM-2002 | MemoryAgent | attraversamento soglia / riarmo |
| PL-MEM-2003 | MemoryAgent | impossibile richiedere la notifica |
| PL-MEM-2004 | MemoryAgent | avvio o attività del monitor |
| PL-PROF-4000 | GUI | richiesta autorizzata di profiler |
| PL-PROF-4001 | Profiler | statistiche heap ottenute |
| PL-PROF-4002 | Profiler | profiler bloccato / non disponibile / fallito |
| PL-BUILD-5001 | Build | inizio fase |
| PL-BUILD-5002 | Build | errore fase / launcher |
| PL-BUILD-5003 | Build | fine fase completata |

I codici compilatore (`CS9035`, `MC3072`, `MSB...`, ecc.) rimangono separati da Event ID ProcessLens. Gli `HRESULT`, `Win32ErrorCode` e `SqliteErrorCode` sono riportati solo quando presenti: assenza NON equivale a zero. Gli ID dei processi possono essere riutilizzati e la correlazione fra sessioni non prova la stessa istanza senza timestamp di avvio.

## Registri JSONL locali

- `%LOCALAPPDATA%\ProcessLens\logs\startup.jsonl`
- `%LOCALAPPDATA%\ProcessLens\logs\memory-analysis.jsonl`
- `%LOCALAPPDATA%\ProcessLens\logs\memory-agent.jsonl`
- `%LOCALAPPDATA%\ProcessLens\logs\profiler.jsonl`
- `%LOCALAPPDATA%\ProcessLens\logs\build.jsonl`

Formato: un oggetto JSON per riga, `schema_version=1`, UTC, gravità, stato, codice, componente, operazione, `operation_id` quando disponibile, PID target e misure con unità nel nome campo. Eventi normali privi di misure non sono inventati. Un file JSONL è limitato a ~1 MiB con un archivio `.previous`, rotazione best-effort; il logging è best-effort, non garantisce persistenza in caso di crash/disco pieno. L'agente non scrive un evento a ogni intervallo 10s, ma soltanto per avvio, transizioni soglia e errori periodicamente limitati.

Esempio illustrativo di fallimento RAM, **non un evento rilevato**:

```json
{"schema_version":1,"event_id":"PL-MEM-2001","component":"ProcessLens.MemoryAgent","operation":"PhysicalMemory.ReadOrAlert","api":"GlobalMemoryStatusEx","severity":"Error","status":"Failed","reason_code":"RAM_READ_FAILED","exception_type":"Win32Exception","h_result_hex":"0x80070005","win32_error_code":5,"complete":false}
```

## Come leggere i log

Aprire PowerShell da dentro la cartella estratta:

```powershell
.\diagnostics-status.ps1 -Stream all -Last 20
.\diagnostics-status.ps1 -Stream build -Last 30
.\diagnostics-status.ps1 -Stream profiler -Last 10
```

Il lettore e' in sola lettura, non effettua acquisizioni ETW, profilazioni o modifiche a Guardian. `AVVIA-TUTTO.cmd` resta il punto di avvio abituale. `build-dashboard.ps1` esegue i test di regressione prima della compilazione/publish e si ferma al primo fallimento. `build.jsonl` registra fase, progetto, exit code ed eventuale prima posizione/codice di errore riconoscibile; le righe di compilazione complete restano nella console.

## Sicurezza / confini

- Nuovi JSONL non includono `Exception.Message`, `Exception.ToString()`, stack trace, comando del target, percorso assoluto del programma bersaglio, output grezzo di `dotnet-gcdump` o heap raw.
- Gli **storici log testuali preesistenti** (`startup.log` ecc.) restano invariati e possono contenere percorsi. Controllarli manualmente prima di condividerli. Non e' stato cancellato alcun log precedente.
- `MemoryAgent` mantiene il campionamento di 10s, la policy allarme 85%/riarmo <80%; non e' stato installato o attivato.
- **Guardian Service, sorgente Guardian, database SQLite e ETW** restano intatti in questa release. I loro log non sono migrati: richiederebbe un aggiornamento separato del servizio.
- Il profiler rimane opt-in: `dotnet-gcdump collect` induce GC Gen2 nel target e puo' consumare RAM. Nessuna acquisizione automatica viene aggiunta.
- Non sono stati aggiunti NuGet packages o nuove dipendenze runtime. Eventi JSONL usano `System.Text.Json` integrato in .NET 8.
- Il collector profiler conserva il limite RAM 85% (non abbassato in questa fase), limite temporale operativo e pulizia best effort. I limiti non equivalgono a un limite assoluto imposto dal kernel.

## Verifica Windows da effettuare

1. Estrarre in cartella nuova, non sovrascrivere una release esistente.
2. Da `AVVIA-TUTTO.cmd` raccogliere tutto l'output: `ProcessLens.DiagnosticsTests`, poi suite esistenti, build WPF e publish.
3. Eseguire `diagnostics-status.ps1 -Stream build` e verificare schema, codici, prima posizione e stato.
4. Verificare il solo avvio GUI; analisi RAM mirata su processo non critico a carico basso.
5. Misurare la differenza CPU/RAM della GUI e quantità di log prodotti rispetto alla versione precedente prima di dichiarare overhead trascurabile.
6. Non effettuare `-RunCapture` finche' la RAM e' alta o i test non sono verdi. La validazione end-to-end del profiler richiede consenso distinto.

## Documentazione Microsoft consultata

- LoggerMessage / logging ad alte prestazioni: https://learn.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging
- Logging con ambiti: https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/source-generation
- ETW provider/controller/consumer e perdite: https://learn.microsoft.com/en-us/windows/win32/etw/about-event-tracing
- Dotnet-gcdump e GC Gen2: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump
- PowerShell `$LASTEXITCODE`: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_automatic_variables?view=powershell-5.1
