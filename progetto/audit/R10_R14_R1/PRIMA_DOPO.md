# R14 R1 — InvalidDataException nel registro dispositivi

Prima: `Load()` generava InvalidDataException per un file oltre 524.288 byte o un catalogo con struttura non valida, ma il filtro catch non gestiva quel tipo. Il test interrompeva il build.

Dopo: lo stesso errore produce RecognitionCatalog non disponibile, con motivo esplicito e nessun dispositivo riconosciuto. Save() conserva il blocco già esistente: un file non leggibile o non valido non viene sovrascritto. Nessun limite o criterio di validazione è stato allentato.

Il delta di produzione è una sola aggiunta al filtro catch di NetworkDevices.cs, più l'identificativo della candidata nel progetto App. Le altre funzioni della R14 sono conservate.

Test reali: 81 asserzioni NetworkDeviceTests e 22 asserzioni portabili SystemProcessTests superate su Linux x64, compilando con Roslyn e runtime .NET 8. La suite aggiornata fallisce con l'eccezione originale se eseguita con il Core originale R14. PacketCaptureTests e WiresharkTools compilati; esecuzione non effettuata.

Il normale comando SDK dotnet build non si avvia in questo ambiente per le informazioni di processo esposte dall'host; è stata usata compilazione diretta con gli stessi sorgenti e riferimenti net8.0, con GlobalUsings equivalenti a quelli del progetto. Non è una qualificazione MSBuild, PowerShell o WPF. Nessuna lettura Windows reale, cattura rete o modifica del sistema è stata eseguita. I log e gli hash dei sorgenti compilati sono in questa directory.
