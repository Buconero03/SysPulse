# ProcessLens v0.19.16 R10 R7 — candidata CPU, risorse GUI e riapertura

Prosegue dalla candidata R10 R6. Le revisioni precedenti, la copia dei log e la loro impaginazione restano incluse. Questa revisione risponde alla riapertura intermittente dall’icona, alla CPU poco aggiornata e alla richiesta di ridurre/misurare la RAM della GUI. Non ripete la revisione semantica già registrata.

**Stato: candidata da qualificare su Windows.** In questo ambiente Linux non sono disponibili SDK .NET, PowerShell o WPF. Compilazione, test C#, script PowerShell, riapertura reale, DPI e confronto con Gestione attività: **NON ESEGUITI**. Consumo RAM e risparmio in MiB: **NON MISURATI**. I controlli Python, le mutazioni dei controlli di sorgente e la riproduzione esatta delle patch non sostituiscono queste prove.

## Prima / dopo

| Area | Prima, R10 R6 | R10 R7 |
|---|---|---|
| X della finestra | Nascondeva la finestra se l’icona risultava registrata | Riduce nella barra applicazioni, lasciando un secondo accesso anche se la tray perde l’icona o i clic |
| Menu dell’icona | Apri ProcessLens | **Mostra ProcessLens** e **Esci da ProcessLens** |
| Presenza dell’icona | Flag locale finché non arrivava TaskbarCreated | Prima di ridurre verifica anche NIM_MODIFY; se l’icona è persa prova a registrarla di nuovo |
| CPU visibile | Campionata insieme all’intera raccolta, ogni 15 s o 45 s sotto carico | Lettura CPU separata ogni circa **2 s**, con intervallo effettivo misurato |
| Denominatore per PID nella GUI | Delta sistema condiviso fra letture di processi eseguite in momenti diversi | Delta tempo CPU dello stesso processo / intervallo monotono di quel PID / numero di CPU logiche attive del computer |
| Lettura interrotta o PID riusato | Possibile confronto tra intervalli o istanze non equivalenti | Nuova baseline; fino a due letture confrontabili mostra N/D, senza inventare zero |
| Media CPU | Campioni legati al lento refresh completo | Media degli intervalli CPU disponibili fino a 30 s; pesi limitati alla porzione nella finestra, massimo 64 intervalli |
| Aggiornamento rapido | Avrebbe richiesto ricostruire tutta la lista per aumentare la frequenza | Notifica i soli valori CPU sulle righe esistenti; dettagli CPU aggiornati senza rilanciare ispezione o rapporto |
| Cache metadati dei file | Dizionario senza limite fra percorsi incontrati durante tutta la sessione | Massimo **512** percorsi; un dato espulso viene riletto quando serve |
| Risorse della GUI | Nessun riepilogo proprio sempre visibile | Riga **GUI ProcessLens**, PID, CPU, RAM privata residente e totale, con **Copia risorse GUI**; commit privato e istante UTC nel testo copiato/tooltip |

La CPU rapida e i dashboard GUI sono sospesi quando la finestra è ridotta; i buffer CPU vengono rilasciati appena il worker in corso è concluso. Al ripristino la baseline CPU riparte; nessuna media su un lungo periodo nascosto viene presentata come attività attuale. Gli esiti arrivati dopo una transizione di visibilità vengono scartati. I campioni per PID hanno anche una verifica di età prima della pubblicazione. La diagnostica testuale completa è etichettata come snapshot dell’ultimo aggiornamento dei dettagli; i valori CPU continui sono nel riquadro Analisi CPU. La lista dei processi e i metadati continuano a essere rilevati ogni 15/45 s: un nuovo processo compare al successivo rilevamento completo. L’ordinamento CPU dei processi già elencati viene aggiornato con la lettura rapida, conservando gli oggetti delle righe.

## Che cosa misura la CPU

Per ogni PID leggibile si usano `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` e `GetProcessTimes`: creazione, tempo kernel e tempo utente appartengono allo stesso handle, chiuso subito dopo. L’intervallo proviene da `Stopwatch`, vicino alla lettura di quel PID, e non dall’orologio civile o dall’avvio della scansione completa. La percentuale è:

`100 × Δ(kernel + user in unità da 100 ns) / (10.000.000 × secondi trascorsi × CPU logiche attive)`.

