# ProcessLens R32 — area di notifica, icone e testi

Base: R31 R1, SHA-256 79a6b2db701b48a80a168bf26fdbee83c86c82c32494cf25dd4701c6a8adc642.

## Cosa cambia

Il pulsante in alto ora dichiara «Nascondi accanto all’orologio» e usa Hide, separato da WindowState.Minimized. La riduzione nativa e la X mantengono il percorso nella barra applicazioni. Il precedente log provava la riduzione, non il nascondimento nell’area di notifica: non dimostrava il guasto del clic fisico.

Prima di nascondere viene verificata la registrazione Shell. In caso di fallimento non si perde la finestra. Log e altre finestre WPF possedute visibili sono nascosti e ripristinati con il loro stato; finestre modali bloccano il nascondimento. Ultimo comando in coda prevalente; recupero dopo riavvio Explorer invariato. Nessuna scrittura nelle impostazioni della taskbar. Windows decide se l’icona è visibile o sotto ^; l’utente può trascinarla.

ProcessLens ha una piccola icona propria, incorporata nell’eseguibile/WPF e copiata per il tray. Il relativo HICON viene rilasciato all’uscita. Le icone dei processi sono cercate prima nelle risorse del file e poi in Shell. Cache originali 128, segnaposto 128; grafica congelata e condivisa. Nessuna acquisizione aggiuntiva di processi, rete o immagini dal web. Le icone mancanti usano iniziali e colore deterministici, esplicitamente etichettati come segnaposto. Il simbolo Windows condiviso è limitato ai PID riservati e a sette componenti di base, con i precedenti controlli di percorso. Nessuna icona verifica la firma del file.

Titoli dell’analisi più brevi: Identità e classificazione, Dati Windows, Associazione all’app, Ruolo, Comando di avvio, Attività rilevata. Rimossi il prefisso ambiguo «Windows:» davanti a qualunque eseguibile e formule burocratiche; mantenuti il ruolo stimato, l’incertezza di associazione e il limite sull’attività corrente. Layout, classificatori, contatori CPU/RAM, porte, filtri, Guardian e acquisizioni non sono riscritti.

## Verifiche eseguite

- 13 progetti reali compilati con SDK/MSBuild; 834 asserzioni eseguite, tutte PASS.
- 40 controlli PowerShell e 17 prove del launcher con file sintetici.
- 9 assembly C# verificati con riferimenti WindowsDesktop e campi XAML simulati. Questo NON è compilazione markup/BAML o esecuzione WPF.
- Fixture Windows aggiornata: nascondimento dal pulsante, callback tray, log e seconda finestra posseduta, ripristino massimizzato e comandi nativi separati. Inclusa nella build Windows; NON eseguita qui.
- Confronto con archivio base, XML, patch applicabile, hash e integrità ZIP.

## Verifica necessaria su Windows

1. Esci dalla versione precedente e avvia AVVIA_PROCESSLENS.cmd dalla nuova cartella.
2. Controlla la versione R32 nel Log finestra. Apri il log, quindi Nascondi accanto all’orologio: finestra principale e log devono scomparire, l’icona blu restare anche sotto ^.
3. Clic sull’icona: mostra. Secondo clic separato: nasconde. Tasto destro: Mostra ProcessLens. Verifica anche dopo apertura di un altro dettaglio e con finestra massimizzata.
4. Trattino e X: riduzione nella barra applicazioni. Esci: chiusura effettiva e rimozione icona.
5. Verifica icone originali su app installate e segnaposto distinti dove mancano le risorse. Controlla leggibilità a 100/125/150/200% DPI.

Non sono misurati RAM, latenza, DPI né il risultato del clic fisico su questo host Linux. La candidata non è una conferma del comportamento sul PC dell’utente.

## Fonti Microsoft

https://learn.microsoft.com/en-us/windows/win32/shell/notification-area
https://support.microsoft.com/it-it/windows/experience/personalization/customize-the-taskbar-in-windows
