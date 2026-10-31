# ProcessLens v0.19.16 R1 — BehaviorTests TCP LISTEN

## Errore che blocca la build Windows

Nella v0.19.16, `ProcessLens.BehaviorTests` si interrompe alla verifica `network listening attributed directly` (riga 55 di `Program.cs`). La verifica cercava la dicitura precedente `TCP in ascolto`; il motore aggiornato produce `Il PID ha 1 socket TCP nello stato LISTEN (porte: 8765).`.

## Correzione mirata

- Aggiornata l'asserzione per verificare lo stato `LISTEN` e la porta 8765 attribuiti al PID di prova.
- Aggiunta una seconda asserzione per verificare che gli stati TCP non siano spacciati per prova di richieste accettate o byte trasferiti.
- Nessun test disabilitato, nessuna modifica a `ProcessBehaviorExplainer`, GUI, Guardian, MemoryAgent o profiler.
- Intestazione launcher e `VERSION.txt` aggiornati a R1.

## Verifica richiesta su Windows

Estrarre in una nuova cartella ed eseguire `AVVIA-TUTTO.cmd` normalmente. Il comando esegue nuovamente l'intera suite; la compilazione WPF e la pubblicazione non sono ancora confermate in questa candidata.

La verifica statica in questo ambiente non equivale a un'esecuzione .NET/WPF su Windows.
