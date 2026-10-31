# R15 — componenti di Windows, organizzati per funzione

La schermata precedente mostrava centinaia di processi perché includeva ogni percorso sotto Windows e offriva anche il filtro “Tutti i processi”. Era un altro inventario generale. La nuova vista usa un catalogo mirato di 17 famiglie, raggruppa le istanze e mostra subito la funzione del componente.

## Cosa cambia

- Cinque aree: kernel e memoria; avvio e sessioni; servizi; sicurezza; desktop e console.
- Una riga per famiglia: ad esempio 80 istanze di svchost.exe diventano una riga con 80 istanze, tutte selezionabili nel menu dei PID.
- I programmi comuni restano nella scheda Processi. Un eseguibile nella cartella Windows non basta più a entrare in questa vista.
- Percorso previsto e nome devono corrispondere. Se il percorso non è leggibile, un nome noto resta visibile con indicazione N/D; non equivale a identità autenticata.
- Dettagli selezionabili: funzione da catalogo, spiegazione tecnica, PID, sessione, thread, handle, Working Set, percorso, avvio e stato della lettura.
- Divisore trascinabile tra elenco e dettagli. I vecchi contatori “Rilevati / Mostrati” della scheda Processi sono nascosti in questa sezione.
- Il pulsante di analisi usa il PID scelto e mantiene il controllo di nome e ora di creazione già presente. Una voce protetta senza ora di avvio ha comunque la scheda del componente consultabile.

## Catalogo e copertura

System Idle Process, System, Memory Compression, Registry, Secure System, smss.exe, csrss.exe, wininit.exe, winlogon.exe, LogonUI.exe, services.exe, svchost.exe, lsass.exe, LsaIso.exe, dwm.exe, explorer.exe e conhost.exe.

È un catalogo di navigazione mirato, **non un censimento completo di ogni componente Windows**. Componenti non ancora descritti, servizi in eseguibili propri e driver non diventano automaticamente righe. Non si verifica la firma digitale. Assenza di una voce dal campione non dimostra che la funzione sia disattivata. La funzione descritta non dimostra attività in corso.

Questa vista legge un campione manuale. **Non cattura eventi**. Per avvii e uscite usare Guardian, che resta invariato. La lettura non ricostruisce tutte le chiamate kernel o le singole operazioni del registro, né accede a memoria protetta o credenziali.

## RAM e costo operativo

Nessun nuovo processo residente, timer, sottoscrizione ETW o storico. La fonte nativa rimane byte per byte quella della R14 R3. La lettura temporanea resta generale, con i limiti esistenti: al massimo 16 MiB per buffer nativo e 16.384 righe; questi limiti **non sono una previsione del consumo complessivo della GUI**. Solo le righe corrispondenti al catalogo vengono restituite alla vista. Alla chiusura della sezione vengono rilasciati catalogo, gruppi e lista delle istanze; il momento della raccolta della memoria gestita dipende dal runtime.

La tabella contiene al massimo 17 gruppi, invece di una riga per processo. Nel test sintetico, 5.000 programmi comuni sono esclusi e 200 host di servizi diventano una riga, con tutti i PID conservati. Non è una misura del Working Set di ProcessLens.

“WS max letto” indica il maggiore Working Set disponibile di una singola istanza. Include pagine private e condivise. I valori mancanti restano N/D e il numero dei contatori disponibili è indicato nei dettagli. Non si sommano i Working Set per stimare la RAM fisica del gruppo.

**Il delta reale di RAM e CPU su Windows non è stato misurato qui.** Per confrontarlo: avviare R14 R3 e R15 separatamente, a parità di programmi; attendere il campione della GUI, annotare Working Set privato e intervallo; aprire Sistema Windows, aggiornare e selezionare svchost.exe; annotare di nuovo; uscire dalla sezione e verificare che gli aggiornamenti restino manuali. Conservare le misure del monitor RAM esistente, senza sommare contatori diversi.

## Verifiche e uso

Estrarre lo ZIP completo in una cartella nuova ed eseguire da PowerShell:

```powershell
.\build-dashboard.ps1
```

Su Windows provare anche:

1. Nessuna lettura alla sola apertura della scheda; premere Aggiorna campione.
2. Nessun programma comune, anche se copiato sotto Windows; una riga per tipo di componente.
3. Filtro per area e ricerca PID esatta, senza corrispondenze per sottostringa.
4. Scelta fra più PID di svchost.exe e apertura dell'analisi della stessa istanza.
5. Dettagli leggibili per Memory Compression o Registry quando il percorso è N/D.
6. Cambio sezione durante una lettura: nessun dato tardivo ripubblicato.
7. Copia del rapporto, divisore trascinabile, dimensioni 100%, 125%, 150% e 200% DPI.

Eseguiti qui: compilazione diretta Roslyn di Core e tre suite .NET; **58 test componenti, 22 test portabili inventario e 81 test registro rete passati**. Il ramo di lettura reale Windows è saltato su Linux. Controlli aggiuntivi su sorgenti, XML, conservazione delle correzioni e sintassi C# registrati in `audit/R10_R15`.

**Build completa MSBuild/WPF, PowerShell, test UI, DPI, letture reali Windows e consumo della GUI NON ESEGUITI su questo host Linux.** La compilazione portabile non li sostituisce. Il pacchetto è una candidata.

## Riferimenti tecnici

Le descrizioni indicano funzioni generali, separate dai dati misurati. Nessuna richiesta a queste pagine avviene durante l'uso dell'app.

- [Microsoft — componenti di sistema critici](https://learn.microsoft.com/en-us/windows/win32/rstmgr/critical-system-services)
- [Microsoft — Service Control Manager](https://learn.microsoft.com/en-us/windows/win32/services/service-control-manager)
- [Microsoft — processi di autenticazione](https://learn.microsoft.com/en-us/windows-server/security/windows-authentication/credentials-processes-in-windows-authentication)
- [Microsoft — Credential Guard](https://learn.microsoft.com/en-us/windows/security/identity-protection/credential-guard/how-it-works)
- [Microsoft — console, terminale e host](https://learn.microsoft.com/en-us/windows/console/definitions)
- [Microsoft — Desktop Window Manager](https://learn.microsoft.com/en-us/windows/win32/dwm/dwm-overview) — usato per il principio di composizione, non per le istruzioni obsolete di disattivazione del servizio.
- [Microsoft — Shell Windows](https://learn.microsoft.com/en-us/windows/win32/shell/shell-entry)
- [Microsoft — compressione della memoria](https://learn.microsoft.com/en-us/shows/seth-juarez/memory-compression-in-windows-10-rtm) — descrizione del meccanismo; presentazione delle voci variabile fra versioni.
- [Microsoft — avvio di sessioni e processi iniziali](https://techcommunity.microsoft.com/blog/askperf/ws2008-startup-processes-and-delayed-automatic-start/372692) — contesto storico; nessuna dipendenza da PID fissi o ordine osservato.
