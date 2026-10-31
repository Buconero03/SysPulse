# R14 R2 → R14 R3

Prima: test console STA senza contesto WPF prima di avviare RefreshAsync; il primo seguito asincrono può accedere ai controlli dal thread pool.

Dopo: contesto Dispatcher impostato prima dell'avvio delle operazioni asincrone, verifica del ritorno sul thread proprietario e ripristino in finally. Applicato alle suite rete e sistema. Asserzioni precedenti conservate. Nessun testo UI o contatore modificato.

Stato: controlli sorgente eseguiti; test WPF NON ESEGUITI su questo host. Il log Windows fornito documenta l'errore della candidata R2, non un risultato della R3.
