# R18 — cattura IP locale e audit spiegato

Questa candidata continua l’esatta R17, dopo le due schermate segnalate il 22 ottobre 2026. Non ripete la revisione semantica precedente.

## Che cosa si vede nelle schermate

- **Cattura Windows / Wi-Fi:** il testo «Sezione chiusa» e l’assenza di sorgenti non mostrano il risultato dell’ultimo tentativo. La R17 rilasciava righe e messaggi quando si lasciava la scheda. Dalla schermata non si ricava un codice errore né si può concludere che la DLL sia assente.
- **Segnali Windows:** la lettura ha restituito zero eventi, con audit WFP disabilitato e audit Logon abilitato. Non è una prova di guasto della query: il sistema potrebbe non aver registrato i blocchi WFP. La UI dava troppo risalto ai conteggi e poco alla copertura mancante.

## Come usare la R18

1. Estrarre in una cartella nuova e compilare con `.\build-dashboard.ps1`. Avviare l’EXE generato: il manifest richiede amministratore.
2. Aprire **Rete → Cattura Windows / Wi-Fi → Elenca sorgenti Windows**.
3. Per una cattura indipendente dalle esportazioni PktMon, scegliere **Windows · IP locale · <scheda> · <IPv4>**, quindi **Cattura per 20 s**. Durante la raccolta usare normalmente una propria applicazione di rete sulla scheda scelta; ProcessLens non genera traffico di prova. È necessaria una scheda attiva con IPv4 locale univoco. La semplice enumerazione non apre un socket né certifica che i permessi consentano la cattura.
4. Le sorgenti **PktMon** rimangono distinte e selezionabili solo quando le API documentate sono disponibili. Se non lo sono, il motivo rimane leggibile senza impedire l’elenco IP locale.
5. **Leggi reti Wi-Fi** continua a leggere la cache radio WLAN. Richiede scheda/servizio WLAN attivi e può richiedere il consenso Posizione. Non è il conteggio dei client del router.
6. Alla fine leggere protocollo, endpoint e byte campionati. **Interrompi lettura** ferma la raccolta. Quando si cambia scheda vengono rilasciate le righe e gli handle; resta una diagnosi testuale limitata e copiabile con l’ora UTC dell’ultimo esito. Un risultato tardivo non ripopola la scheda chiusa.

## Alternativa Winsock

Il nuovo collector usa il socket Windows `AF_INET / SOCK_RAW / IPPROTO_IP`, associa un IPv4 locale esplicito e imposta `SIO_RCVALL` a **RCVALL_IPLEVEL = 3**. Non usa RCVALL_ON, non abilita la modalità promiscua della scheda e non cambia il canale o la modalità Wi-Fi. Non dipende dalle esportazioni di `Pktmonapi.dll`, da Wireshark o da Npcap.

Il bind viene verificato sulla stessa identità di scheda e sullo stesso indirizzo correnti; un IPv4 condiviso tra più schede non diventa una scelta ambigua. Un indirizzo migrato a un’altra scheda richiede una nuova enumerazione. I token di selezione non sopravvivono alla chiusura o a una nuova enumerazione.

Limiti: solo IPv4 sulla scheda scelta; nessun ARP, frame 802.11, dissector completo o attribuzione al PID. L’ora è quella di ricezione nel programma, distinta dal timestamp del driver di PktMon. Componenti PktMon, motivi scarto e perdite non misurate sono **N/D**, mai zeri inventati. Anche un campione terminato regolarmente può aver perso pacchetti nel socket: questa perdita non è misurabile da questa lettura.

La cattura conserva soltanto metadati derivati da intestazioni fino a 128 byte; il datagramma completo passa temporaneamente in un buffer di 65.535 byte, poi il buffer viene azzerato. Massimo 2.048 righe e 20 s di raccolta attiva, polling non bloccante con attesa di 100 ms. Setup e chiamate di sistema non hanno una garanzia di durata rigida. Il socket viene ripristinato a RCVALL_OFF e chiuso anche su errori o annullamento. Nessun nuovo processo ausiliario, servizio o timer permanente. Nessuna cifra di RAM totale o risparmio è stata misurata qui.

## Segnali Windows: attivare i futuri blocchi WFP

La scheda evidenzia **Copertura incompleta** se un criterio è disabilitato e **Copertura non verificata** se la lettura del criterio manca. «Nessun evento trovato» resta un esito possibile anche con audit attivo.

Per attivare i futuri eventi 5157, aprire un terminale amministrativo e controllare prima lo stato:

```powershell
auditpol /get /subcategory:"{0CCE9226-69AE-11D9-BED3-505054503030}"
```

Se si desidera attivarli:

```powershell
auditpol /set /subcategory:"{0CCE9226-69AE-11D9-BED3-505054503030}" /failure:enable
```

Poi premere **Leggi eventi Windows**. Non vengono ricreati eventi passati e non vengono provocati blocchi o accessi falliti di prova. Abilitare l’audit può aumentare il volume del registro Security. Se prima era disabilitato e si vuole ripristinare quello stato:

```powershell
auditpol /set /subcategory:"{0CCE9226-69AE-11D9-BED3-505054503030}" /failure:disable
```

Queste istruzioni sono anche nell’interfaccia, sotto **Come attivare la raccolta**. ProcessLens non esegue i comandi e non cambia criteri di audit, firewall o rete. I criteri di un PC aziendale possono prevalere.

## Verifiche eseguite e limiti

- **332 asserzioni .NET portabili PASS**, incluse 56 nuove per sorgenti, identità, socket simulati, buffer, errori, chiusura, cap, annullamento, informazioni mancanti e copertura audit, in it-IT/en-US.
- Controllo dei tipi C# di Windows, Semantics, App e tre progetti di test UI: sei assembly senza errori o avvisi. Sono usati stub espliciti per i campi generati: **non è compilazione del markup WPF/BAML né esecuzione dei test WPF**.
- Sorgenti/markup XML e regressioni precedenti ricontrollati; patch R17→R18, prima/dopo, registro stringhe e manifest SHA-256 inclusi.
- **NON ESEGUITI:** cattura reale Winsock/PktMon, chiamate WLAN reali, modifica manuale dell’audit, build completa Windows, sintassi PowerShell, esecuzione WPF, DPI e misure reali RAM/CPU.

Prima della promozione eseguire la pipeline Windows e provare: sorgente IP locale su scheda attiva; traffico proprio noto; interruzione; chiusura/riapertura; errori ancora copiabili; audit disabilitato/abilitato senza generare eventi fittizi. Nessuna promessa di vedere tutti i dispositivi Wi-Fi o rilevare chi osserva l’IP.

## Fonti primarie

- [SIO_RCVALL: modalità IPLEVEL, bind esplicito e privilegi](https://learn.microsoft.com/en-us/windows/win32/winsock/sio-rcvall)
- [IOControlCode.ReceiveAll](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.iocontrolcode)
- [Auditing WFP: sottocategorie e GUID indipendenti dalla lingua](https://learn.microsoft.com/en-us/windows/win32/fwp/auditing-and-logging)
- [Criteri avanzati: significato e volume degli eventi](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/advanced-audit-policy-configuration)

Audit corrente: `audit/R10_R18`. Le revisioni precedenti rimangono come storico.
