# R17 — cattura Windows e reti Wi-Fi native

Candidata sorgente basata sull’esatta R16, non una nuova revisione dell’analisi terminologica già completata. La seconda scheda di Rete diventa **Cattura Windows / Wi-Fi**. Questa funzione non richiede Wireshark, TShark o Npcap. La vecchia implementazione esterna e i suoi test rimangono nei sorgenti come compatibilità storica; non sono caricati dalla nuova scheda.

## Uso su Windows

1. Estrarre lo ZIP in una cartella nuova. Eseguire `.\build-dashboard.ps1` con .NET 8 SDK su Windows. La pipeline include il nuovo controllo sorgente e `ProcessLens.NativeCaptureTests`, oltre alle suite precedenti e WPF.
2. Aprire ProcessLens, poi **Rete → Cattura Windows / Wi-Fi**. L’apertura non legge la rete né avvia una cattura.
3. **Leggi reti Wi-Fi** legge la cache BSS del servizio WLAN: scheda, SSID, BSSID, RSSI in dBm, frequenza e tipo AP/peer ad hoc. Non installa driver e non forza una scansione. Windows può richiedere il consenso Posizione. Se l’accesso è negato, il motivo rimane visibile; l’amministratore non sostituisce quel consenso. Radio spenta, servizio WLAN inattivo o scheda assente restano distinguibili dagli errori.
4. Per i pacchetti, avviare ProcessLens con i privilegi amministrativi richiesti da PktMon, premere **Verifica PktMon**, selezionare una sorgente e premere **Cattura per 20 s**. Nessuna sorgente viene selezionata automaticamente.
5. **Interrompi lettura** annulla l’operazione. La cattura conserva il campione già ricevuto se la scheda è ancora aperta; chiudere la sezione annulla e scarta i risultati tardivi. Le chiamate WLAN/native in corso non sono forzatamente terminate: l’annullamento viene controllato prima e dopo, senza promettere un limite rigido per la durata totale del driver.
6. Selezionare/copiare le righe, oppure usare **Copia rapporto**. La cattura registra inizio/fine UTC, sorgente selezionata, orario locale degli eventi e perdite segnalate. Non crea automaticamente ETL, PCAP o file contenenti payload.

## Disponibilità PktMon

L’integrazione usa solo le esportazioni pubblicamente documentate di `Pktmonapi.dll`, caricata dal percorso assoluto di sistema, con versione API `0x00010000`. Sono controllate tutte le funzioni richieste prima dell’inizializzazione. Questa candidata supporta l’ABI PktMon Windows x64. La documentazione Microsoft non specifica una versione client minima per queste API: **la presenza di `pktmon.exe` non garantisce le esportazioni multisessione**.

DLL mancante, esportazioni assenti, privilegi insufficienti o incompatibilità mostrano **non disponibile** con la causa. La funzione non richiama esportazioni interne non documentate e non ripiega su un `pktmon stop` globale, che potrebbe interrompere Defender o una cattura altrui. Wi-Fi BSS, cache dispositivi e Segnali Windows hanno letture indipendenti e rimangono utilizzabili secondo i rispettivi permessi.

## Che cosa vedo davvero?

| Lettura | Dato disponibile | Limite |
| --- | --- | --- |
| Cache dispositivi | Voci IPv4/ARP e IPv6/ND del PC | Non è il registro completo dei client del router |
| PktMon nativo | Metadati dei pacchetti osservati sulla sorgente scelta | Nessun elenco di tutti i dispositivi Wi-Fi e nessuna attribuzione al processo proprietario |
| Native Wi-Fi BSS | AP o peer ad hoc rilevati dalle scansioni del sistema | Cache radio, non client connessi; SSID non prova un hotspot telefonico |
| Segnali Windows R16 | Blocchi WFP e accessi remoti/rete falliti registrati | Dipende da audit e accesso al registro; non certifica sicurezza o assenza di attacchi |

Il decoder PktMon estrae solo Ethernet/VLAN e intestazioni IP di base. IPv4 frammentato e IPv6 con estensioni non acquisiscono porte dedotte. ARP è etichettato senza inventare dispositivi; 802.11 grezzo e tipi sconosciuti restano non decodificati. I codici scarto sono numerici, senza interpretazioni inventate. Le righe sono **osservazioni**, non pacchetti unici: lo stesso pacchetto può apparire più volte nello stack. I byte indicano il campione ricevuto, non il totale del traffico.

