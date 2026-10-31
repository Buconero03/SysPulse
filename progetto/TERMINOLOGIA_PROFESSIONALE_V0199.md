# ProcessLens v0.19.9 — Standard terminologico

La schermata principale deve dire **quale funzione è stata classificata**, **su quali informazioni** e **cosa resta indeterminato**. Non confondere classificazione euristica con osservazione in tempo reale.

## Criteri

1. Interfaccia: nomi brevi, italiano naturale, terminologia coerente con Windows.
2. Contenuti: una frase chiara per scheda; niente ripetizioni dell'identica funzione.
3. Evidenze: separare le informazioni raccolte dalle deduzioni. Il punteggio 0–100 è interno e non è una probabilità.
4. I parametri dell'applicazione (`--type`, `--utility-sub-type`) restano nel pannello **Dettagli tecnici della classificazione**.
5. Evitare attribuzioni di intenzione: un processo padre indica una relazione osservata, non il motivo esatto dell'esecuzione.

| Prima | Dopo |
|---|---|
| Esegue codice Node/JavaScript in un processo utility isolato | Esegue componenti Node.js in un processo separato |
| Servizio Node/JavaScript | Processo ausiliario Node.js |
| Come funziona | Descrizione |
| Come è stato riconosciuto | Elementi di identificazione |
| Perché può essere in esecuzione | Informazioni sull'avvio |
| Funzione riconosciuta da indizi | Funzione identificata |

## Verifiche

Test unitari `ProcessLens.TerminologyTests` ampliati con un processo Chromium/Electron di tipo `utility` che dichiara `node.mojom.NodeService`. Controlli XAML/PowerShell aggiornati per le etichette nuove.

## Conservazione dei confini

Non sono state alterate la frequenza di campionamento v0.19.8, le API di misura, le regole Guardian o il database storico, la policy 85/80 e il profiler separato. `AVVIA-TUTTO.cmd` e `AVVIA-VELOCE.cmd` restano invariati.

## Riferimenti terminologici

- Microsoft Windows Apps, Writing style: https://learn.microsoft.com/en-us/windows/apps/design/style/writing-style
- Microsoft Win32, User Interface Text: https://learn.microsoft.com/en-us/windows/win32/uxguide/text-ui
- Microsoft Win32, Layout: https://learn.microsoft.com/en-us/windows/win32/uxguide/vis-layout
- Electron, utilityProcess: https://www.electronjs.org/docs/latest/api/utility-process
