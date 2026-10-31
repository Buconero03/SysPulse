# v0.19.1 — Test evidence report

## Problema osservato su Windows

Dopo i test di lettura RAM e di `QueryWorkingSetEx`, `tests/ProcessLens.MemoryTests/Program.cs` terminava con `FAIL allocation report evidence is misleading`. Il report conteneva `Nessuna categoria identifica`, mentre il test cercava il frammento `non identific`; il controllo falliva pur se la spiegazione era semanticamente corretta.

## Correzione

- `WindowsAllocationEvidence.cs`: limite delle prove esplicitato con la frase `Questa scansione non identifica...`; chiarito che il campione di pagine non rappresenta tutta la RAM fisica.
- `ProcessLens.MemoryTests`: test delle evidenze per culture `it-IT` e `en-US`, verifica delle categorie virtuali e delle pagine campionate, caso di API non disponibile; errore dettagliato.
- Nessun cambiamento al servizio Guardian, allo schema SQLite o alla policy di accesso al target.

## Validazione Windows

Dalla cartella del progetto estratto usare `AVVIA-TUTTO.cmd`, oppure `./build-dashboard.ps1` in PowerShell. La build deve continuare oltre `PASS allocation report...` ed eseguire i test del profiler e la compilazione della GUI.

Il tool `dotnet-gcdump` installato in precedenza non richiede una nuova installazione. Profilare un target solo dopo aver superato test e build, e soltanto con consenso esplicito.
