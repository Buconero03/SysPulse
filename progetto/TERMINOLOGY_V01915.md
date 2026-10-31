# ProcessLens v0.19.15 — Glossario controllato e ripetizioni

Sorgente base: ProcessLens v0.19.14. Nessuna riscrittura, nessuna modifica a Guardian o MemoryAgent.

## Modifiche mirate
- Rimossi i quattro prefissi ripetuti «intensità degli indizi» dalle prove del classificatore; ogni voce conserva il valore originale dell'evidenza, ora sotto etichette specifiche.
- Distinti funzione classificata e attività osservata. Il punteggio euristico rimane nel pannello tecnico come valore 0–100, non come probabilità.
- Rinominate categorie GUI che non dimostrano una relazione parentale.
- Ridotte le spiegazioni duplicate nei pannelli di classificazione e resi espliciti i dati utilizzati.
- Aggiornati i test di comportamento e terminologia, senza rimuoverli.

## Vocabolario
`docs/terminology/GLOSSARIO_TERMINOLOGICO_PROCESSLENS.csv`: 95 voci di ricerca editoriale da utilizzare come riferimento. Sono proposte di terminologia da validare rispetto ai segnali disponibili, **non** denominazioni ufficiali di Microsoft.

## Validazione
Eseguire su Windows 11 x64 `AVVIA-TUTTO.cmd` dalla cartella estratta. Il progetto NON è stato compilato in questo ambiente; non usare la release per profili reali prima della build e dei test.

In Google Colab caricare direttamente questo ZIP completo nella cella «Carica lo ZIP del tuo progetto», non il pacchetto Glossario/Ricerca, e rieseguire le celle dell'audit.
