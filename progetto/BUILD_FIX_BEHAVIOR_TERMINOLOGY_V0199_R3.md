# ProcessLens v0.19.9 R3 — BehaviorTests: test aggiornati al contratto terminologico

## Errore riprodotto dal log Windows

La versione v0.19.9 R2 supera il controllo di 39 script PowerShell e i test RAM,
ma si arresta al test `ProcessLens.BehaviorTests` alla riga 18 con
`FAIL UI text: clear identity evidence`.

## Causa precisa

Il test della R2 cercava nel campo `ProcessInsight.DetectionBasis` le vecchie
etichette `Identità del programma:` e `Funzione dedotta:`.
L'analizzatore v0.19.9 usa invece una sintesi concisa, composta da
`Attendibilità della classificazione:`, `Fonte:` e avvertenza sull'attività corrente.
L'identità, il ruolo e le altre prove non sono stati eliminati: sono disponibili
in `ProcessInsight.Evidence` per i dettagli tecnici.

## Correzione R3

Solo `tests/ProcessLens.BehaviorTests/Program.cs` è cambiato nel codice:
- il test verifica identità e ruolo nella collezione delle evidenze;
- verifica attendibilità, fonte e limite sull'attività nella sintesi;
- verifica l'assenza di percentuali pseudo-probabilistiche e identificatori interni
  nella sintesi;
- mantiene tutti gli altri test, incluso il controllo sui segreti dei comandi.

Tutti gli script PowerShell, i launcher CMD, i sorgenti Guardian, il database,
MemoryAgent, GUI, Core, Semantics e Profiler sono **identici alla R2**.
Non è stato disabilitato alcun test.

## Verifica su Windows (necessaria)

Estrarre lo ZIP completo in una nuova cartella e avviare `AVVIA-TUTTO.cmd`.
Gli esiti ottenuti in precedenza sulla R2 non certificano la build completa R3:
raccogliere l'output integrale di `BehaviorTests`, `TerminologyTests`,
`RefreshPolicyTests`, `ProfilerTests`, build e publish WPF.

Nessun intervento su Guardian o profilazione profonda è richiesto.
