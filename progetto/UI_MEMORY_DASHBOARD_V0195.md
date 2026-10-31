# ProcessLens v0.19.5 — dashboard Memoria RAM (candidata)

Base: v0.19.4, senza riscritture. `AVVIA-TUTTO.cmd` e Guardian non modificati.

## Modifiche
- Riorganizzata la vista in stato RAM, configurazione avvisi e classifica processi.
- Tre metriche con unità coerenti: percentuale e GiB in uso, GiB disponibili, GiB totali.
- Barra RAM disegnata da un template WPF: stesso colore dello stato, non la barra verde predefinita.
- Etichette visuali: <75 ordinario, 75–<85 osservare, 85–<90 elevato, >=90 quasi saturo.
  Queste soglie UI non sostituiscono la policy notifiche 85/80.
- Descrizioni prudenti: sola percentuale non prova paging, blocchi, memory leak o responsabilità del processo.
- Avvisi più compatti: una chiave `Run` prova un avvio configurato, non un agente in esecuzione.
- Classifica massimo 20 letture ordinate per RAM residente discendente; non sommare valori con pagine condivise.
- Grid più leggibile e con più spazio, colonne numeriche allineate a destra e scorrimento.
- Messaggi di errore su refresh rendono esplicito che i valori visibili possono essere vecchi.
- Test aggiunti: `check-memory-ui-v0195.ps1` durante build e verifiche deterministiche delle soglie in MemoryTests.

## Verifiche richieste su Windows
- `AVVIA-TUTTO.cmd` senza esecuzione amministrativa manuale.
- Test MemoryTests e compilazione GUI; verificare la barra rossa con RAM >=90%, arancio >=85%, verde <75%.
- Controllare scheda a 100%/125%/150% di scaling, ridimensionamento e lista 20 righe.
- Verificare agent status con chiave Run configurata e non configurata: non indica il processo attivo.
- Guardian e database precedenti non vanno modificati o reinstallati.

## Limitazioni
- Nessuna compilazione WPF Windows o test grafico eseguito in questo ambiente.
- Il prototipo profiler .NET conserva i suoi limiti e resta da validare su PID .NET non critico.
