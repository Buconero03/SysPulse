# Changelog R6 → R7 candidata

- Descrizioni di runtime/ruolo da parametri e nomi, separate dal lavoro corrente e dalla causa storica dell’avvio.
- Knowledge e fallback C# coerenti su updater, crash handler, main/coordinator, TCP e GPU; predicati e ID conservati.
- Associazioni proposte e metadati dichiarati; punteggi interni distinti dalle probabilità.
- Rete: socket rilevate, non volume di traffico/delega; note e limiti di zero; ascolto TCP distinto da connessioni/UDP.
- CPU: N/D quando disponibile il flag; finestra fino a 30 secondi; nessuna salute certa da campioni parziali.
- Memoria: saldo distinto da monotonia, soglie con uguaglianza corretta, contatori equivalenti e stessa istanza per confronto.
- Progresso/ETA: testo riconosciuto e fonte; stima condizionata e scenario aritmetico senza calibrazione statistica.
- Servizi: descritta la capacità SCM già presente e lo stato del solo servizio; nessun cambiamento di controller/policy.
- GUI: titoli a capo, terna memoria con minimi comuni e guida RAM più breve; verifica visuale Windows ancora da eseguire.
- Quattro suite aggiornate/ampliate; aggiunto verificatore Windows completo senza avviare GUI, fixture, profilazioni o installazioni.
- Audit originale conservato, matrice di 3.406 riferimenti, 41 decisioni prioritarie, continuità glossario di 95 voci, diff e manifest.

**Stato:** 20 controlli strutturali Python PASS. Test .NET/PowerShell, build e visuale WPF **NON ESEGUITI**. R7 non è una release stabile.
