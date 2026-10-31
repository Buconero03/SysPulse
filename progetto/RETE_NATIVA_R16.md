# R16 — segnali di rete con le API di Windows

La scheda **Rete → Segnali Windows** legge dati locali senza Wireshark, TShark, Npcap, comandi esterni o driver aggiuntivi. La scheda di cattura Wireshark resta opzionale e distinta.

## Dati disponibili

- **Dispositivi nella cache**: continua a usare GetIpNetTable2, già presente. IP/MAC osservati dal PC, riconoscimenti locali e nuove voci; non è il conteggio completo dei client attualmente associati al router.
- **Segnali Windows**: legge gli eventi Security 5157, connessioni bloccate da Windows Filtering Platform, e 4625 di tipo 3 o 10, accessi di rete o interattivi remoti falliti.
- Mostra ora, IP e porte riportati, direzione del blocco, protocollo, programma e PID riportato all'epoca. Non associa un PID storico al programma attuale e non deduce una porta RDP dal solo tipo di accesso.
- Interroga AuditQuerySystemPolicy per le sottocategorie Filtering Platform Connection e Logon. Distingue raccolta errori abilitata, disabilitata o configurazione non leggibile. Sono criteri attuali di sistema: non ricostruiscono quelli passati o le eccezioni per utente.

Un blocco può essere normale attività del firewall. Un accesso fallito può dipendere da credenziali errate, configurazione o tentativi non autorizzati. Il rapporto descrive quanto registrato; non certifica un'intrusione o un ascolto del traffico.

Le API locali non interrogano il database dei client del router e non rivelano con certezza una cattura passiva su un altro dispositivo. I tentativi fermati dal router prima di raggiungere il PC possono non apparire nei suoi registri. Non viene attribuita l'attività a una persona.

## Uso e disponibilità dei registri

1. Compilare con `.\build-dashboard.ps1` ed eseguire la candidata.
2. Aprire **Rete → Segnali Windows**.
3. Premere **Leggi eventi Windows**. Aprire “Raccolta eventi e limiti” per configurazione audit ed errori.
4. Selezionare una riga per i dettagli. “Copia rapporto” copia campione, limiti, configurazione ed eventi.

La lettura può richiedere privilegi amministrativi o diritti sul registro Security. ProcessLens non eleva privilegi automaticamente per questa scheda.

Se la raccolta è disabilitata, ProcessLens lo segnala e lascia invariata la configurazione. Gli eventi non raccolti in precedenza non si possono recuperare. Su Windows Pro/Enterprise, un amministratore può valutare la configurazione in Criteri di sicurezza locali → Configurazione avanzata dei criteri di controllo: Accesso agli oggetti → Connessione piattaforma filtro, e Accesso/Disconnessione → Accesso, controllo degli errori. Nomi e disponibilità variano con lingua, edizione e criteri aziendali. Non è necessario abilitare i successi o il controllo per ogni pacchetto per usare questa vista. Un'impostazione di dominio può prevalere su quella locale.

## Costo e limiti

Nessun nuovo processo residente, timer, watcher, sottoscrizione ETW o cattura di pacchetti. La sola apertura della scheda non legge Security o la configurazione audit. La lettura parte dal pulsante e riguarda un intervallo assoluto degli ultimi 15 minuti, dal più recente.

Limiti di ogni operazione:

- 256 eventi Security esaminati, anche se alcuni non appartengono ai tipi di accesso mostrati.
- 32.768 caratteri XML per evento e 1.048.576 caratteri XML elaborati in totale.
- Buffer nativo per XML limitato a 65.536 byte; viene liberato dopo ogni evento.
- Budget di enumerazione di 3 secondi e attesa EvtNext di 250 ms per chiamata. Le chiamate native sincrone di apertura, audit e rendering non hanno un timeout globale imposto dall'app: il budget non è una garanzia sulla durata totale.
- Il raggiungimento di un limite o un errore dopo alcune letture produce un rapporto parziale con i dati già letti e la causa. Un errore iniziale non diventa un elenco vuoto riuscito.
- Il cambio scheda/sezione annulla la richiesta e impedisce la pubblicazione di risultati tardivi. Le righe e il campione vengono rilasciati; la raccolta della memoria gestita dipende dal runtime.

Nessuna modifica a firewall, audit, servizi o driver. Non vengono registrati nomi account o righe di comando; il percorso del programma è ridotto al nome del file. La lettura XML vieta DTD e risorse esterne. I registri restano su Windows: la scheda non crea un nuovo archivio giornaliero.

Il consumo reale aggiuntivo di RAM/CPU della GUI **non è stato misurato su Windows**. I limiti dei dati non equivalgono al suo Working Set. Il monitor RAM già presente può essere usato per confrontare R15 e R16 a parità di carico, prima della lettura, dopo una lettura e dopo la chiusura della sezione.

## Verifiche

Eseguiti su Linux con compilazione diretta Roslyn e runtime .NET 8:

- 40 asserzioni per la nuova fonte, con XML sintetico e API simulata: parsing per nome, culture, errori, criteri audit, ABI, gestione degli handle, limiti, annullamento e privacy.
- 58 asserzioni componenti Windows, 22 asserzioni portabili inventario e 81 asserzioni registro dispositivi: regressioni passate. Il ramo di lettura reale Windows è saltato.
- Parsing sintattico C# dei sei file aggiunti/modificati; non è compilazione dei tipi WPF o del markup.
- Controlli sorgenti/XML, mutazioni dei controlli e conservazione delle correzioni precedenti; risultati in `audit/R10_R16`.

**Non eseguiti qui:** chiamate reali ad AuditQuerySystemPolicy/EvtQuery/EvtNext/EvtRender, build completa MSBuild/WPF, PowerShell, test UI, DPI, misure RAM/CPU e generazione di eventi reali. I test WPF aggiunti usano una fonte simulata e sono inclusi nella pipeline Windows. La candidata richiede questa verifica.

Prove Windows: verificare l'apertura senza acquisizione automatica, un registro accessibile e uno negato, raccolta disabilitata, rapporto vuoto con limiti visibili, eventi noti già presenti, cambio scheda durante la lettura, copia rapporto e DPI 100/125/150/200%. Nessun test genera traffico, tentativi di accesso o cambiamenti ai criteri.

## API e documentazione Microsoft

- [GetIpNetTable2](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getipnettable2)
- [EvtQuery](https://learn.microsoft.com/en-us/windows/win32/api/winevt/nf-winevt-evtquery), [EvtNext](https://learn.microsoft.com/en-us/windows/win32/api/winevt/nf-winevt-evtnext), [EvtRender](https://learn.microsoft.com/en-us/windows/win32/api/winevt/nf-winevt-evtrender)
- [AuditQuerySystemPolicy](https://learn.microsoft.com/en-us/windows/win32/api/ntsecapi/nf-ntsecapi-auditquerysystempolicy), [struttura e flag audit](https://learn.microsoft.com/en-us/windows/win32/api/ntsecapi/ns-ntsecapi-audit_policy_information), [GUID delle sottocategorie](https://learn.microsoft.com/en-us/windows/win32/secauthz/auditing-constants)
- [Audit WFP](https://learn.microsoft.com/en-us/windows/win32/fwp/auditing-and-logging)
- [Evento 5157](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-5157), [evento 4625](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-4625)
