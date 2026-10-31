# ProcessLens v0.9.4 — Safe Process Termination

- Tasto destro su riga processo -> **Termina processo…**.
- Il PID e la riga vengono acquisiti al momento del click, senza usare selezioni successive.
- Verifica preliminare: PID > 4, diverso dal PID ProcessLens, nessun servizio Windows associato,
  nessun nome di processo essenziale, flag critico non impostato, StartTime e ExecutablePath accessibili.
- Dialogo di conferma con nome, PID, data di avvio e rischio di perdita dati.
- WindowsProcessTerminator apre **un singolo handle** al PID con QUERY_LIMITED_INFORMATION,
  TERMINATE e SYNCHRONIZE. Con lo stesso handle controlla ora di creazione, percorso completo,
  nome essenziale e `IsProcessCritical` (se la query fallisce, annulla).
- Poi chiama `TerminateProcess`; attende max 2 s la chiusura; aggiorna la lista.
- Accesso negato, processo terminato, PID riutilizzato o verifica impossibile sono errori gestiti.
- Nessun processo figlio viene terminato; nessuna elevazione automatica; nessuna installazione service/helper.

## Limiti

La terminazione forzata può perdere dati o interrompere applicazioni e servizi. Un processo
che non rientra nelle categorie protette non è automaticamente sicuro. Alcuni processi
protetti non possono essere terminati senza autorizzazioni elevate e non verranno terminati.

## Test

`./test-termination.ps1` esegue test deterministici della policy senza terminare processi reali.
Il controllo della terminazione nativa richiede successiva validazione manuale su Windows:
selezionare un processo di test sacrificabile (per esempio Notepad con file vuoto),
controllare che Termina processo rimuova il PID corretto e che non terminino gli altri processi.
Non testare su componenti del sistema o su sessioni con lavoro non salvato.
