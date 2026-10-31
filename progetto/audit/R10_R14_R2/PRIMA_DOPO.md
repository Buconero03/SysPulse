# R14 R2 — prima scheda della vista rete

Prima: il TabControl della vista rete non aveva un SelectedIndex esplicito. RefreshAsync scarta le richieste se SelectedIndex è diverso da 0. Il test crea la vista senza mostrarla e senza Loaded: l'asserzione che combinava chiamata, riga e avviso falliva senza riportare i valori dei tre termini.

Dopo: NetworkTabs dichiara SelectedIndex="0". Il test verifica la selezione prima del caricamento e il pulsante abilitato, poi controlla separatamente chiamate, righe e avviso, mantenendo tutte le condizioni originali. Aggiunti casi di cambio scheda prima della visualizzazione e nessuna acquisizione implicita. Corretti i due avvisi nullable nel test tramite cast con controllo esplicito.

Produzione: soltanto NetworkDevicesView.xaml (un attributo) e versione del progetto App. Nessun codice C# di produzione cambia dalla R14 R1. La correzione InvalidDataException rimane byte per byte identica, così come acquisizione, semantica, rete, CPU/RAM, Guardian e profiler.

Il log Windows dell'utente conferma il completamento di NetworkDeviceTests e PacketCaptureTests. Non conferma il resto del build, fermato su NetworkUiTests. Non è stata riprodotta la nuova anomalia WPF qui: host Linux, nessun runtime Windows. Il tentativo di compilazione incrociata tramite MSBuild non arriva alla compilazione, per un errore dell'host sulle informazioni del processo. I controlli correnti sono sui sorgenti/XML e sui loro casi di regressione.
