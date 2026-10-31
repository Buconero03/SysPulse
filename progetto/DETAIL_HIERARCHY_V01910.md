# ProcessLens v0.19.10: gerarchia dei dettagli

## Obiettivo
La schermata del processo selezionato conserva sintesi, contatori e spiegazioni concise.
Il contenuto esteso e raggruppato in sezioni con navigazione progressiva.

## Sezioni
- Identita e ambiente di esecuzione: metadati del PID e classificazione.
- CPU e memoria: contatori e campioni; non mescolare virtuale e residente.
- Rete, servizi e relazioni: osservazioni espresse separatamente.
- Percorso e parametri: presentazione per riga dei flag `--`; comando originale sempre disponibile.
- Evidenze: identita, funzione, origine e altre osservazioni.
- Diagnostica completa: originale in sola lettura per controllare la fedelta del riepilogo.

I gruppi sono una scelta di presentazione per prefisso, non nuove inferenze. Le prove non sono riscritte.
Il formatter NON e un parser Windows completo: visualizza un'interruzione di riga prima di flag `--`
fuori da virgolette semplici. Per l'esatto valore della command line consultare sempre il testo originale.
Non esportare automaticamente righe di comando o evidenze in log.

## Test
Eseguire `AVVIA-TUTTO.cmd` da estrazione pulita; controllare la riga
`PASS Detail UI v0.19.10: ...`; poi tutti i test Behavior/Terminology e la build WPF.
Aprire un processo Chrome con molti parametri di avvio e verificare la gerarchia.
Ridimensionare la GUI con 125-150% DPI e provare le sezioni espandibili.
Misurare l'overhead sulla vista aperta/chiusa e confrontarlo con v0.19.9.

## Stato
Controlli statici locali non equivalgono a una build Windows completata.
Guardian, MemoryAgent, SQLite e profiler non vengono modificati.
