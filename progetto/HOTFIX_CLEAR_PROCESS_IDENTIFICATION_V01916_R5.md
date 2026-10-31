# ProcessLens v0.19.16 R5 — identificazione comprensibile

## Motivazione
La scheda di riepilogo mostrava i giudizi «indizi forti» e «indizi molto forti» come se potessero spiegare i processi, senza indicare i dati concreti utilizzati. La voce «funzione classificata» non spiegava alcun comportamento osservato. Le etichette su tre colonne risultavano inoltre troncate alle dimensioni reali della GUI.

## Modifiche
- Scheda principale: app collegata **(stima)** e tipo di processo **suggerito**, due colonne a capo automatico.
- Sostituzione della classifica astratta a tre colonne con tre frasi distinte: dato Windows (nome eseguibile/PID), motivo del collegamento all'app (incluso il caso del padre con nome corrispondente) e significato del tipo suggerito.
- Punteggi e ID di regole rimangono esclusivamente nel rapporto tecnico espandibile.
- Provenienza: «orari di avvio compatibili» invece di «orari di creazione coerenti»; nessuna affermazione che la relazione padre/figlio provi da sola l'intenzione o il tipo di operazione.
- Test di regressione per l'esempio Python/conda, provenienza non verificata e leggibilità UI.
- Nessun cambio al collector, alle letture di memoria o ai componenti Guardian, MemoryAgent, profiler e servizi.

## Limiti
Queste modifiche riguardano presentazione e provenienza; non provano che conda controlli effettivamente il processo né quale script o operazione stia eseguendo. Il pacchetto deve essere compilato e testato con .NET/WPF su Windows: i controlli statici non sostituiscono tale verifica.
