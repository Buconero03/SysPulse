# ProcessLens R10 R12 — Dispositivi sulla rete

La candidata riparte dalla R11 R2. Aggiunge **Rete** alla navigazione e una vista dedicata, costruita solo quando viene aperta. Restano incluse le correzioni R11 R1 e R2 ai test memoria e riepilogo audit.

## Prima e dopo

| Prima | R12 |
|---|---|
| Nessuna vista dei vicini della rete locale | Rete → Dispositivi sulla rete |
| Comando esterno da interpretare | Aggiorna elenco, tabella copiabile, fonte e limiti visibili |
| Nessun riconoscimento locale | Nome scelto, È un mio dispositivo / Salva nome, Rimuovi riconoscimento |
| Nessun avviso per nuove voci | Avviso in linea dopo una nuova osservazione completa della stessa rete |

La tabella mostra nome scelto, IP nella cache, MAC, presenza, riconoscimento e scheda. I nomi non vengono ricavati dal MAC e non vengono effettuate ricerche DNS. Il nome iniziale è «Nome non disponibile».

## Cosa significa l’indicatore

| Stato | Significato |
|---|---|
| Verde | Tutte le voci rilevate con MAC hanno un’etichetta scelta dall’utente e la lettura non ha limiti rilevati |
| Giallo | Sono presenti voci con MAC da riconoscere |
| Grigio | Lettura fallita, parziale o vuota; MAC o impronta di rete mancanti; registro non leggibile |

I colori descrivono l’elenco e i riconoscimenti. **Non attestano la sicurezza della rete, l’assenza di intrusioni o il numero di persone collegate.** La cache non è un inventario completo del router.

«Raggiungibile di recente» corrisponde allo stato Windows Reachable. Stale, Delay e Probe non diventano «collegato adesso». Anche una voce statica non dimostra presenza attuale. «Cache letta» è l’orario della lettura, non l’ora dell’ultima attività del dispositivo.

IPv4 e IPv6 con lo stesso MAC sono raggruppati nella stessa scheda e impronta di rete. Lo stesso dispositivo può quindi comparire più volte. Gli IP privi di un MAC valido restano visibili come indirizzi senza MAC e non incrementano il conteggio dei dispositivi. Sono esclusi indirizzi propri del PC, multicast, loopback e broadcast IPv4 calcolati dalla maschera della scheda. La vista considera schede Ethernet e Wi-Fi attive; può includere schede virtuali di questi tipi. Non presenta questo ambito come l’intera rete.

## Acquisizione e consumo

Il lettore separato `WindowsNetworkDevices` usa `GetIpNetTable2(AF_UNSPEC)` per leggere la tabella locale IPv4/IPv6 e libera sempre il buffer con `FreeMibTable`. Il parsing usa dimensione e offset di allineamento tramite Marshal. Le strutture sono affiancate da fixture byte indipendenti nei test C#, senza interrogare una rete reale.

Non vengono avviati PowerShell, scansioni di porte, ping, risoluzioni DNS o processi esterni. La lettura parte solo da **Aggiorna elenco**, su worker. Nessun timer della nuova sezione. Richieste simultanee vengono accorpate; un risultato non viene pubblicato dopo il cambio sezione, la riduzione o la chiusura della finestra. Al cambio sezione vengono rilasciati righe, snapshot e catalogo caricato. Il piccolo storico delle novità rimane nella sessione, con limiti espliciti.

La tabella usa virtualizzazione e riciclo delle righe. Acquisizione e presentazione hanno un limite di 4.096 voci; i limiti di acquisizione risultano come lettura parziale. Le schede di rete vengono lette prima e dopo la tabella: un cambiamento della configurazione invalida l’impronta e lo stato di lettura completa. Non è una transazione atomica con il router e non promette di rilevare ogni cambio di rete.

**Nessuna misura del consumo RAM/CPU è stata eseguita qui.** Queste scelte limitano il lavoro aggiunto, senza quantificare un risparmio.

## Riconoscimenti e novità

I nomi sono memorizzati in `%LOCALAPPDATA%\ProcessLens\network-devices.json`: schema versione 1, massimo 512 etichette, 80 caratteri per nome, file massimo 512 KiB. Scrittura su file temporaneo e sostituzione del file finale. File non valido, schema sconosciuto o errore di lettura vengono segnalati; non vengono sostituiti automaticamente.

