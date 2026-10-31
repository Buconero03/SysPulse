# ProcessLens v0.9.0 — CPU Intelligence

## Cambiamenti
- CPU globale via `GetSystemTimes` (kernel + user - idle); si mostra solo dopo almeno 2 campioni validi.
- CPU per PID = differenza di `Process.TotalProcessorTime` / differenza dei tempi CPU aggregati di Windows.
- Percentuali per l’intera capacità del computer, non per un singolo core: con 12 CPU logiche un core interamente usato vale circa 8,33%.
- N/D quando la lettura non è accessibile o non esiste ancora un intervallo affidabile; non viene confusa con 0%.
- Media pesata temporalmente e picco degli ultimi 30 secondi, grafico campionato ogni ~2 secondi, tempo CPU accumulato.
- Sotto-processi aggregati tramite catena di antenati PID verificata temporalmente. Non si sommano processi estranei solo perché hanno lo stesso nome.
- Python: distinguere moduli `-m`, kernel Jupyter, `multiprocessing` worker, `-c`, file `.py` e processi in ascolto TCP senza inventare l'attività interna.
- Query `Perché usa CPU?` con separazione tra misurazione e causa possibile.
- Costi: nessuna ETW globale attiva, nessun profiler thread-stack continuo, nessuna lettura del file Python eseguito. Deep inspection solo sul PID selezionato.

## Limiti importanti
- Non si possono identificare automaticamente le istruzioni Python attualmente in esecuzione solo tramite CPU, nome del file, DLL o porte. Per uno stack attuale servono strumenti di profiling/strumentazione con costi/permessi maggiori.
- `GetSystemTimes` e Task Manager possono differire per finestra di mediazione, processor groups, virtualizzazione e tempo dello snapshot.
- Il campionamento necessita almeno di un intervallo completo; il primo refresh riporta N/D.
- La media e il picco riguardano la finestra osservata: nei primi 30 s non c'è ancora una storia completa.
- Senza privilegi adeguati alcuni processi protetti restano N/D; non forzare l'elevazione per il monitoraggio ordinario.
- Lo stesso processo può accumulare tempo CPU di thread paralleli; il tempo CPU cumulativo non equivale al tempo trascorso dall'avvio.

## Verifica in Windows
```
.\test-cpu.ps1
.\bootstrap.ps1
```

Confronto suggerito: avvia Windows Task Manager, cerca `python`, abbina lo stesso PID tramite scheda Dettagli e osserva il valore medio su 10–20 s. I numeri non devono coincidere esattamente a ogni secondo, ma dovrebbero essere dello stesso ordine di grandezza e rispondere allo stesso carico.
