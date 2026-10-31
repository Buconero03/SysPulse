# ProcessLens v0.19.16 R7 R1 — correzione domanda sulle porte

Base: R7 candidata, audit precedente conservato. Non è una release stabile.

Il log Windows fornito dall’utente conferma che MemoryTests (incluse culture it-IT/en-US e regressioni R7) è passato. BehaviorTests ha superato i controlli precedenti e le prime tre regressioni R7, poi è fallito su «partial network acquisition». Le fasi successive sono NON ESEGUITE; il codice PL-BUILD-5002 è il contenitore diagnostico della fase fallita, non un errore di sintassi del compilatore.

La domanda «Ha porte aperte?» non corrispondeva a nessun intent: il matcher cercava «porta», ma non «porte». Veniva restituito Help(), quindi mancavano AccessDenied e il limite sull’assenza di socket. La stessa domanda è proposta dalla GUI e dall’aiuto: non è un semplice errore del test.

R1 riconosce porta/porte/port/ports con confini di parola. Mantiene gli altri indicatori di rete e gli altri intent. Evita anche i falsi positivi «comporta», «riporta», «supporta». Il test precedente è conservato e rafforzato con il titolo della risposta; aggiunti casi sul plurale, maiuscole, inglese e parole con porta interna.

La modifica funzionale è una riga in ProcessQueryEngine; nessun collector, contatore, contratto, ID knowledge, Guardian, MemoryAgent, profiler o diagnostica strutturata modificato. I metadati/banner identificano R7 R1. Gli audit sotto audit/R7 e audit/prior sono storici della revisione precedente; il delta attuale è in audit/R7_R1. Il manifest SHA256SUMS_R7.txt è rigenerato e copre la candidata attuale. Nessuna installazione/profilazione/terminazione eseguita.

Verifiche locali: controlli strutturali e casi di riconoscimento della parola eseguiti con Python; questi ultimi verificano la forma del predicato, non eseguono ProcessQueryEngine. Build, test .NET/PowerShell/WPF della R1 NON ESEGUITI qui: ambiente Linux senza dotnet/pwsh. Gli esiti del log dell’utente riguardano R7 prima della correzione e non sono attribuiti alla R1.

Su Windows, estrarre nuovamente il pacchetto completo aggiornato senza mescolare file precedenti, aprire PowerShell nella radice contenente src e tests ed eseguire:

```powershell
dotnet run --project .\tests\ProcessLens.BehaviorTests\ProcessLens.BehaviorTests.csproj -c Release
```

Se exit code 0, rieseguire `AVVIA-TUTTO.cmd` per completare la sequenza interrotta; la verifica completa senza avviare la GUI è `verify-candidate-r7.ps1`. Non serve installare o riavviare Guardian per questo hotfix. La verifica visuale WPF/DPI resta da completare.
