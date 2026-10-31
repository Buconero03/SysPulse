# ProcessLens R13 — integrazione Wireshark/Npcap su Windows

La candidata riparte dalla R12. In **Rete** sono disponibili due schede: **Dispositivi nella cache** e **Wireshark / Wi-Fi**. La seconda viene costruita solo quando viene aperta. Non avvia catture, verifiche del monitor o installazioni automaticamente.

## Installazione sul PC Windows

1. Installa [Wireshark per Windows](https://www.wireshark.org/download.html), includendo il componente **TShark**. La cartella standard è `C:\Program Files\Wireshark`.
2. Installa Npcap, proposto dall’installer Wireshark oppure disponibile dal [sito ufficiale Npcap](https://npcap.com/#download). Abilita **Support raw 802.11 traffic (and monitor mode) for wireless adapters**. Se Npcap è già installato senza questa opzione, modificane/reinstalla la configurazione tramite il suo installer.
3. Segui le eventuali richieste dell’installer e riavvia solo se richiesto. ProcessLens non installa né riconfigura il driver.
4. La scheda e il driver devono supportare monitor/raw 802.11. La presenza di Npcap non basta a garantirlo. Come prima indicazione puoi leggere:

```powershell
netsh wlan show wirelesscapabilities
```

La voce relativa al monitor è un’indicazione del driver. La verifica operativa rimane quella di dumpcap e della successiva ricezione di frame. Se il supporto manca, puoi usare una cattura normale oppure importare un file ottenuto con hardware compatibile.

## Compilazione e utilizzo

```powershell
.\build-dashboard.ps1
```

1. Avvia la candidata compilata e apri **Rete → Wireshark / Wi-Fi**.
2. Premi **Verifica strumenti**. Per un’installazione personalizzata scegli **Scegli dumpcap.exe…**: `tshark.exe` deve trovarsi nella stessa cartella. La presenza del file Npcap è distinta dal funzionamento del driver.
3. Seleziona esplicitamente un’interfaccia. L’app non sceglie automaticamente la prima e accetta soltanto interfacce locali Npcap con identificatore stabile, non indici numerici, pipe o sorgenti remote.
4. Per la modalità normale lascia il monitor disattivato. Per ricevere intestazioni/frame 802.11 seleziona **Usa modalità monitor** e premi **Verifica monitor**. **Già questa verifica può disconnettere il Wi-Fi.** Il pulsante di cattura monitor si abilita soltanto dopo una dichiarazione positiva dei link type per quella interfaccia/strumento. Non è ancora una prova di ricezione radio.
5. Premi **Salva nuova cattura…** e scegli un **nuovo file locale `.pcapng`**. I file esistenti non vengono sovrascritti. La cattura termina al primo limite applicabile: 20 secondi, 10.000 pacchetti o 10.000 kB nel criterio dumpcap. Snaplen 256 byte e buffer richiesto 2 MiB. Il driver può applicare un buffer diverso; non è un limite alla memoria totale del processo.
6. Premi **Analizza ultima cattura**. In alternativa usa **Analizza file…** per una cattura locale già esistente, fino a 12 MiB.
7. Puoi copiare tabella/rapporto o aprire il file con **Apri file in Wireshark**. La GUI Wireshark si avvia soltanto da quel pulsante e rimane uno strumento esterno sotto il tuo controllo.

**Interrompi operazione**, cambio scheda, riduzione o chiusura della sezione annullano il lavoro gestito dall’app. Vengono interrotti soltanto i processi diagnostici creati da quel comando. Un’interruzione può lasciare il file scelto vuoto o parziale: il percorso è quello selezionato nel dialogo. Un file parziale può essere provato con Analizza file; un errore del decoder resta un errore. La riconnessione Wi-Fi dopo il monitor dipende da Windows/driver; l’app non cambia profilo, password o canale per forzarla.

## Cosa mostra il rapporto

| Dato | Significato |
|---|---|
| Trasmettitore nel frame | Campo `wlan.ta` del pacchetto letto |
| Destinatario nel frame | Campo `wlan.ra`; non prova che il destinatario abbia risposto |
| BSSID nel frame | Valore dichiarato nel campo `wlan.bssid` |
| Rete annunciata / SSID | Nome dichiarato in beacon o probe response; non nei probe request |
| Frequenza e segnale | Campi Radiotap, quando presenti; altrimenti N/D |
| Frame nel campione | Conteggio per quella osservazione; uno stesso frame può alimentare più righe, quindi le righe non vanno sommate come pacchetti distinti |

L’analisi legge i primi 2.048 pacchetti del file, mostra al massimo 512 combinazioni di osservazioni e segnala i limiti. Il decoder gira separatamente, con risoluzione dei nomi disabilitata. Durante l’analisi il file è aperto in lettura senza condivisione di scrittura su Windows; nel rapporto è incluso lo SHA-256 dei byte letti. I dettagli della cattura appena eseguita vengono mantenuti nel rapporto relativo allo stesso file.

SSID e indirizzi sono contenuto dei frame: possono cambiare o essere imitati. Un SSID può appartenere a un router, un ripetitore o un telefono. La sua presenza non ne dimostra l’appartenenza alla LAN. Un indirizzo nei frame non identifica una persona e non conferma un’associazione attuale al tuo access point. Questa versione non aggiunge una tabella client del router né un elenco dei client dell’hotspot Windows.

Una cattura normale può avere soltanto intestazioni Ethernet anche se il PC usa Wi-Fi. Zero frame 802.11 decodificati non significa zero dispositivi wireless. In monitor si osserva ciò che riceve la scheda sul canale impostato dal driver, non tutti i canali contemporaneamente. Non sono implementati cambi automatici di canale, decrittazione o trasmissione/iniezione di frame. Il NAT di un telefono può nascondere i client a valle rispetto alla LAN principale.

## Comandi integrati

Esempi strutturali; l’app passa ogni argomento separatamente, senza shell. Il segnaposto dell’interfaccia viene sostituito con il nome Npcap scelto, non con il suo indice.

```text
dumpcap.exe -D
dumpcap.exe -i <interfaccia-locale> -I -L
dumpcap.exe -i <interfaccia-locale> [-I oppure -p] -a duration:20 -a filesize:10000 -c 10000 -s 256 -B 2 -q -w <nuovo-file.pcapng>
tshark.exe -n -r <file-locale> -c 2048 -T fields -E separator=/t -E quote=n -E occurrence=f -E escape=y -e frame.number -e wlan.ta -e wlan.ra -e wlan.bssid -e wlan.fc.type -e wlan.fc.subtype -e wlan.ssid -e radiotap.channel.freq -e radiotap.dbm_antsignal
```

La verifica monitor richiede una scelta esplicita anche quando l’interfaccia è già presente nell’elenco. Timeout dei comandi: elenco/verifica 10 s, acquisizione massimo 30 s di processo inclusa inizializzazione, decoder 15 s. Output massimo 512 Ki caratteri per ciascun flusso stdout/stderr; un superamento interrompe il comando, non produce un rapporto vuoto di successo. La normale fine della cattura resta regolata dai criteri dumpcap sopra.

## Risorse, file e ambito

Dumpcap, TShark e la GUI Wireshark sono processi separati. I limiti di durata, file e output non sono limiti alla loro RAM complessiva. **Consumo CPU/RAM non misurato qui.** Non restano catture automatiche in background; il visualizzatore aperto dall’utente è indipendente. Per confrontare il consumo totale considera anche gli strumenti esterni; il monitor RAM conserva le sue regole precedenti di inclusione e discendenza verificata.

Il file scelto può contenere dati di rete anche con snaplen 256. Rimane sul PC nel percorso indicato; non viene caricato automaticamente né inserito nello ZIP del progetto. Eliminalo quando non serve. L’app non elimina automaticamente i file scelti e non registra payload nel rapporto testuale. L’analisi limitata può non contenere tutte le osservazioni presenti nel file.

MainWindow, calcoli CPU/RAM, collector precedenti, Guardian, lettori SQLite, MemoryAgent, profiler e monitor RAM sono conservati rispetto alla R12. Aggiunti policy/parser, adattatore dei comandi e pannello WPF; la vista Rete precedente è estesa con una scheda caricata su richiesta. Le righe della cache vengono invalidate entrando nella scheda radio, per evitare di presentare il vecchio elenco come aggiornato dopo una possibile disconnessione.

## Verifica tecnica

Nuova suite `ProcessLens.PacketCaptureTests`: argomenti e limiti, parser di fixture TShark, culture it-IT/en-US, SSID, valori mancanti, scope locale, file nuovi/limiti/errori e hash. Le prove del runner avviano **soltanto una copia dello stesso eseguibile di test** per controllare argomenti, timeout, annullamento e output eccessivo; nessun tool di cattura viene avviato da questi test. Estesa `ProcessLens.NetworkUiTests` con client simulato, gate monitor, selezione esplicita, copia e risultati tardivi. Collegati entrambi alla build Windows.

**Compilazione .NET/WPF, suite C#, PowerShell, installazione Npcap, cattura reale e prove DPI non eseguiti qui:** SDK .NET, PowerShell, Windows e scheda radio non disponibili. I controlli Python verificano sorgenti/XML e mutazioni, non certificano compilazione o compatibilità hardware. Stato corrente, patch e registri sono in `audit/R10_R13`; gli audit precedenti sono storici.

Sul PC Windows verificare modalità normale, supporto monitor, errore permessi/driver, ricezione effettiva, interruzione/cambio sezione/riduzione, riapertura della connessione e importazione di file malformati. Provare 950×680 e 1180×800 DIP alle scale 100/125/150/200%. Il pannello ha scorrimento verticale e tabella virtualizzata con altezza finita.

## Fonti

Consultate il 20 ottobre 2026. Le etichette e i limiti operativi sono scelte di ProcessLens.

- [Manuale dumpcap](https://www.wireshark.org/docs/man-pages/dumpcap.html)
- [Manuale TShark](https://www.wireshark.org/docs/man-pages/tshark.html)
- [Guida Npcap e modalità monitor](https://npcap.com/guide/npcap-users-guide.html)
- [Cattura WLAN su Windows](https://wiki.wireshark.org/CaptureSetup/WLAN)
- [Campi IEEE 802.11](https://www.wireshark.org/docs/dfref/w/wlan.html)
- [Campi Radiotap](https://www.wireshark.org/docs/dfref/r/radiotap.html)
- [Tethering Android e NAT IPv4](https://source.android.com/docs/core/ota/modular-system/tethering)
