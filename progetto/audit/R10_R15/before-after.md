# R14 R3 → R15

Prima: filtro per qualsiasi percorso nella cartella Windows, elenco per PID, opzione per tutti i programmi e dettagli generici in fondo.

Dopo: catalogo esplicito di 17 famiglie in cinque aree; nome e percorso atteso o nome noto con percorso N/D; una riga per componente e scelta della singola istanza. Funzione da catalogo separata dal campione, dettagli più ampi con divisore, massimo Working Set letto con copertura esplicita. Contatori generali dei processi nascosti in questa sezione.

Fonte Windows, API, Guardian, profiler, classificazione generale e query invariati. Nessun evento aggiuntivo o nuovo timer. La GUI conserva solo la selezione del catalogo, senza storico.

Sono intenzionalmente sostituite le asserzioni UI che richiedevano “tutti i processi” e una riga per PID: quel comportamento è rimosso su richiesta dell'utente. Restano contesto Dispatcher, annullamento, coalescenza, errori e verifica dell'identità. Nuova suite indipendente .NET per il catalogo e regressioni UI per categorie e scelta del PID.

Il test sintetico non misura la RAM della GUI; nessun risultato WPF o DPI dichiarato.
