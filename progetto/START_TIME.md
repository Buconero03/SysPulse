# ProcessLens v0.8.1 — orario di avvio

- Ogni riga mostra `Avvio: GG/MM/AAAA HH:mm:ss` e `Attivo da ...`.
- Nei dettagli, sopra le altre schede, è presente una scheda dedicata con data/ora e durata.
- La durata viene ricalcolata al refresh dell'interfaccia (2 secondi), usando la differenza UTC in modo da evitare salti dell'ora legale.
- Nella catena di avvio vengono mostrati anche i timestamp dei processi antenati, quando Windows li rende disponibili.
- Se Windows non concede l'accesso all'orario di avvio per un processo protetto, compare `Non disponibile`, mai un valore inventato.
- Gli orari provengono da `System.Diagnostics.Process.StartTime`, già raccolti dal motore Windows; non viene aggiunto nuovo polling.

## Prova su Windows

````powershell
.\bootstrap.ps1
.\test-origin.ps1
````

Controlla processi appena avviati e processi in esecuzione da ore. Conferma che il valore `Attivo da` avanzi durante gli aggiornamenti, mentre il timestamp di avvio resti fisso.

Non è una cronologia retroattiva per processi già terminati. Quella richiede acquisizione preventiva degli eventi.
