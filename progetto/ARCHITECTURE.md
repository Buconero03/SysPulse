# ProcessLens v0.5 — Generic + Deep Architecture

```text
                      ┌──────────────────────────┐
                      │ Windows process snapshot │
                      └────────────┬─────────────┘
                                   │
                        cheap/global evidence
                                   │
              ┌────────────────────▼───────────────────┐
              │ Identity -> Framework -> Owner -> Role │
              └────────────────────┬───────────────────┘
                                   │
                           process list / cache
                                   │
                             user selects PID
                                   │
              ┌────────────────────▼───────────────────┐
              │       Windows Deep Inspector           │
              │                                        │
              │ AUMID / windows / modules / TCP / UDP  │
              └────────────────────┬───────────────────┘
                                   │
                        enriched evidence snapshot
                                   │
              ┌────────────────────▼───────────────────┐
              │ Re-run generic semantic pipeline       │
              └────────────────────┬───────────────────┘
                                   │
                       precise explanation + proof
```

## Core rule

A product-specific name is never required for baseline classification. Windows evidence has priority. Product knowledge may later enrich wording, but it must not override stronger operating-system evidence.

## Evidence strength examples

Strong/direct:
- Windows Service Control Manager maps PID -> service;
- Chromium `--type=renderer` / `--type=gpu-process`;
- explicit script/JAR/.NET assembly in command line;
- TCP socket in LISTEN state owned by the PID;
- AUMID/package identity;
- top-level window owned by the PID.

Supporting/probabilistic:
- loaded modules associated with a framework;
- parent/child topology;
- executable/product/file-description naming signals;
- established network connections without a declared service role.

## Performance rule

Do not deep-inspect all processes. Global refresh must remain cheap. Expensive inspection is demand-driven for the selected PID.


## Knowledge Engine v0.6

Pipeline aggiuntiva:

1. Windows raccoglie i moduli/DLL solo per il PID selezionato.
2. Le firme JSON assegnano punteggi alle evidenze osservate.
3. Framework e ruolo vengono risolti separatamente.
4. Una singola DLL debole non basta: le regole possono richiedere piu' evidenze.
5. Le euristiche hardcoded rimangono fallback, non sono la fonte primaria.

La knowledge base e' generica: `coreclr.dll`, `jvm.dll`, `python3*.dll`,
`node.dll`, `chrome_elf.dll`, `libcef.dll`, `Qt*Core.dll`, ecc. identificano
tecnologie; command line/rete/finestre/process tree aiutano a determinare il ruolo.

## v0.7 — Process Query Engine

Il livello di query è sopra il motore semantico, non lo sostituisce:

```text
Domanda utente
   -> Intent parser
   -> PID selezionato
   -> evidence snapshot / deep inspection
   -> progress detector
   -> ETA estimator (solo con almeno due campioni)
   -> answer composer
```

Principi:

1. una risposta deve distinguere osservazione, inferenza e stima;
2. CPU/RAM non sono progresso;
3. un'ETA richiede un totale di lavoro osservabile e velocità misurata;
4. i processi long-running non ricevono ETA artificiale;
5. la query approfondita riguarda solo il PID selezionato;
6. il parser è genericamente basato sull'intento, non su nomi di prodotti.

## v0.7.2 — Network evidence semantics

The engine deliberately separates these observations:

- TCP LISTEN: the PID owns a listening TCP socket/port;
- TCP ESTABLISHED: Windows attributes an established TCP connection to the PID;
- UDP endpoint: Windows attributes a bound UDP endpoint to the PID.

None of these facts alone is renamed to "uses the Internet". A listening socket may be
loopback/local, an established connection may be LAN-only, and multi-process applications
often delegate all network work to a helper PID.

To reduce false negatives without product-specific rules, deep inspection also computes a
**same-executable process family**: the top-most same-name ancestor plus same-name descendants.
Network facts are shown both for the selected PID and for this related family. This is a
generic mechanism for Chromium/Electron-style architectures and other multi-process programs.


## v0.7.2 — Socket ownership graph

`WindowsDeepProcessInspector` mantiene ora endpoint e proprietario PID per la famiglia selezionata. L'aggregazione di famiglia resta solo contesto; l'attribuzione primaria rimane sempre la tabella socket di Windows. I sibling multiprocesso vengono classificati con segnali generici della command line (`renderer`, `gpu-process`, `utility`, `NetworkService`, ecc.) senza regole di prodotto.


## v0.7.3 - Icone native dei processi

La lista processi e l'intestazione del pannello dettagli mostrano ora l'icona associata da Windows all'eseguibile. Le icone vengono risolte tramite Windows Shell (`SHGetFileInfo`) e messe in cache per percorso, così il refresh della lista non interroga continuamente Shell32. Per processi protetti o senza percorso accessibile viene usata l'icona generica associata agli eseguibili.
