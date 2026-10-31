# ProcessLens v0.19.12 R1 - Fix CS9035 nei test di terminologia

## Errore osservato sul Windows dell'utente

Durante `AVVIA-TUTTO.cmd`, in fase "Test terminologia e confini tra regole e osservazioni",
la compilazione `tests/ProcessLens.TerminologyTests/Program.cs` e terminata
con due errori C# `CS9035` alle linee 92 e 97: il record `ProcessSnapshot`
richiede `Pid`, `ParentPid` e `Name`, ma due fixture indicavano soltanto
`Pid` e `Name` (oltre ad altre proprieta facoltative).

## Correzione

- Test unknown.exe: aggiunta `ParentPid = 0`.
- Test powershell.exe: aggiunta `ParentPid = 0`.
- Il valore 0 e usato per il test come genitore non determinato, senza
  attribuzione inventata a un processo realmente osservato.
- Intestazione `start-processlens-all.ps1` aggiornata a v0.19.12 R1
  per distinguere i pacchetti estratti.
- Tutti i controlli e tutte le asserzioni dei test sono conservati.

## Limiti della verifica

Sono stati verificati in locale: integrita ZIP, presenza delle proprieta
obbligatorie nei due initializer, e parita esatta dei sorgenti applicativi e
script con il pacchetto v0.19.12 (salvo il titolo del launcher).
L'ambiente locale non dispone dell'SDK .NET ne di PowerShell, percio NON
sono ancora stati eseguiti compilazione WPF, suite C# e prova del profiler.

## Procedura Windows

1. Estrarre questo ZIP in una cartella nuova, senza sovrascrivere la candidate.
2. Eseguire `AVVIA-TUTTO.cmd` come di consueto.
3. Attendere la fine dei test e della pubblicazione; se falliscono, inviare
   l'intero log senza saltare i test.
4. Non avviare `verify-dotnet-profiler.ps1 -RunCapture` finche la build non
   sia verde, la RAM sia inferiore all'80% e l'utente non abbia dato il consenso.

Nessuna modifica o reinstallazione di Guardian, MemoryAgent o database SQLite.