## API per superare il limite Wi-Fi: risultato della ricerca

`WlanGetNetworkBssList` e `WlanGetAvailableNetworkList` espongono reti/AP rilevati dalla scheda senza Npcap. `WlanScan` può chiedere una nuova scansione, ma non è una cattura radio completa e non viene chiamata da questa candidata. `WlanSetInterface` documenta la richiesta della modalità network monitor: è una richiesta al driver, non un modo per rendere compatibile qualunque scheda. Cambiare modalità può interrompere la connessione. Questa candidata non cambia modalità, canale o configurazione radio.

Non risulta un’API Windows documentata che garantisca il traffico di tutti i client o aggiri hardware, driver, canale radio e cifratura. Nessuna di queste letture determina chi osserva il proprio IP. Per il numero completo dei client della propria rete serve la vista del router/access point; nessuna interrogazione al router è aggiunta qui.

## Risorse e verifiche

- Nessun nuovo timer, processo ausiliario, servizio o monitor permanente.
- Una sola operazione UI per volta. Risultati rilasciati alla chiusura della sezione.
- PktMon: sessione privata nominata casualmente; viene attivato/arrestato soltanto il relativo handle. Ring buffer richiesto: moltiplicatore 1 della base documentata di 256 KB, non una stima della RAM complessiva. Intestazioni fino a 128 byte; massimo 2.048 righe; attesa attiva massima 20 s dopo l’attivazione.
- Tabelle sorgenti native fino a 1 MiB e 256 sorgenti; WLAN fino a 16 interfacce, buffer per BSS fino a 1 MiB e massimo complessivo di 512 righe. Blob di information elements non interpretati. Perdite, limiti raggiunti, stop imprevisti ed errori mantengono lo stato parziale.
- **276 asserzioni .NET portabili eseguite** su Linux con Roslyn/.NET 8, di cui 75 nuove su buffer sintetici e handle simulati posseduti. Nessuna cattura reale nei test.
- Controllo dei tipi C# con reference WindowsDesktop e stub dei soli campi generati: distinto dalla compilazione markup WPF e dalla sua esecuzione. I log e il perimetro esatto sono in `audit/R10_R17`.
- **Build Windows/MSBuild, PowerShell, markup WPF/BAML, esecuzione WPF, DPI, chiamate native reali e misure RAM/CPU: NON ESEGUITI in questo ambiente Linux.** Nessuna riduzione o quantità aggiuntiva di RAM dichiarata come misurata.

Prima di promuovere la candidata: completare `build-dashboard.ps1`; provare il ramo API assenti/accesso negato; lettura WLAN con radio attiva e disattiva; cattura manuale e interruzione; chiusura/riapertura durante la raccolta; confronto cache/router senza equipararli; leggibilità a DPI 100/150/200%; confrontare il Working Set privato del gruppo ProcessLens prima/durante/dopo. Non interrompere catture estranee per fare la prova.

## Fonti Microsoft consultate

- [API PktMon: multisessione e disponibilità](https://learn.microsoft.com/en-us/windows/win32/pktmon/pktmon)
- [Inizializzazione, versione e privilegi](https://learn.microsoft.com/en-us/windows/win32/pktmon/packetmonitor/nf-packetmonitor-packetmonitorinitialize)
- [Stream, callback, layout e limiti](https://learn.microsoft.com/en-us/windows/win32/pktmon/packetmonitor/nf-packetmonitor-packetmonitorcreaterealtimestream)
- [Enumerazione sorgenti](https://learn.microsoft.com/en-us/windows/win32/pktmon/packetmonitor/nf-packetmonitor-packetmonitorenumdatasources)
- [Chiusura stream](https://learn.microsoft.com/en-us/windows/win32/pktmon/packetmonitor/nf-packetmonitor-packetmonitorcloserealtimestream)
- [WlanGetNetworkBssList](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetnetworkbsslist)
- [WLAN_BSS_ENTRY](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/ns-wlanapi-wlan_bss_entry)
- [Consenso alla posizione e accesso Wi-Fi](https://learn.microsoft.com/en-us/windows/win32/nativewifi/wi-fi-access-location-changes)
- [WlanSetInterface e modalità richieste al driver](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlansetinterface)

Audit corrente R17: `audit/R10_R17`; gli audit precedenti sono storici. Diff completo rispetto alla R16 e manifest SHA-256 sono inclusi.