Una CPU logica occupata per tutto l’intervallo su 12 CPU dà circa 8,33%; due danno circa 16,67%. Lo zero è valido soltanto fra due contatori riusciti e confrontabili. Cambiamento della creazione o del numero di CPU, contatore decrescente, intervallo nullo/inverso o superiore a 6 s invalidano la continuità. Una lettura fallita cancella la baseline di quel PID. La pubblicazione nella GUI richiede anche la creazione nota e uguale a quella della riga; l’assenza di identità non autorizza un’associazione.

Il totale sistema usa il tempo occupato di `GetSystemTimes`, normalizzato sull’intervallo monotono e sulle CPU logiche. Oltre 64 CPU logiche questo totale è **N/D**, perché l’API copre il solo gruppo del thread chiamante; i processi possono continuare a essere misurati rispetto al totale delle CPU attive. L’acquisizione completa precedente e i suoi contratti restano presenti per i consumatori esistenti; **la GUI non ne riutilizza i vecchi valori CPU** quando ricarica i metadati.

Non è promessa un’uguaglianza istantanea con un’altra finestra: le finestre di campionamento, gli arrotondamenti e l’aggregazione possono differire. Questo non giustifica errori di calcolo: vanno confrontati PID, creazione e contatore equivalenti. Microsoft documenta che Windows 11 recente usa metriche standard nelle varie pagine di Gestione attività e conserva la precedente metrica nella colonna facoltativa **CPU Utility**. Non si assume che tutte le versioni di Windows usino ancora la vecchia metrica di utilità. Registrare versione/build Windows e colonna osservata.

## RAM: cosa si può affermare

La cache ora ha un limite verificabile nel codice; i test di churn controllano che non superi 512 voci. I cicli rapidi CPU evitano di ricostruire righe, classificazioni, controlli del rapporto e ispezioni profonde. Rimangono i limiti di cache, la virtualizzazione e la costruzione differita dei dashboard delle revisioni precedenti. Non sono stati introdotti GC forzati o svuotamenti del working set.

**Non è dimostrato un risparmio di 10, 30 o 50 MiB**, né che la nuova frequenza CPU abbia un costo trascurabile sul computer dell’utente. La misura deve verificare sia CPU della GUI sia RAM a parità di scenario. Il limite riguarda il numero di percorsi, non un tetto in byte all’intera applicazione; con oltre 512 eseguibili distinti la ricarica dei metadati può aumentare il lavoro e va misurata.

La nuova riga distingue RAM privata residente, working set totale e commit privato. Il totale residente comprende pagine condivisibili; il commit non è RAM fisica. I contatori riguardano **solo il PID della GUI**, non la somma di Guardian e MemoryAgent. `PrivateWorkingSetSize` è letto con EX2; su sistemi senza estensione valida la sentinella evita di mostrare un falso zero e la RAM privata resta N/D. Nessuna differenza fra questi contatori viene chiamata consumo di oggetti .NET.

## Verifica su Windows

1. Per sostituire i binari, uscire dalla GUI precedente con **Esci da ProcessLens** nel menu dell’icona. La X riduce: non termina. Nessun processo viene chiuso automaticamente dagli script.
2. Nella cartella estratta eseguire `powershell -NoProfile -File .\verify-candidate-r10.ps1` per tutti i test e le build, oppure il consueto `build-dashboard.ps1`, che ora comprende anche `ProcessLens.CpuTests`. Qualsiasi FAIL blocca la qualificazione. La fixture del profiler viene solo compilata nella verifica completa.
3. Dopo esito positivo pubblicare con la procedura esistente e aprire la nuova GUI. Controllare che **Copia risorse GUI** riporti `0.19.16-R10-R7-cpu-restore-candidate`.
4. Provare 20 cicli alternati: X → pulsante nella barra; X → clic sull’icona; X → menu **Mostra ProcessLens**; minimizza → barra; finestra massimizzata → X → Mostra. Controllare selezione e dimensioni. Un secondo avvio deve richiedere il ripristino della stessa istanza. Testare anche un riavvio di Explorer e sospensione/ripresa **solo se avviati manualmente dall’utente**. Nel log diagnostico il ripristino riporta visibilità, stato e risultato di Activate; una richiesta accolta non viene chiamata conferma del primo piano.
5. Selezionare un processo esistente con carico osservabile. Confrontare in Gestione attività **lo stesso PID**, non un gruppo App. Registrare build Windows e CPU standard / CPU Utility. Osservare almeno 60 s e i cambiamenti di carico; confrontare intervalli e medie, non una singola cifra di due aggiornamenti diversi. Controllare l’aggiornamento dei valori anche senza ricostruzione della lista e con ordinamento CPU attivo. Ripetere dopo finestra ridotta per oltre 10 s: N/D iniziale, poi nuova coppia valida, nessun picco dovuto alla pausa.
6. Controllare riduzione/riapertura durante una raccolta completa e durante il campionamento rapido. Campioni tardivi non devono ripubblicare valori della sessione precedente. Provare un PID non leggibile e l’uscita di un processo già aperto dall’utente: N/D, nessuna falsa lettura a zero o eredità da un PID riusato.
7. Verificare log selezionabili, copia dei rapporti, notifiche di uscita, filtri, menu contestuali e dashboard come nelle revisioni precedenti. Controllare 100/125/150/200% DPI, 1180×800 e 950×680 DIP, con culture it-IT/en-US; la riga delle risorse deve andare a capo senza sovrapposizioni.

