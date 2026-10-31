# R16 → R17: prima / dopo

| Prima R16 | Dopo R17 | Confine mantenuto |
| --- | --- | --- |
| Seconda scheda: Wireshark / Wi-Fi; strumenti esterni necessari | Cattura Windows / Wi-Fi; PktMon API e WLAN native | API PktMon verificate dinamicamente; indisponibilità esplicita, nessuna esportazione non documentata |
| Cattura tramite dumpcap; analisi TShark di file | Sessione PktMon privata in memoria con metadati endpoint | Non è un sostituto completo dei dissector Wireshark; nessuna promessa di esportazione PCAP |
| Wi-Fi dipendente da Npcap e dalla cattura 802.11 | Lettura diretta della cache BSS con SSID, BSSID, RSSI, frequenza, tipo AP/peer | Non dimostra client connessi né hotspot telefonico |
| Modalità monitor opzionale esterna | Nessuna modifica della modalità radio | Hardware, driver, canale e cifratura non aggirabili per software |
| Test parentela UI verifica NetworkCaptureView | Verifica NativeCaptureView; le fixture del vecchio client rimangono | Contesto Dispatcher, copia, selezione esplicita e rigetto risultati tardivi mantenuti |
| Controlli R16 e cronologia precedente | Controlli R17 più riqualifica dei 149 controlli precedenti | Eccezioni limitate a successori R17 con hash esatto nel registro autorizzato |

Nessuna revisione ripetuta delle regole semantiche già concluse; Guardian, profiler, MemoryAgent e acquisizioni precedenti non modificati. Base ZIP R16 verificata: `dfc956ea3405d82e1a1e01d94c4d685030e183d73af53c4c1fa5399ea6a813e3`.

Le misure sono da fare su Windows: nessuna quantità totale di RAM o risparmio stimato viene presentato come osservato. Vedere RETE_NATIVA_R17.md e test-matrix.csv.
