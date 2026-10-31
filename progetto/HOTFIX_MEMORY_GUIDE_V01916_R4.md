# ProcessLens v0.19.16 R4 — Guida dettagliata alla RAM privata

## Obiettivo

Fornire nell'interfaccia WPF una spiegazione leggibile, espandibile e legata al PID selezionato per le tre misure distinte: memoria fisica privata residente (Private Working Set), Working Set totale, memoria privata impegnata (Private Bytes / commit).

## Implementazione

- `MainWindow.xaml`: nuovo Expander nella scheda «MEMORIA · MISURE SEPARATE», chiuso per impostazione predefinita per evitare un muro di testo; differenze fra misure, cautele sull'interpretazione e API Windows esplicite.
- `ProcessMemoryGuide.DescribeCurrentValues`: esempio dinamico per il PID selezionato, con tre valori indipendenti. N/D non diventa zero.
- `MainWindow.xaml.cs`: collega la guida al processo selezionato, senza nuove letture o timer.
- Test: controlli semantici di formattazione in it-IT ed en-US e verifica statico-XAML della guida.
- Codice di raccolta Windows, Guardian, MemoryAgent e profiler invariati.

## Fonte delle definizioni

Microsoft Learn, PROCESS_MEMORY_COUNTERS_EX2: https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2
Microsoft Learn, Process Working Set: https://learn.microsoft.com/en-us/windows/win32/procthread/process-working-set

## Validazione da eseguire su Windows

1. `dotnet run --project .\tests\ProcessLens.MemoryTests\ProcessLens.MemoryTests.csproj -c Release`
2. `AVVIA-TUTTO.cmd` per tutti i test e la build WPF.
3. Apri Processi, seleziona un PID e apri «Che cosa significa RAM privata?». Verifica che i valori corrispondano alle tre metriche della scheda e che la guida possa essere richiusa.
4. DPI al 100%, 125%, 150% e larghezza minima: testo senza sovrapposizioni, pannello scorrevole.

L'archivio è una **candidate** finché build, test .NET e verifica visiva su Windows non sono completati.
