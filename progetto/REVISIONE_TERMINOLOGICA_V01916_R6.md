# ProcessLens v0.19.16 R6 — revisione terminologica controllata

**Base:** v0.19.16 R5, preservata. **Stato:** candidate, build e test WPF su Windows non eseguiti qui.

## Metodo

Inventario di 3406 voci da 105 file. Segnalazioni lessicali R5: 451 (63 prioritarie); R6: 431 (41 prioritarie). Sono indicatori da verificare, non conteggi di bug.

Applicate 34 sostituzioni controllate, su 8 file, comprese VERSION.txt e intestazione launcher.

## Confini

- Separati fatti registrati da Windows, associazioni basate su regole e limiti della misura.
- Eliminata l'asserzione 'comportamento server dimostrato' dalla classificazione Node.js basata su LISTEN.
- 'CPU quasi inattiva' sostituita con 'CPU misurata molto bassa', senza dedurne inattività generale.
- Evitati diversi 'è attivo perché' quando manca la prova della causa storica.
- Le domande legittime dell'utente, i codici interni, le misure RAM, gli ID, le API e la catena diagnostica sono conservati.
- Guardian, MemoryAgent, Profiler, acquisizione Windows e test esistenti non modificati.

## Convalida

Sono stati eseguiti controlli statici XML/XAML/JSON, integrità ZIP, checksum, confronto dei componenti protetti e verifiche puntuali delle frasi corrette. Non equivalgono a compilazione .NET/WPF né a un test end-to-end Windows.

## Documenti

Il rapporto integrale, con l'inventario dei testi e le decisioni per i casi ad alta priorità, è distribuito separatamente nel pacchetto di audit `ProcessLens_Audit_Integrale_R5_R6.zip`.
