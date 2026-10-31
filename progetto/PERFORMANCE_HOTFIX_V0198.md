# ProcessLens v0.19.8: mitigazione carico GUI

## Problema segnalato
Il 12 ottobre 2026 l'utente ha osservato un incremento anomalo dell'utilizzo globale di CPU e memoria durante l'uso di ProcessLens v0.19.7. La segnalazione deve essere indagata. Lo screenshot da solo non consente di misurare il carico *indotto* dai processi, quindi non dimostra o esclude un rapporto causale.

## Tracce statiche nel codice originale
* MainWindow aggiorna tutti i processi ogni 2 secondi, con raccolta CPU/working set/commit e informazioni sulle istanze; ricostruisce anche la lista WPF.
* Il catalogo servizi completo viene riletto ogni 20 secondi quando la scheda Servizi è aperta.
* L'associazione processi-servizi viene ricalcolata ogni 10 secondi in WindowsProcessCollector.
* Un aggiornamento in corso evita sovrapposizioni per la stessa vista; questo non elimina il costo dei singoli campionamenti.

## Modifiche mirate
* Scansione generale: 15 secondi di base; 45 secondi se CPU di sistema >=70% oppure RAM fisica utilizzata >=85%. Le soglie sono *protezioni*, non una diagnosi causale.
* Catalogo servizi: rilettura automatica massimo una volta ogni 120 secondi, ma pulsante Aggiorna mantiene la lettura esplicita.
* Associazione SCM ai PID: da ogni 10 a ogni 60 secondi.
* Sospensione del refresh automatico della GUI durante la minimizzazione.
* Log locale rate-limited per scansioni lente (>1 s), con numero di elementi e durata; non memorizza command line o heap.
* Test .NET 8 sulla policy integrati nella build automatica. I launcher CMD sono invariati.

## Limiti e prossima prova
La modifica è preventiva: non identifica ancora in modo certo la sorgente dell'anomalia. Prima di aprire la v0.19.8 misurare il sistema con ProcessLens chiuso. Dopo un ritorno a condizioni ordinarie, provare la v0.19.8 e confrontare CPU/RAM di sistema e CPU/RAM del processo (almeno 60 secondi per condizione). Se persiste un peggioramento interrompere subito il test, non profilare processi critici e condividere i dati. Guardian e MemoryAgent non vengono modificati o reinstallati dal codice.

### Stato delle verifiche
Controlli statici su archivio e sorgente disponibili in ambiente Linux. Non e' stata eseguita una build WPF o un test di carico Windows. Non dichiarare la v0.19.8 validata prima dei log reali dell'utente.
