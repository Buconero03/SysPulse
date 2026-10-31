# ProcessLens v0.16.1 - Avvio automatico da CMD

## Primo avvio: `AVVIA-TUTTO.cmd`

1. Estrarre l'intero ZIP in una cartella (per esempio `D:\.A2\ProcessLens-v0.16.1`).
2. Aprire la cartella `ProcessLens-v0.16.1` e fare doppio clic su `AVVIA-TUTTO.cmd` **senza Esegui come amministratore**.
3. Si apre una finestra Windows PowerShell che esegue, in sequenza:
   - test e build della dashboard WPF e del MemoryAgent;
   - se non ancora configurato, richiesta esplicita `ATTIVA` per l'agente RAM >=85% / riarmo <80%;
   - verifica, in sola lettura, dello stato del servizio `ProcessLensGuardian` gia installato;
   - avvio della GUI `ProcessLens.exe` (UAC per la sola GUI, se richiesto);
   - riepilogo dello stato dell'agente e del consumo di memoria.
4. PowerShell resta aperto al termine per leggere eventuali errori e risultati. Si puo' chiudere manualmente.

**Non** viene installato, arrestato o aggiornato automaticamente il servizio Guardian. Gli avvisi RAM non vengono configurati senza conferma `ATTIVA`. Nessun altro programma viene chiuso o manipolato.

## Avvii successivi: `AVVIA-VELOCE.cmd`

Avvia la GUI senza ricompilare. Se l'agente RAM e' gia configurato dall'utente e non sta girando, lo avvia dalla cartella `%LOCALAPPDATA%\ProcessLens\MemoryAgent`. Non installa l'agente se non e' stato autorizzato in precedenza.

## Problemi frequenti

- La prima build richiede **.NET 8 SDK** e i componenti Windows necessari per WPF. La GUI pubblicata richiede .NET 8 Desktop Runtime.
- Se si avvia il CMD con privilegi amministrativi, la prima installazione dell'agente RAM viene **saltata** per evitare un agente elevato in background. Riavviare `AVVIA-TUTTO.cmd` normalmente.
- Se UAC viene annullato, la GUI non parte; il servizio Guardian continua comunque a funzionare.
- Se il servizio Guardian e' assente o fermo, il launcher mostra un avviso. Non cerca di installarlo o avviarlo automaticamente.
- Entrambi i CMD usano il percorso della cartella dove si trovano, anche con spazi o `(1)` nel nome, senza `cd` manuali.
- Quando un test o una build falliscono, la sequenza si ferma prima del successivo avvio.

La gestione della memoria degli altri processi resta *osservativa*: classifiche e dati, senza terminazioni o forzature del working set. L'agente non richiede che la GUI rimanga aperta dopo l'installazione.
