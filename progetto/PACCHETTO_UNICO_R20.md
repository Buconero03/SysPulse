# ProcessLens R20 — pacchetto offline Windows x64

R20 e il pacchetto di installazione della candidata R19. Il codice dell'applicazione resta identico alla R19, inclusi indicatore RAM e nuova schermata Sistema Windows. La foto allegata mostra la schermata iniziale della cattura: non contiene un errore da cui si possa identificare una dipendenza mancante.

## Uso

1. Chiudi ProcessLens. Estrai tutto lo ZIP in una nuova cartella con percorso breve, ad esempio `D:\ProcessLens-R20`. Non avviare i file direttamente dallo ZIP.
2. Doppio clic su `INSTALLA_PROCESSLENS.cmd`. Lascia terminare test, compilazione WPF e pubblicazione. Non serve scaricare separatamente .NET o i pacchetti NuGet. Prevedi circa 3 GiB di spazio libero per SDK, cache e compilazione.
3. Apri ProcessLens dal menu Start oppure usa `AVVIA_PROCESSLENS.cmd`. Windows mostra la richiesta UAC prevista dal programma.
4. Per i problemi delle sezioni Rete esegui `DIAGNOSTICA_RETE.cmd`. Il rapporto indica il percorso di `rete-prerequisiti.txt`; puoi inviare quel file per individuare il blocco specifico.

Il pacchetto comprende i sorgenti completi, .NET SDK 8.0.426 Windows x64 con Desktop Runtime 8.0.32 e 64 pacchetti NuGet con le dipendenze per net8.0/net8.0-windows7.0. Il manifest registra gli hash. L'hash SHA512 dell'archivio SDK coincide con quello pubblicato da Microsoft e viene controllato nuovamente all'installazione.

La copia privata di .NET si trova in `%LOCALAPPDATA%\Programs\ProcessLens\R20`; l'ambiente viene configurato solo nei processi avviati dagli script. Non viene cambiato il PATH dell'utente o della macchina. Il runtime include WPF; Visual Studio non serve. I collegamenti sono nel menu Start dell'utente. L'installer non attiva Guardian o MemoryAgent e non installa servizi, driver o attività pianificate.

Il progetto estratto riceve un `NuGet.Config` locale con una sola origine, la cartella NuGet inclusa. Le impostazioni NuGet globali non cambiano. Durante questa installazione offline non si esegue l'audit online delle vulnerabilita NuGet e si usa la verifica offline della revoca certificati. Nessun esito di sicurezza e dedotto da queste impostazioni.

L'installer **compila sul PC Windows**: questo ZIP non contiene una nuova GUI precompilata e qualificata qui. Se un test fallisce, l'installazione si interrompe e conserva il log; non vengono ignorati test per ottenere un eseguibile. I log sono in `%LOCALAPPDATA%\ProcessLens\logs`.

## Perche le sezioni possono non mostrare dati

| Funzione | Requisiti reali | Controllo nel pacchetto |
| --- | --- | --- |
| Leggi reti Wi-Fi | Scheda/driver WLAN, servizio WLAN AutoConfig, eventuale consenso Posizione | Stato servizio, interfacce di rete, WlanOpenHandle e WlanEnumInterfaces; istruzioni per Posizione |
| Cattura IPv4 locale | IPv4 locale valido e privilegi amministrativi; traffico visibile al PC | Conteggio interfacce/indirizzi IPv4 e stato amministratore; nessuna cattura automatica |
| Cattura PktMon | Export API esposti dalla versione di Windows e privilegi richiesti | Verifica dei dieci export richiesti nella DLL di sistema; non avvia il motore |
| Segnali Windows | Accesso al registro Security e criteri di audit pertinenti | Prova di lettura senza salvare eventi; auditpol esclusivamente /get |

Un installer non aggiunge una scheda Wi-Fi, non concede il consenso Posizione e non rende disponibili export assenti dal sistema operativo. La diagnostica non verifica il consenso effettivo per la cache BSS: per quello serve l'esito del pulsante Leggi reti Wi-Fi. I driver vanno ottenuti dal produttore della scheda; non sono inclusi driver generici.

Queste sezioni usano API Windows: Wireshark, TShark e Npcap non sono dipendenze della vista attuale. Installarli non correggerebbe una lettura WLAN negata o l'accesso a Security. Il pacchetto non cambia automaticamente i criteri di audit e non copia DLL di Windows da altri computer.

Dopo l'installazione: Rete > Cattura Windows / Wi-Fi > Elenca sorgenti Windows, seleziona la sorgente e avvia la cattura. Per la lettura Wi-Fi premi Leggi reti Wi-Fi e consulta Cache radio Wi-Fi. Segnali Windows ha il suo pulsante di lettura separato. Gli elenchi vuoti e i dati N/D non certificano l'assenza di dispositivi o tentativi di accesso.

## Verifiche di questo pacchetto

Eseguiti su Linux: sintassi PowerShell, fixture di integrita e percorsi, controllo tipi C# della diagnostica, chiusura delle dipendenze tramite NuGet.Frameworks, CRC degli archivi, hash ufficiale SDK e manifest completo. Le prove precedenti della R19 restano archiviate con i loro limiti.

**Non eseguiti qui:** installazione Windows, Windows PowerShell 5.1, restore effettivo con MSBuild su Windows, build completa WPF, driver, consenso Posizione e letture reali di rete/Security. La verifica completa avviene sul PC destinazione; il pacchetto non puo dichiarare gia risolto il problema di rilevamento mostrato.

## Fonti

- Microsoft, installazione .NET su Windows: https://learn.microsoft.com/dotnet/core/install/windows
- Metadati ufficiali .NET 8: https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json
- Microsoft, accesso Wi-Fi e Posizione: https://learn.microsoft.com/windows/win32/nativewifi/wi-fi-access-location-changes
- Microsoft, PacketMonitorInitialize: https://learn.microsoft.com/windows/win32/pktmon/packetmonitor/nf-packetmonitor-packetmonitorinitialize
- Microsoft, auditpol get: https://learn.microsoft.com/windows-server/administration/windows-commands/auditpol-get
- Pacchetti originali NuGet: https://api.nuget.org/v3-flatcontainer/

Licenze .NET e avvisi di terze parti sono conservati nell'archivio SDK originale. Le licenze dei pacchetti restano nei rispettivi nupkg; questo bundle non modifica i binari di terze parti.
