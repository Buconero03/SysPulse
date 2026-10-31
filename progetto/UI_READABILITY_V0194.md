# ProcessLens v0.19.4 — leggibilita' GUI (candidata)

Base: v0.19.3. Conservati i miglioramenti ai log e la compatibilita' gcdump 10.

## Interventi
- Finestra centrata con dimensioni compatibili con schermi piu' piccoli e adattamento all'area di lavoro.
- Navigazione/filtri separati dagli indicatori di sistema, senza sovrapposizione.
- Lista processi con titolo, badge, spiegazione, origine e tempi separati; testo a capo e tooltip sul nome lungo.
- Intestazione della lista con ordinamento su una riga distinta.
- Evidenze descritte in italiano piu' naturale e con righe separate; gli ID delle regole restano nei dati diagnostici, non nella sintesi.
- Descrizione del crash handler senza gergo inglese superfluo.
- Regressioni semantiche BehaviorTests e preflight PowerShell check-ui-readability.ps1 integrato in build-dashboard.ps1.

## Controlli e limiti
La presente versione non e' stata compilata sul computer Windows dell'utente: va validata con AVVIA-TUTTO.cmd.
I test locali si limitano a controlli statici e lettura dei file. Verificare layout Windows a 100%, 125%, 150% DPI e in finestra ridimensionata.
Non eseguire l'installazione o aggiornamento di Guardian per questo intervento.
Nessuna modifica a Guardian, SQLite, RAM policy, raccolta heap o protocolli dei log.
