# ProcessLens v0.19.3 — Messaggi diagnostici piu chiari

## Scopo e limiti

- Versione candidata dal sorgente v0.19.2 (che include compatibilita gcdump 10); nessuna build Windows ancora eseguita.
- Solo testi, etichette di log e presentazione degli errori sono stati modificati rispetto alla v0.19.2. Nessun cambiamento ETW, schema SQLite, servizio Guardian, policy di profilazione o soglie RAM.
- Non installare/aggiornare Guardian: `build-dashboard.ps1` compila e pubblica la GUI e i relativi processi, non modifica il servizio installato.

## Dove leggere i log

| Componente | Percorso | Semantica |
|---|---|---|
| GUI | `%LOCALAPPDATA%\ProcessLens\logs\startup.log` | `INFO` per fasi ordinarie; `ERRORE` per eccezioni con tipo, descrizione e dettagli tecnici |
| Audit RAM | `%LOCALAPPDATA%\ProcessLens\logs\memory-analysis.log` | `VERIFICA RAM`, PID, fase, esito e timestamp |
| MemoryAgent | `%LOCALAPPDATA%\ProcessLens\memory-agent.log` | `INFO`, `AVVISO`, `ERRORE`; esplicita soglia, riarmo e lettura non disponibile |
| Build | Output PowerShell `build-dashboard.ps1` | Fasi numerate, test, pubblicazione; la build si ferma quando un controllo fallisce |
| Profilazione manuale | Output `profile-dotnet.ps1` | Stato, motivo, heap GC e oggetti gestiti, con avvertenza sulle misure mancanti |

Nota privacy: le eccezioni nel log di avvio possono contenere percorsi locali o nomi di file. Rivedere il log prima di condividerlo. Nessun log viene inviato automaticamente.

## Esempi di testo (illustrativi, NON un'esecuzione reale)

`2026-10-11T20:00:00+02:00 | AVVISO | MemoryAgent | Soglia RAM raggiunta (85%): uso attuale 86,1%. Avviso richiesto a Windows.`

`[2026-10-11T20:00:00+02:00] | ERRORE | PID=1234 | FASE=Prima lettura processi non riuscita`

`[FASE 3] Test profiler .NET: parser gcdump storico/10, sicurezza e limiti...`

## Verifica su Windows

1. Estrarre l'intero archivio in una nuova cartella, senza sovrascrivere una release funzionante.
2. `powershell -NoProfile -ExecutionPolicy Bypass -File .\build-dashboard.ps1`
3. Verificare test RAM e profiler, compilazione WPF e pubblicazione; annotare eventuali errori.
4. Non avviare la profilazione reale finche il target .NET non e di prova e non si e letto l'avviso di consenso.

## Limitazioni rimaste

- I log e i messaggi gia persistiti da Guardian v0.14.1 restano invariati: per cambiarli servirebbe un aggiornamento esplicito del servizio, non autorizzato in questa revisione.
- La pubblicazione della GUI non e ancora atomica e tutti i 13 progetti di test non sono eseguiti da un unico script.
- Il timeout profiler resta gestito separatamente tra raccolta, report e GUI.
