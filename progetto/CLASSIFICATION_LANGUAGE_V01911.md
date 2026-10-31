# ProcessLens v0.19.11 — Linguaggio della classificazione

## Obiettivo
La schermata principale distingue il programma riconosciuto, l’applicazione associata,
la funzione classificata, l'elemento utilizzato e i limiti dell'inferenza.
I punteggi euristici, ID interni `knowledge:` e dettagli grezzi sono conservati
nel rapporto diagnostico espandibile ma non compaiono nella sintesi di lettura.

## Modifiche
- `MainWindow.xaml`: sotto «Elementi di identificazione», nuova sezione «Come è stato riconosciuto»,
  con coppie etichetta/valore distanziate. Il rapporto diagnostico originale resta disponibile.
- `ProcessDetailPresentation.cs`: formatter puramente presentazionale e sicuro rispetto agli argomenti
  della command line; la funzione sconosciuta non viene inventata.
- `role-signatures.json`: descrizioni Chromium meno artificiose, conservate le condizioni delle regole.
- `IdentityResolver`: evita il generico «Sistema operativo Microsoft Windows» quando è disponibile
  la descrizione specifica dell'eseguibile.
- Test per riepilogo classificazione, privacy argomenti e nome PowerShell.

## Confini
Guardian, database SQLite, MemoryAgent e timer di scansione non sono modificati.
Non viene eseguita alcuna profilazione. Nessuna installazione automatica aggiunta.
La compilazione WPF reale deve essere effettuata su Windows tramite `AVVIA-TUTTO.cmd`.
