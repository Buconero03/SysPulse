# ProcessLens v0.17.1 — Menu domande e rapporto RAM strutturato

## Interfaccia WPF
- Rimossi nove pulsanti ripetuti nella sezione «Chiedi a ProcessLens».
- Un `ComboBox` offre le stesse nove azioni; quando selezioni una domanda, l'analisi parte automaticamente. Dopo la selezione il menu torna sul segnaposto, permettendo di ripetere la stessa domanda. La casella per le domande libere rimane disponibile.
- Il menu viene disattivato durante l'analisi per evitare interrogazioni simultanee.
- Le risposte sulla RAM mostrano subito la sintesi e raggruppano le sezioni tecniche in pannelli `Expander` apribili singolarmente; la sezione delle misure è aperta per impostazione predefinita. Non si esegue HTML.

## Analisi RAM: criteri e limiti
- Sintesi iniziale con nome processo, PID, metrica fisica disponibile e trend verificato.
- Working Set totale, Private Working Set e Private Commit sono descritti distintamente: non sono valori da sommare.
- Variazione temporale con numero campioni, durata, delta, velocità descrittiva, massimo e minimo; non si dichiara un memory leak sulla sola crescita.
- La mappa `VirtualQueryEx` indica regioni MEM_COMMIT e non è interpretata come RAM residente o heap.
- Possibili ragioni per runtime/ruolo soltanto quando riconoscibile; sono ipotesi, non allocazioni osservate. Nessun comando sensibile viene stampato.
- Azioni di approfondimento dipendenti dal runtime e avvertenze contro svuotamenti forzati e attribuzioni ingiustificate.

## Test
`build-dashboard.ps1` esegue una verifica di accoppiamento XAML/handler e i test `ProcessLens.BehaviorTests` con casi sintetici. La build WPF effettiva richiede Windows + .NET 8 SDK.

## Compatibilità
Nessuna modifica a SQLite, Guardian Windows Service, avviso RAM 85/80 o attivazione automatica. `AVVIA-TUTTO.cmd` e `AVVIA-VELOCE.cmd` conservano il flusso precedente.
