# Audit terminologico ProcessLens — R5 → R6

**Data:** 14 ottobre 2026. **Sorgente analizzato:** archivio completo `ProcessLens-v0.19.16-R5-process-identification-clarity-candidate.zip` salvato nella Library dell’utente.

## Ambito e metodo

Censite **3406 voci testuali** in **105 file** (C# applicativo, GUI WPF, JSON, script e test). Le stringhe sono estratte tramite lexer C#, parser XML/JSON e ricerca mirata degli output CLI. Gli snippet a interpolazione possono essere frammentati: l’inventario non è un AST e non rappresenta un numero di messaggi unici o di difetti.

| Indicatore automatico | R5 | R6 |
|---|---:|---:|
| Voci rilevate | 3406 | 3406 |
| File con testi | 105 | 105 |
| Voci con almeno un segnale lessicale | 451 | 431 |
| Di cui segnalazioni prioritarie | 63 | 41 |
| Sostituzioni mirate documentate | — | 34 |

**Avvertenza:** il numero di segnali non certifica quanti errori semantici rimangano; richiede valutazione su ogni percorso di esecuzione. Il registro delle 63 priorità contiene decisione e motivazione specifica, mentre le altre 388 segnalazioni rimangono nello screening con contesto e posizione. Le voci prive di alert non sono automaticamente corrette.

## Standard linguistico

| Area | Criterio d’uso | Errore da evitare |
|---|---|---|
| Identità | Nome PID, percorso e metadati con origine distinta | Trattare il nome commerciale come prova della provenienza |
| Associazione app | Nome genitore/metadata/package con fonte e limiti | Dire "possiede" senza collegamento verificato |
| Funzione | Categoria assegnata dal classificatore e segnale specifico | Spacciare la classificazione per codice attualmente eseguito |
| Avvio | PPID riportato, verifica temporale e catena incompleta | Attribuire intenzioni all’utente o motivo storico senza ETW |
| Attività | CPU per intervallo, finestre, TCP e UDP separati | Dire "sta lavorando" o "è inattivo" da una metrica sola |
| TCP | LISTEN/ESTABLISHED e PID tabella IP Helper | Dire richieste accettate o byte trasferiti senza telemetria |
| Memoria | Private Working Set, Working Set totale, private commit | Sommare WS privato e totale o equiparare commit a RAM fisica |
| Rischio chiusura | Possibili effetti per processi e servizi associati | Dichiarare un impatto garantito non osservato |
| Certezza | Punteggi di regole solo nel pannello tecnico | Presentare punteggi 0–100 come probabilità statistiche |
| Privacy | Metadati limitati in sintesi, dati grezzi su richiesta | Esibire percorsi e argomenti potenzialmente sensibili nel riepilogo |
| Servizi | Stato/configurazione SCM, cronologia separata | Confondere avvio automatico con autore dell’avvio |
| Guardian | Evidenza storica ETW/SQLite con PID e timestamp | Alterare eventi e contratto del servizio senza autorizzazione |

## Riscontri concreti confermati e corretti in R6

- **Node.js con TCP LISTEN:** `RoleClassifier.cs` affermava che il comportamento server fosse "dimostrato". In realtà lo stato TCP non misura richieste, risposte né byte. Corretta descrizione.
- **CPU:** etichette "quasi inattiva" in GUI e risposte query erano incompatibili con un’interpretazione affidabile della sola CPU. Ora descrivono il valore misurato senza dedurre inattività.
- **Deducibilità di un blocco:** una connessione `ESTABLISHED` o un campione CPU non escludono blocchi parziali del programma. Corretta la risposta del question engine.
- **Causa storica di avvio:** molte frasi "è attivo perché" derivavano da identità, finestra o PPID, non da una prova dell’autore e del motivo dell’avvio. Diversi percorsi di fallback corretti.
- **Etichette GUI:** corretti titoli e classificazioni per esplicitare "tipo suggerito" e "avvio e limiti", con testo breve per evitare il precedente overflow.
- **Glossario:** mantenute le 95 voci editoriali come riferimento. Le definizioni del glossario sono proposte contestuali, non un elenco di terminologie tutte certificate da Microsoft.

## Casi ancora aperti per verifica semantica e UX Windows

- `src/ProcessLens.App/MainWindow.xaml.cs:630` — $"Punteggio euristico interno: {i.RoleConfidencePercent}/100 (non è una probabilità)\n — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.App/MainWindow.xaml.cs:702` — $"Punteggi regole (non probabilità): identità {i.IdentityConfidencePercent}/100, app associata {i.OwnershipConfiden — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Core/ProcessDetailPresentation.cs:150` — Punteggio assegnato dalle regole — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Core/ProcessDetailPresentation.cs:151` — $"{insight.RoleConfidencePercent}/100 (non è una probabilità statistica) — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Core/ServiceExplanation.cs:25` — Potrebbe fornire funzionalità a Windows o ad altre applicazioni. Lo stato del servizio e le dipendenze dichiarate n — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/ProcessBehaviorExplainer.cs:43` — $"Classificatore locale: punteggio regole {i.RoleConfidencePercent}/100 sulla funzione. Non è una probabilità stati — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/ProcessQueryEngine.cs:542` — La CPU è rimasta molto bassa nei campioni disponibili, ma questo NON dimostra che il processo sia bloccato: — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/ProcessQueryEngine.cs:714` — Puoi chiedere, per esempio: “Cosa sta facendo?”, “A che punto è?”, “Quando finirà?”, “È bloccato?”, “Ha porte apert — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:118` — Componente interno; ruolo specifico non dimostrato — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:157` — Windows associa una finestra top-level visibile direttamente a questo PID. Il ruolo interno oltre alla UI non è ded — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:205` — Una build o IDE .NET lo ha avviato e può lasciarlo residente per riuso. — Il motivo dell’avvio o l’associazione a una app potrebbe non essere osservabile.
- `src/ProcessLens.Semantics/RoleClassifier.cs:217` — Il runtime .NET è confermato e Windows osserva socket TCP LISTEN attribuite al PID. — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:222` — Il CLR è confermato e Windows associa una finestra top-level al PID. — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:237` — Processo figlio creato dal meccanismo multiprocessing di CPython. Il compito concreto dipende dal programma che lo  — Il motivo dell’avvio o l’associazione a una app potrebbe non essere osservabile.
- `src/ProcessLens.Semantics/RoleClassifier.cs:256` — $"Avviato da {p.ParentName} per eseguire il modulo {module}. — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:277` — $"È stato avviato per eseguire {filename}. — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:277` — $"Avviato da {p.ParentName} per eseguire {filename}. — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.Semantics/RoleClassifier.cs:303` — $"{owner.Owner} lo ha avviato come tool JavaScript/TypeScript. — Il motivo dell’avvio o l’associazione a una app potrebbe non essere osservabile.
- `src/ProcessLens.Semantics/RoleClassifier.cs:324` — $"È attiva per eseguire {Path.GetFileName(path)}. — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.
- `src/ProcessLens.App/MainWindow.xaml:525` — Chi lo ha avviato? Da dove è partito? — Il segnale lessicale non basta a dichiarare un errore; richiede valutazione del percorso che mostra la stringa.

## Parti da non cambiare senza ulteriore autorizzazione e test

- `Guardian`: schema JSON/SQLite, eventi ETW, relazioni temporali, servizio, riarmo e retention.
- `MemoryAgent` e profiler: policy di consenso e soglie, niente acquisizione automatica.
- `ProcessLens.Windows`: contatori nativi e logica dei dati non disponibili.
- Identificativi `knowledge:`, nomi dei campi API, codici diagnostici, SQL `PRAGMA query_only=ON`, manifesti storici e contratti dei test.

## Prove di riferimento esterne

- Microsoft, `PROCESS_MEMORY_COUNTERS_EX2` — PrivateWorkingSetSize / WorkingSetSize / PrivateUsage: https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2
- Microsoft, `MIB_TCPROW_OWNER_PID` — stato TCP e PID: https://learn.microsoft.com/en-us/windows/win32/api/tcpmib/ns-tcpmib-mib_tcprow_owner_pid
- Microsoft, Create processes: https://learn.microsoft.com/en-us/windows/win32/procthread/creating-processes
- Microsoft, UpdateProcThreadAttribute (parent process override): https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute

## Validazione necessaria prima della promozione

1. `dotnet run --project .\tests\ProcessLens.TerminologyTests\ProcessLens.TerminologyTests.csproj -c Release`
2. `dotnet run --project .\tests\ProcessLens.BehaviorTests\ProcessLens.BehaviorTests.csproj -c Release`
3. `AVVIA-TUTTO.cmd` e suite completa, senza alterare Guardian.
4. Screenshot e leggibilità a 100%, 125%, 150% DPI; schermate Python/Conda, Node, Chromium, servizi, memoria e rete.
5. Confronto Colab/Work R5→R6 e nuova revisione delle stringhe in contesto.

**Limite della presente revisione:** il censimento è esteso a tutti i file testuali individuati, ma non equivale alla validazione manuale in GUI delle 3.406 voci. Le 63 segnalazioni di priorità sono state classificate; l'analisi completa del comportamento richiede build Windows e scenari reali.
