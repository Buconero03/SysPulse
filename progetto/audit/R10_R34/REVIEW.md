# ProcessLens R34 — rete e log

Base: R33 R1 integra, SHA-256 eefa97864d7b0dc22d22a3aec08707a4bf7a968004fb4cc25b9ef1f11843a090. Si prosegue dalla candidata corrente, senza ripetere la revisione terminologica storica.

## Risultato

Tre schede con compiti distinti: Dispositivi osservati, Traffico e Wi-Fi, Eventi e blocchi. Azioni principali evidenziate, titoli e istruzioni brevi, riepiloghi separati dai dettagli, intestazioni scorrevoli e separatori ridimensionabili per tabella e spiegazione. Non è disponibile una prova grafica WPF su questo host Linux.

L'elenco dei dispositivi ora spiega il vuoto: mai letto, invalidato dal cambio scheda, errore, lettura riuscita senza righe. Lo svuotamento non lascia l'ora precedente come se l'elenco fosse corrente. I contatori parlano di voci per MAC e scheda; il riconoscimento resta un'etichetta locale dell'utente. Il rapporto è copiabile anche quando la lettura restituisce zero righe o un errore strutturato.

Il log condiviso di sessione raccoglie inizio, esito, durata, conteggi grezzi e visualizzati, stato dell'audit, errori, annullamenti e risultati tardivi esclusi. Orari UTC, fasi telegrafiche, massimo 128 righe / 1200 caratteri per messaggio. I dati non possono introdurre nuove righe fittizie tramite caratteri di controllo o separatori Unicode. Nessun timer, writer su disco o raccolta aggiuntiva in background. Il rapporto completo della singola operazione resta separato dal log abbreviato.

La selezione di una riga di traffico spiega byte campionati, componente e codice scarto senza attribuirli a un PID. La selezione Wi-Fi spiega SSID/BSSID, RSSI e cache radio senza contarli come client connessi. La selezione di un evento Security separa significato, dati registrati e verifica successiva; i PID restano storici e non provano l'identità dell'istanza attuale.

## Lettura estesa degli eventi

Prima: 5157 e 4625 tipi 3/10, ultimi 15 minuti. Ora: anche 5155 e 5159, intervallo 15 o 60 minuti; opzionalmente 5152. I due nuovi eventi di porta locale usano ProcessId come nello schema pubblicato e non ricevono una destinazione remota inventata. Conservati ID di filtro e livello WFP, con limite interpretativo per identificatori storici.

La query filtra i 4625 locali non pertinenti prima di consumare il budget e ordina i risultati per ora/record dopo la lettura. Gli scarti per pacchetto sono esclusi di default per evitare che eventi numerosi occupino il campione. Si controlla la categoria audit Packet Drop solo se richiesta. Tutte le categorie sono letture: nessun criterio viene attivato o disattivato automaticamente. Le istruzioni auditpol restano esplicite e facoltative.

Limiti invariati: 256 record esaminati per lettura, 3 secondi di enumerazione, 1 Mi caratteri XML complessivi, 32 Ki caratteri per evento. Accesso negato, registri incompleti, XML non interpretabile e budget esaurito rimangono visibili. L'intervallo di un'ora amplia la ricerca temporale, non il numero massimo di record.

Gli eventi devono essere stati registrati da Windows. I 5155/5159 non sono promessi come frequenti; la loro presenza dipende dai filtri WFP e dalle operazioni effettive. Non sono state aggiunte catture radio, installazioni, sonde, letture del router o superamenti dei limiti Wi-Fi. Nessuna conclusione automatica su intrusi, rete sicura o persone che osservano l'IP.

## Verifiche eseguite

- Progetti reali .NET 8 SDK/MSBuild: NativeNetworkTests 77, NetworkDeviceTests 81, NativeCaptureTests 75, CompatibleCaptureTests 56, CopyTests 52: totale 341 asserzioni PASS.
- 44 fasi PowerShell e 17 fixture del launcher: PASS.
- Nove assembly C# WindowsDesktop: PASS con reference assemblies e campi XAML simulati; gli import impliciti sono valutati da MSBuild. Non equivale a compilazione BAML o esecuzione WPF.
- Nuovi test: eventi 5155/5159/5152, campi SDK esatti e casi mancanti, opzione 15/60, audit condizionale, rifiuto di intervalli errati prima di chiamare API, log limitato e resistente a righe fittizie, copie e spiegazioni it-IT/en-US.
- Fixture WPF aggiornata per contatori, segnaposto vuoti, timestamp invalidati, selezione dettagli e passaggio reale delle opzioni al client simulato. Da eseguire su Windows con build-dashboard.ps1.
- La verifica XPath standard copre categorie e tipi Logon; le comparazioni temporali proprie di Windows Event Log richiedono la prova Windows. I limiti temporali sono verificati separatamente nel decoder e nella query prodotta.
- XML valido, confronto degli ambiti modificati, patch applicata e confronto byte, manifesti SHA-256, ZIP chiuso/sincronizzato/verificato prima della pubblicazione atomica; estrazione indipendente e verifica successiva del trasferimento.

Il warning CA1416 della fixture multipiattaforma della cattura compatibile è già legato a IOControlCode.ReceiveAll, API Windows in un file invariato. La compilazione finale incrementale riporta zero errori. Le vecchie 308 asserzioni R33 non vengono sommate ai controlli attuali.

## Prova su Windows

1. Chiudere con Esci, estrarre in cartella nuova, aprire AVVIA_PROCESSLENS.cmd.
2. Rete → Dispositivi osservati: prima del refresh deve comparire la spiegazione; leggere, cambiare scheda e tornare: nessun vecchio timestamp corrente. Anche un elenco vuoto deve essere spiegato.
3. Traffico e Wi-Fi: elencare senza avviare raccolte, scegliere manualmente la sorgente e acquisire; selezionare una riga. Verificare copia e motivi degli errori, inclusi privilegi o permessi Wi-Fi se Windows li richiede.
4. Eventi e blocchi: scegliere 15/60 minuti, leggere; se il registro è inaccessibile deve comparire N/D e il motivo. Se gli eventi sono presenti, confrontare ID, RecordID, ora e campi con Visualizzatore eventi. Provare l'opzione 5152 senza aspettarsi che attivi l'audit.
5. Aprire il log di sessione e copiarlo; controllare che non sia presentato come log completo della rete. Verificare layout a 100/125/150% DPI e ridimensionamento dei pannelli.
6. Tray e riapertura devono conservare il comportamento funzionante della R33 R1. Nessun dato reale o misura RAM è stato ottenuto su questo host.

## Fonti Microsoft consultate

- https://learn.microsoft.com/en-us/windows/win32/fwp/auditing-and-logging
- https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-5155
- https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-5159
- https://learn.microsoft.com/en-us/windows/win32/wes/consuming-events
