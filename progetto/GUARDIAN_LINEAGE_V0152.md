# ProcessLens v0.15.2 — Guardian Parent-Child Lineage Explorer

## Cosa cambia

La nuova scheda **Guardian > Esplora parentela** permette di consultare uno specifico PID senza bloccare l'interfaccia WPF e senza interrompere il servizio Windows. Usa lo storico già conservato in `C:\ProgramData\ProcessLens\Guardian\guardian.sqlite3`.

- **Avvia un'indagine** digitando il PID oppure facendo doppio clic su un generatore nella scheda **Attività e processi padre**.
- **Ricostruisci gli antenati** (massimo sei livelli), con nome, PID e descrizione del limite probatorio.
- **Esamina i figli diretti** osservati nella finestra selezionata (massimo 200 per lettura; il numero totale della finestra è indicato).
- **Naviga sui figli** con doppio clic oppure con il pulsante *Esplora figlio selezionato*; la ricerca è ancorata all'ID dell'evento storico di avvio, evitando di passare a un nuovo processo che nel frattempo abbia ereditato lo stesso PID.
- **Limita le query**: finestre da 5 a 60 minuti nella GUI, time-out breve, lettura manuale per il dettaglio (il riepilogo continua ad aggiornarsi ogni 10 secondi).

## Significato dell'evidenza

- **Compatibile**: il PPID del figlio coincide con il PID del genitore e gli orari noti sono coerenti. Non è una dimostrazione assoluta: i PID sono riutilizzabili, alcuni eventi possono mancare e il Collector memorizza timestamp a precisione di un secondo.
- **Ambigua**: confini nello stesso secondo o cicli/riutilizzi non sufficientemente distinguibili.
- **Non verificabile**: mancano nello storico avvio o identità del presunto genitore, oppure l'ultimo evento disponibile ne documenta l'uscita.

Il programma non acquisisce command line, directory Git, nomi utenti, contenuto file o altri dati non precedentemente raccolti. Il servizio Guardian v0.14.1 NON viene modificato.

La scheda mostra per impostazione predefinita **l'ultimo avvio osservato** del PID digitato. Se il PID è stato riutilizzato, l'analisi non ingloba figli appartenenti a una sua vecchia istanza: il limite di un'istanza è il primo evento di uscita o il primo nuovo avvio con lo stesso PID. Usare il doppio clic su un figlio permette di selezionare un'istanza precisa tramite l'ID evento.

## Compilazione su Windows

Dal percorso estratto `ProcessLens-v0.15.2`:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build-dashboard.ps1
.\run-release.ps1
```

Lo script di build verifica i collegamenti XAML, esegue i test SQLite di regression e compila la GUI. Non installa né aggiorna alcun servizio.

## Limiti intenzionali

L'attendibilità di una genealogia dipende dalla cronologia ETW che Guardian ha effettivamente acquisito. Il servizio può essere stato avviato dopo la nascita del processo padre, oppure lo storico può essere stato soggetto alla retention. L'analisi non equivale a un tracciamento kernel verificato di **Process Unique Key**. Per raggiungere attribuzioni più forti in futuro servirà una nuova versione del Collector con una chiave di istanza stabile e maggiore precisione temporale, opportunamente progettata e validata.

**Validazione eseguita fuori da Windows**: XML/XAML, schema del progetto, coerenza dei metodi/eventi e scenari SQLite sintetici. Il build effettivo del codice C# WPF richiede .NET 8 su Windows.