Per la misura passiva copiare il PID dalla riga delle risorse e, da una seconda PowerShell, usare ad esempio:

```powershell
.\measure-processlens-resources.ps1 -TargetPid 1234 -Label R10R7 -Scenario processes-idle -DurationSeconds 180 -IntervalSeconds 2
```

Sostituire **1234 con il PID reale**. Lo script non avvia processi o profiler, non installa nulla e non modifica memoria. Tiene un handle in lettura alla stessa istanza e verifica la creazione. Salva CSV e JSON in `verification-r10r7`: campioni disponibili, mediana e massimo **campionato**, non picco continuo dell’intera sessione. Il CSV dichiara la cultura della sessione. Le celle mancanti non sono zero.

Se R6 è disponibile e funzionante, misurarla prima nello stesso scenario con `-Label R10R6` usando **lo stesso nuovo script**. Attendere un minuto dall’avvio, mantenere stesse schede aperte, filtri, numero approssimativo di processi e carico esterno. Misurare separatamente lista inattiva, dettaglio aperto, navigazione RAM e finestra ridotta. Ripetere almeno tre volte; confrontare lo stesso contatore. Una RAM privata minore con CPU sensibilmente maggiore non dimostra un miglioramento complessivo. Al momento questi risultati non esistono nel pacchetto.

## Test aggiunti e perimetro

- `CpuTests`: normalizzazione, intervallo effettivo, zero reale, baseline, contatore inverso, clock invalido, riuso PID, pause, cambio CPU logiche, media pesata e bordo dei 30 s, sovrapposizione sicura dei valori sullo snapshot.
- `MemoryEfficiencyTests`: limite della nuova cache su 10.000 percorsi, confronto senza distinzione maiuscole, sostituzione, ricaricamento e capacità non valida. Non è una misura RAM della GUI.
- `CopyUiTests`: confronto del contatore CPU nativo con due letture .NET indipendenti sul solo processo di test, identità reale, PID inesistente, seconda lettura e reset, contatori RAM del processo di test e notifiche delle proprietà della riga. Non avvia/termina processi bersaglio né mostra il monitor; la riapertura reale resta manuale.
- Test di policy: X lascia il pulsante nella barra anche con icona registrata; Esci consente la chiusura. Guardie Python con mutazioni conservano i vincoli di sorgente; non eseguono questi test C#.

Il solo cambiamento a un file Windows preesistente è la dichiarazione della cache metadati, ora limitata. Gli altri file Windows/Core aggiunti contengono il nuovo percorso CPU e la lettura della sola GUI, motivati da questa richiesta e accompagnati dai test indicati. Nessun contratto JSON o ID della knowledge base cambia. Guardian, MemoryAgent, profiler, diagnostica originale e log RAM originali sono conservati. Nessuna nuova terminazione o profilazione automatica.

Restano aperti i rilievi semantici in `audit/R10/review-findings.csv` e il rischio asincrono dei rapporti generali in `audit/R10_R6/new-review-finding.csv`; non sono stati implicitamente risolti dal nuovo campionatore CPU. L’analisi della causa intermittente originale di mancato ripristino non è riprodotta qui: la seconda via tramite taskbar elimina la dipendenza esclusiva dalla tray, ma l’esito sul sistema dell’utente va provato.

## Fonti Microsoft consultate il 18 ottobre 2026

- GetProcessTimes, unità e diritti: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes
- CPU logiche su tutti i gruppi: https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getactiveprocessorcount
- Limite di gruppo GetSystemTimes: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes
- Gestione attività attuale e CPU Utility: https://learn.microsoft.com/en-us/windows/whats-new/whats-new-windows-11-version-25h2 e https://support.microsoft.com/en-us/help/5053656
- Contatori e supporto EX2: https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2
- GetProcessMemoryInfo: https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo
