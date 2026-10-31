# ProcessLens v0.19.16 R2 — Coerenza dei limiti TCP e test

## Errore osservato su Windows

`ProcessLens.TerminologyTests/Program.cs`, verifica riga 169 della R1: l'asserzione cercava la frase `non risultano misurate richieste` nel campo `WhatIsIt`. Il ruolo generico `role.generic.server-listener` veniva scelto prima del fallback C#, ma nella knowledge base JSON il campo `meaning` non conteneva ancora la precisazione sui limiti di osservazione. Il test della fase 2 si interrompeva con eccezione .NET.

## Modifiche

- `src/ProcessLens.Semantics/knowledge/role-signatures.json`: per lo stato TCP `LISTEN` dichiara che la lettura non misura richieste accettate né byte trasferiti. Distingue lo stato della socket dal motivo dell'avvio.
- `src/ProcessLens.Semantics/RoleClassifier.cs`: medesimo limite nel percorso fallback. Nel fallback GPU Chromium il parametro `--type=gpu-process` e il limite della classificazione sono espliciti, come nella regola JSON.
- `tests/ProcessLens.TerminologyTests/Program.cs`: verifica ruolo, prova diretta della socket, limite sull'attività e rapporto leggibile. Conserva tutti i test `ESTABLISHED`, padre omonimo e GPU già previsti.
- Metadati R2, manifest SHA-256 attivo, intestazione del launcher.

Nessun test disabilitato. Non vengono installati o modificati Guardian, MemoryAgent, profiler o database. La compilazione e l'intera suite su Windows **non sono ancora eseguite** in questo ambiente.

## Uso

Estrarre lo ZIP in nuova directory. Avviare `AVVIA-TUTTO.cmd`; l'intestazione deve indicare `v0.19.16 R2`. Conservare il log fino a pubblicazione completata. La cattura del profiler resta esclusivamente opt-in.
