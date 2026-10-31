# R17 → R18

| Prima | Dopo | Verifica |
| --- | --- | --- |
| Solo API PktMon documentate per la cattura; possibile indisponibilità sulla versione Windows | Sorgenti IP locale Winsock indipendenti e PktMon separate | Socket simulati; nessun socket aperto durante l’enumerazione |
| Il cambio scheda cancellava anche il motivo dell’ultimo esito | Righe/handle rilasciati; riepilogo diagnostico limitato e copiabile conservato | Nuova fixture WPF supplied; esecuzione Windows non effettuata |
| Audit WFP disabilitato relegato nei dettagli sotto zero eventi | Copertura incompleta nel riepilogo, motivo e istruzioni di attivazione manuale | Fixture portabili replicano lo stato della schermata |
| Perdite/componenti/scarti presupposti dal modello PktMon | Flag espliciti di disponibilità per il nuovo collector; campi N/D | Test it-IT/en-US e rapporto effettivo |

Le schermate non riportano il motivo di un errore PktMon: nessuna causa specifica è inventata. La seconda mostra una query con zero eventi e audit WFP disabilitato, non prova di guasto della query. L’abilitazione automatica dell’audit è esclusa; le istruzioni mostrate non vengono eseguite dall’app.

Guardian, MemoryAgent, profiler, contatori CPU/RAM e acquisizioni precedenti conservati byte per byte fuori dal perimetro R18. Non è ricominciata l’analisi terminologica già completata.
