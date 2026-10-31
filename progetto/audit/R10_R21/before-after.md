# R20 / R21 — distribuzione leggera

| Voce | R20 offline | R21 leggero |
| --- | --- | --- |
| SDK nell'archivio | 285.292.840 byte | Escluso |
| NuGet nell'archivio | 46.294.995 byte | Escluso |
| Avvio quotidiano | Il comando di installazione poteva essere scambiato per avvio | Un solo AVVIA_PROCESSLENS.cmd nella radice |
| Primo uso su questo PC | SDK privato, test e compilazione | Riusa l'installazione R20 gia completata |
| Sorgenti | Mescolati ai comandi di installazione | Sottocartella progetto |
| SDK della compilazione | Vincolo esatto 8.0.426 | Vincolo rigido archiviato; il progetto resta net8.0 |
| Test di sviluppo | Lanciati dall'installer | Restano nel progetto; l'avvio non li esegue |
| Codice applicativo | R19 | Byte identici alla R19 contenuta nella R20 |

La R21 non contiene una nuova GUI precompilata. Il comando riconosce una pubblicazione locale gia compilata o avvia la copia che il log Windows dell'utente conferma installata dalla R20. Non aggiunge timer, monitor, catture o processi di sviluppo residenti.

Il peso del file ZIP non misura la RAM del processo. Non sono state eseguite nuove misure del consumo dell'applicazione, prove UAC o avvii reali Windows.

Il pacchetto R20 originale rimane la base verificabile. I sette file ritirati sono archiviati senza modificarli sotto r20-original; gli archivi binari di terze parti sono esclusi e restano descritti nel registro downloads della R20.