La chiave è composta da un hash locale di ID stabile della scheda e gateway configurati con IP/MAC leggibili, più il MAC del dispositivo. L’indice temporaneo di zona IPv6 non entra nell’hash. Senza MAC dei gateway il riconoscimento è disabilitato. **L’impronta è un criterio locale di raggruppamento, non un’autenticazione:** non legge SSID o profilo NLM; reti con identica scheda e identiche coppie gateway IP/MAC possono condividere l’impronta. MAC imitati o cambiati possono produrre corrispondenze o nuovi elementi. L’etichetta «Riconosciuto da te» mantiene questo limite.

La prima lettura completa di ogni impronta crea il riferimento della sessione: non segnala tutti i dispositivi iniziali come nuovi. Le letture complete successive avvisano per MAC nuovi non già riconosciuti. Un’assenza temporanea non azzera la memoria; letture parziali o fallite non la aggiornano. Storico massimo: 16 impronte, 1.024 MAC per impronta. Un’impronta con oltre 1.024 MAC in una lettura non aggiorna gli avvisi. Se storico e campione superano il limite, il riferimento viene riavviato senza inventare una novità; dopo 16 impronte viene rilasciata quella più vecchia. Lo storico non viene salvato tra avvii. Per avere l’avviso serve un nuovo aggiornamento esplicito.

## Verifiche e ambito

Nuove suite, collegate a `build-dashboard.ps1`:

- `ProcessLens.NetworkDeviceTests`: normalizzazione, scope, gruppi IPv4/IPv6, dati mancanti, stati cache, riconoscimenti, novità, persistenza e fixture di layout nativo.
- `ProcessLens.NetworkUiTests`: sorgente simulata, lettura manuale, riconoscimento e rimozione, copia della tabella, coalescenza, risultato tardivo, rilascio righe, errori e navigazione reale XAML. Nessuna finestra mostrata, nessuna lettura reale di rete, nessuna modifica alla clipboard.
- `check-network-devices-r12.ps1`: verifica statica Windows del collegamento e dei vincoli.
- `audit/tools/check_network_devices_r12.py`: controlli di sorgente/XML e mutazioni deliberate dei vincoli.

**Compilazione .NET/WPF, suite C#, PowerShell, confronto con una rete Windows reale e prove DPI non eseguiti in questo ambiente:** SDK .NET, PowerShell e WPF non disponibili. I controlli Python non sostituiscono queste verifiche. I risultati correnti sono sotto `audit/R10_R12`; quelli delle revisioni precedenti restano storici.

I quattro sorgenti esistenti modificati sono MainWindow XAML/C#, metadati versione dell’app e visibilità interna per i test. Sono aggiunti lettore Windows, modelli Core e vista Rete. I precedenti collector, calcoli CPU/RAM, Core/Semantics, Guardian, lettori SQLite, MemoryAgent, profiler e monitor RAM restano identici alla R11 R2. Nessuna installazione o azione su processi è stata eseguita.

## Verifica su Windows

```powershell
.\build-dashboard.ps1
```

Aprire la candidata compilata, scegliere **Rete → Aggiorna elenco**. Verificare nome/MAC/IP confrontando la cache, senza aspettarsi un inventario del router:

```powershell
Get-NetNeighbor | Select-Object InterfaceIndex, IPAddress, LinkLayerAddress, State
```

Provare riconoscimento, riapertura, rimozione, copia, refresh ripetuto, cambio sezione durante la lettura e riduzione/ripristino. Verificare la lettura su Wi-Fi/Ethernet, anche senza gateway leggibile, senza connessione e con schede virtuali. Controllare spaziatura e scorrimento a 950×680 e 1180×800 DIP, scale 100/125/150/200%. Il mancato rilevamento di un host non dimostra che sia scollegato.

## Fonti tecniche

Consultate il 20 ottobre 2026. Le etichette sono scelte di ProcessLens; le fonti descrivono API e stati, non certificazioni di sicurezza.

- https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getipnettable2
- https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-mib_ipnet_table2
- https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-mib_ipnet_row2
- https://learn.microsoft.com/en-us/windows/win32/api/ws2ipdef/ns-ws2ipdef-sockaddr_in6_lh
- https://learn.microsoft.com/en-us/powershell/module/nettcpip/get-netneighbor
