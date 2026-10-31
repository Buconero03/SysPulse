# Controlli della candidata v0.19.7

## Verificati in questo ambiente

- Integrita' sorgenti e successivo archivio ZIP
- Parsing XML di tutti i file XAML del progetto
- Parsing JSON di tutti i file JSON del progetto
- Controllo unicita' x:Name in tutte le viste
- Risoluzione nominale degli handler dichiarati nel MainWindow.xaml
- Etichette presenti in XAML, test esistenti aggiornati, nuovi test .NET collegati alla build
- Hash e confronto byte per byte dei sorgenti Guardian Service, Guardian Core e MemoryAgent rispetto alla v0.19.6
- Launcher AVVIA-TUTTO.cmd e AVVIA-VELOCE.cmd invariati

## NON verificati in questo ambiente

- Parser PowerShell 5.1/7 (non disponibile qui; preflight incluso nello script Windows)
- Compilazione .NET 8 / WPF Windows e test C# effettivamente eseguiti
- Risultato grafico su Windows, PID reale, profilazione heap reale

Eseguire `AVVIA-TUTTO.cmd` non come amministratore nella cartella estratta.
Se un test fallisce, la sequenza si interrompe: fornire il log completo.
