# R10 R4 — piccolo avviso quando il processo si chiude

Quando Windows segnala l'uscita del processo selezionato, ProcessLens mostra in basso a destra nella propria finestra un avviso: **Il processo esempio.exe (PID 1234) si è chiuso.** Il testo riporta il nome eseguibile dell'istanza osservata. L'avviso dura 8 secondi, rimane selezionabile e non cambia il focus né richiede OK.

La richiesta esplicita Termina processo continua a richiedere la conferma già presente. Per l'esito positivo viene usato lo stesso piccolo avviso al posto del successivo messaggio modale. Un risultato di richiesta accettata senza uscita confermata conserva il messaggio originale sotto Esito richiesta; non viene trasformato in Processo chiuso. Errori e rifiuti restano nei dialoghi esistenti. Le regole e l'esecutore nativo della terminazione restano byte per byte invariati.

## Fonte e identità

La nuova funzionalità introduce una sola osservazione nativa in lettura, confinata nella GUI. L'aggiunta è necessaria per la richiesta dell'utente: l'assenza dall'elenco filtrato o da uno snapshot incompleto non dimostra che un processo sia terminato.

`ProcessExitWatch` apre un handle con `SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION`, confronta esattamente il timestamp di creazione con l'istanza selezionata tramite `GetProcessTimes` e registra un'attesa a segnale Windows, singola e senza timeout. La conferma di uscita deriva dal segnale di quell'handle. PID e nome da soli non bastano. Non vengono richiesti diritti di scrittura o terminazione da questo osservatore.

Si osservano il processo selezionato e, durante una richiesta esplicita, il suo destinatario: al massimo due osservatori. I due segnali della stessa istanza producono un solo avviso. La deduplicazione conserva al massimo 16 identità. Selezione cambiata, dettagli cancellati o finestra chiusa rilasciano gli osservatori pertinenti; non si aggiunge polling su tutti i processi. La chiusura può essere naturale o provocata da un altro programma: l'avviso non ne inventa la causa.

Limiti: se l'orario di creazione non è disponibile o Windows nega l'accesso in lettura/attesa, non viene inventata una conferma automatica. Uscite precedenti all'aggancio, processi non selezionati e applicazione già chiusa non hanno una notifica garantita. Il processo oggetto di una richiesta esplicita resta osservato finché esce, viene sostituito da un'altra richiesta o viene chiusa la finestra. Questa è una notifica interna alla finestra, non una notifica del Centro notifiche Windows. Non viene mantenuto uno storico completo delle uscite.

## Test e continuità

La R3 con gerarchia, titoli a capo e copia è inclusa. Collector, Core, Semantics, Guardian, MemoryAgent, profiler e terminazione nativa sono invariati rispetto alla R3. Non è stato terminato o profilato alcun processo durante questo lavoro.

Le regressioni pure fornite coprono testo dell'avviso, richiesta non confermata, identità mancante, duplicati, PID riutilizzato e storico limitato. La suite Windows include una verifica indipendente e in sola lettura sul proprio processo di test: aggancio al timestamp corretto, rifiuto del timestamp errato, rilascio idempotente senza falsa uscita. Non avvia o termina un processo bersaglio. Il test WPF controlla anche presenza, tipo selezionabile e stato inizialmente nascosto dell'avviso.

Eseguiti qui: controlli strutturali Python/XML, regressioni portabili preesistenti, patch replay reale, confronto byte/SHA-256, ZIP e manifest. **NON ESEGUITO:** test .NET/WPF inclusa la verifica nativa appena descritta, PowerShell, build/pubblicazione, scenario reale di uscita e DPI. Ambiente Linux senza dotnet/pwsh. Nessuna riduzione o misura RAM dichiarata.

Su Windows, estrarre in una cartella nuova e dalla radice con `src` e `tests`:

```powershell
.\build-dashboard.ps1
```

Solo se termina con successo:

```powershell
.\run-release.ps1
```

Verifica manuale proposta: selezionare un'applicazione ordinaria già aperta e chiuderla normalmente; controllare nome/PID e scomparsa dell'avviso dopo 8 secondi. Cambiare soltanto filtro o ricerca non deve produrre Processo chiuso. Se si prova Termina processo, usare un bersaglio senza lavoro da salvare e la conferma già esistente; l'esito non deve richiedere un secondo OK. La sola suite automatica non effettua questa terminazione.

Audit `audit/R10_R4/`, rapporto prima/dopo e registro stringhe inclusi; manifest corrente `SHA256SUMS_R10.txt`. Le criticità RAM già documentate nella R10 restano aperte. Unica radice del progetto conservata.

Fonti primarie:

- https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool.registerwaitforsingleobject
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.registeredwaithandle.unregister
