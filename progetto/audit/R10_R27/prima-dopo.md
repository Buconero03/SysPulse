# R27: icona per i componenti Windows

Base R26: SHA-256 `338a4d2c4871c412daecb86df9c9ff20673d47658ce840562f0f094ae3824b82`.

## Risultato e perimetro

Simbolo Windows blu a quattro riquadri, vettoriale, condiviso fra righe dei processi, intestazioni dei gruppi e dettaglio selezionato. Le altre app continuano a usare il precedente resolver della Shell. La disposizione della schermata è invariata. I due tooltip delle immagini e quello del dettaglio spiegano la classificazione; non introducono badge di sicurezza.

Il disegno è una singola DrawingImage congelata: nessun bitmap per PID, download, file immagine, worker o cache aggiuntiva. La cache esistente delle altre icone resta limitata a 128 voci. Non sono state eseguite misure di costo o risparmio RAM; non si dichiara consumo nullo.

## Criteri

1. PID 0 con nome Idle/System Idle Process e PID 4 con nome System.
2. Nomi del catalogo Windows esistente con percorso atteso, inclusi gli host a 32 bit esplicitamente previsti dal catalogo.
3. Altri componenti, come ShellHost: nome del file coerente con lo snapshot, percorso sotto System32/SysWOW64/SystemApps, azienda dichiarata Microsoft Corporation e prodotto dichiarato Microsoft Windows Operating System, anche nella forma italiana e con marchi ®/™.

Il solo nome, la categoria del classificatore o l'ospitalità di un servizio non bastano. Un'app Microsoft generica non riceve automaticamente il simbolo. Percorsi assenti, esterni, con attraversamento `..`, stream alternativi o caratteri di controllo restano esclusi. Il nome del file deve corrispondere al nome del processo. La directory Windows viene letta una volta dall'ambiente; i test la iniettano come stringa sintetica.

La classificazione non autentica il file. Metadati dichiarati, percorsi lessicali e nomi non verificano firme, collegamenti, attività corrente o sicurezza. Per identità non classificabili resta l'icona precedente. Questa revisione non amplia l'inventario dei processi e non modifica i filtri Windows o il catalogo.

## Verifiche eseguite

- 13 suite portabili, **716 asserzioni**: 55 nuove rispetto alla R26, nel gate ProcessGroupsTests già incluso nella build Windows. Copertura dei componenti del catalogo, PID riservati, installazioni su altra unità, percorsi inattesi, nomi simili, metadati insufficienti, app Microsoft, integrità dello snapshot e semantica del tooltip.
- 2 controlli negativi su copie della produzione: classificazione dal solo nome e promozione indiscriminata dei prodotti Microsoft. Entrambi falliscono sulla regressione prevista.
- 34 fasi PowerShell e 17 fixture del launcher. Ricevuta R27 distinta da R26: una build fallita non può avviare l'eseguibile precedente.
- 14 controlli del guard sulle risorse con relativi casi alterati.
- Controllo C# di 9 assembly con riferimenti WindowsDesktop e stub dei campi XAML, non compilazione del markup o esecuzione WPF. Le nuove asserzioni UI verificano condivisione della stessa immagine fra righe/gruppo, immagine congelata e descrizione coerente. Queste asserzioni WPF sono compilate come C#, **non eseguite qui**.
- XML, scope esatto, patch riapplicata con confronto byte, manifest e ZIP verificati in fase di confezionamento.

Core, classificatore semantico, acquisizioni Windows, Guardian, profiler, MemoryAgent e monitor RAM restano identici alla R26. La logica R26 di scorrimento e riduzione e il precedente resolver Shell sono preservati; il controllo di scope verifica queste condizioni. I vecchi log R26 rimangono storici: i risultati correnti sono soltanto in questa cartella.

## Verifica Windows da eseguire

Aprire R27 con l'unico launcher. In Processi controllare svchost, RuntimeBroker, taskhostw, System e i componenti con metadati OS disponibili; poi confrontare Chrome/AnyDesk/Xbox e altri programmi. Aprire un gruppo e selezionare un PID: simbolo e tooltip devono essere coerenti anche nel dettaglio. I processi con origine non classificabile conservano l'icona precedente. Verificare rendering a 100/150/200% DPI, scorrimento durante i campioni e riduzione/riapertura. Nessuna di queste prove GUI reali è stata eseguita nell'ambiente Linux.

## Valutazione della schermata fornita

Il simbolo comune rende più rapida la distinzione fra componenti Windows e applicazioni, soprattutto nelle intestazioni compatte. La struttura attuale separa bene elenco e analisi. Lo screenshot mostra 370 gruppi per 459 PID, con molti gruppi singoli, e nessun processo selezionato: i numerosi riquadri del dettaglio rimangono quindi vuoti. Un futuro intervento mirato può rendere compatto lo stato senza selezione; non è stato mescolato a questa modifica delle icone.
