# ProcessLens Guardian v0.13.2 — ETW namespace build fix

## Sintomo

`EtwProcessMonitor.cs(40,46): error CS0103: Il nome KernelTraceEventParser non esiste nel contesto corrente`.

## Causa e correzione

`KernelTraceEventParser` e' dichiarata nello spazio dei nomi `Microsoft.Diagnostics.Tracing.Parsers` del pacchetto `Microsoft.Diagnostics.Tracing.TraceEvent`. Il sorgente precedente importava solo il namespace figlio `Microsoft.Diagnostics.Tracing.Parsers.Kernel`, che contiene gli oggetti degli eventi ma non il parser.

Aggiunta la direttiva: `using Microsoft.Diagnostics.Tracing.Parsers;`.

Non sono state modificate la raccolta ETW, le opzioni SQLite, la configurazione del servizio, gli script di installazione o la logica di sicurezza. La compilazione di Guardian.Service rimane un passaggio esplicito di `build-guardian.ps1`: nessun servizio e' installato automaticamente.

## Verifica da Windows

In PowerShell nella radice della v0.13.2:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build-guardian.ps1
```

Installare il servizio SOLO dopo che tutti i test, la build e la pubblicazione si concludono con successo. In caso di altri errori, riportare l'intero nuovo log.

## Validazione del pacchetto

Controlli statici XML/XAML/JSON e presenza del namespace; non e' stata eseguita una compilazione .NET sull'host che prepara l'archivio.
