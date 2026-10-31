# ProcessLens v0.17.2 — Interfaccia richieste e diagnostica RAM

## Modifiche
- Menu a tendina **nativo WPF** «Analisi approfondita» aperto anche con il passaggio del mouse; normale clic e tastiera restano disponibili.
- Voci raggruppate per funzione: informazioni, risorse, origine e sicurezza; il menu rimpiazza i nove pulsanti.
- Domande libere rimangono disponibili. Selezionare un elemento esegue l'analisi *on-demand*, solo per il PID selezionato.
- Rapporto RAM in sezioni espandibili con **valutazione motivata** e parametri distinti (RAM privata, Working Set totale, commit privato, mappa virtuale).
- La valutazione aggiunge la RAM fisica totale/disponibile di Windows (GlobalMemoryStatusEx) e confronta l'andamento sulla stessa istanza del processo.
- Stato **DA MONITORARE / TENDENZA STABILE / NESSUN SEGNALE DI CRESCITA CONTINUA / VALUTAZIONE PARZIALE**; non pretende benchmark specifici non misurati.
- Esplicitato che pressione generale all'85% non prova che un PID sia la causa.

## Limiti
Le cause interne (heap, cache, DOM, buffer) sono esempi plausibili basati sul ruolo, **non** misure attribuibili a oggetti concreti. Un vero memory leak richiede campionamenti prolungati e/o profiler del runtime.

## Build
`AVVIA-TUTTO.cmd` oppure `./build-dashboard.ps1` da PowerShell; GUI Windows pubblicata in `publish/win-x64`.
Non aggiorna Guardian né le notifiche RAM esistenti.
