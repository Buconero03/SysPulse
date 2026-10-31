# R10 R5 — riduzione a icona, chiusura nella tray e riapertura

La candidata riprende la R10 R4. L'utente segnala che la finestra non torna visibile dopo la riduzione e chiede che la X riduca il programma invece di terminarlo. La schermata allegata mostra l'avviso RAM; da sola non stabilisce lo stato della finestra principale o la causa della mancata riapertura. L'avviso appartiene al percorso di allarme RAM, che non viene modificato.

| Comando | Comportamento previsto |
|---|---|
| Pulsante – della finestra | Minimizza normalmente nella barra delle applicazioni |
| Clic sulla finestra minimizzata nella barra | Ripristino standard di Windows |
| X o Alt+F4 | Nasconde la finestra nell'area di notifica; ProcessLens resta avviato |
| Clic sull'icona ProcessLens vicino all'orologio | Riapre la finestra |
| Tasto destro sull'icona → Apri ProcessLens | Riapre la finestra |
| Nuovo avvio con l'istanza già attiva | Messaggio alla finestra esistente e ripristino; nessuna seconda GUI |
| Tasto destro sull'icona → Esci da ProcessLens | Chiude realmente la GUI e rilascia l'icona |

Windows può collocare l'icona fra quelle nascoste nel menu **∧** vicino all'orologio. L'icona usa il simbolo applicazione condiviso di Windows e il tooltip ProcessLens. La posizione e la visibilità dell'icona nell'area principale sono decise dalla Shell e dalle preferenze dell'utente.

## Correzione e ripristino

Il ramo precedente non implementava l'icona nell'area di notifica; la riattivazione cercava `Process.MainWindowHandle`, che non è un percorso affidabile per finestre nascoste. La nuova GUI registra una richiesta di ripristino sulla propria finestra nativa. Il secondo avvio enumera le finestre di primo livello, incluse quelle nascoste, cerca il titolo ProcessLens e invia tale richiesta con un timeout, richiedendo un riscontro. Il destinatario accoda il ripristino sul Dispatcher, mostra la stessa finestra, ripristina lo stato normale o massimizzato precedente e richiede l'attivazione. Il riscontro conferma la presa in carico, non una verifica visiva del foreground. L'accodamento evita di chiamare Show in modo rientrante mentre WPF sta elaborando Closing. Il percorso precedente resta come fallback per un'istanza di una revisione più vecchia, limitato al titolo esatto ProcessLens per non selezionare un altro dialogo ospitato da dotnet.

L'istanza primaria usa `OnExplicitShutdown`. Il controller intercetta la X e annulla la chiusura; Esci, fine sessione Windows ed errori fatali consentono invece la chiusura effettiva. Il controller è creato soltanto da App all'avvio reale. Costruire MainWindow nella suite WPF non aggiunge icone e non cambia il comportamento Close del test.

L'icona viene registrata tramite `Shell_NotifyIconW`; clic, menu e richiesta di ripristino arrivano all'hook della finestra WPF. Alla ricreazione della taskbar si tenta di aggiungerla nuovamente. Se la registrazione fallisce, la X minimizza nella barra delle applicazioni anziché nascondere l'unico modo di riapertura. Se la taskbar viene ricreata e l'icona non può essere ripristinata mentre la finestra è nascosta, la finestra viene resa nuovamente visibile. L'icona viene rimossa all'uscita; l'icona di sistema condivisa non viene distrutta.

Limitazioni: il protocollo di ripristino richiede la stessa sessione/desktop e può essere soggetto alle restrizioni di integrità di Windows, per esempio avvio non elevato contro istanza elevata. Non viene richiesta elevazione automatica. Un'istanza bloccata che non risponde entro il timeout non viene dichiarata riattivata. Le schermate ricevute non dimostrano l'esecuzione delle build precedenti e il comportamento reale della Shell richiede verifica Windows.

## Attività e componenti

Le dashboard GUI già create vengono disattivate quando la finestra è nascosta o minimizzata e riattivate solo nella scheda pertinente quando torna visibile. Il polling principale aveva già il controllo IsVisible/WindowState. Non si introducono timer di polling per la tray. Le operazioni già in corso non vengono terminate per il solo nascondimento della finestra.

Collector, Core, Semantics, Guardian, MemoryAgent, profiler, regole di allarme RAM, osservatore di uscita R4 e terminazione nativa restano invariati. Esci chiude la GUI e le sue risorse; non disinstalla o termina Guardian e MemoryAgent. Non viene promessa né misurata una riduzione RAM.

## Verifica

Eseguiti qui: controlli strutturali Python/XML, regressioni portabili già presenti, confronto byte dei componenti conservati, replay reale della patch, ZIP e manifest SHA-256. Test puri aggiunti alla suite esistente per X con tray, fallback senza tray, uscita esplicita e sospensione/ripresa del polling GUI.

**NON ESEGUITO:** compilazione e test .NET, PowerShell, tray reale, messaggi nativi di ripristino, menu con mouse/tastiera, session ending, Explorer restart, DPI e focus. Ambiente Linux senza dotnet/pwsh. Nessun programma dell'utente, processo o servizio è stato avviato, fermato o riavviato qui. Le verifiche di sorgente non sostituiscono questi scenari.

Prima della build uscire dalla vecchia GUI: la pubblicazione già esistente rifiuta di sostituire i file mentre ProcessLens è in esecuzione. Usare la chiusura normale della vecchia revisione; nella R5 usare Esci da ProcessLens. Non occorrono chiusure forzate automatiche. Poi estrarre in una cartella nuova:

```powershell
.\build-dashboard.ps1
```

Se riesce, avviare la nuova candidata. Una vecchia istanza rimasta avviata verrebbe riattivata dal protocollo single-instance invece di aprire la nuova GUI.

```powershell
.\run-release.ps1
```

Verifiche manuali: –/ripristino; X/clic tray; menu Apri/Esci; X da massimizzato e ripristino; nuovo avvio da minimizzato e da nascosto; mantenimento PID della GUI; sospensione/ripresa delle dashboard. Il riavvio di Explorer e la fine sessione sono scenari manuali da eseguire solo quando opportuno, non azioni della suite.

Audit corrente `audit/R10_R5/`, con prima/dopo e registro stringhe. Manifest corrente `SHA256SUMS_R10.txt`. Una sola radice del progetto; revisioni precedenti storiche e criticità RAM aperte conservate.

Fonti primarie:

- https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw
- https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataw
- https://learn.microsoft.com/en-us/windows/win32/shell/taskbar
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.shutdownmode
