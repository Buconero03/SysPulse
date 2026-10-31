# ProcessLens v0.12.0 — Perché usa RAM?

## Obiettivo
Spiegare la RAM di un processo distinguendo fatti misurati, tendenze e ipotesi.

- Nuova domanda **Perché usa RAM?** accanto a **Perché usa CPU?**.
- Valori misurati: Private Working Set, Working Set totale, commit privato.
- Cronologia locale per PID verificato con orario di avvio: ultimi 5 minuti, massimo 160 campioni, campionamento ~2 secondi.
- Variazione, minimo e picco nella scheda memoria. Storico non persistente e azzerato quando PID o metrica cambiano.
- `VirtualQueryEx` (solo alla domanda esplicita): regioni di **spazio di indirizzamento virtuale MEM_COMMIT** classificate MEM_PRIVATE / MEM_IMAGE / MEM_MAPPED; massimo 100.000 regioni o 2 secondi.
- Il risultato dichiara esplicitamente che i MB virtuali impegnati **non sono memoria fisica residente** e che non è possibile identificare cache, oggetti o heap precisi senza un profiler dedicato.
- Interpretazioni del ruolo (language server, renderer, GPU) indicate come cause *possibili*, non provate.
- CPU inattiva non implica che il processo debba liberare immediatamente la RAM; la crescita di pochi minuti non dimostra un leak.

## Uso
Avvia con `bootstrap.ps1`, seleziona un processo, attendi almeno 20 secondi e premi **Perché usa RAM?**.

## Vincoli
La mappa virtuale può essere non disponibile per processi protetti, accesso negato, istanze PID riutilizzate o mismatch di architettura. Non è una heap snapshot e non legge il contenuto dei processi. Non forzare l'elevazione di privilegi. Il programma non modifica la memoria del processo analizzato.

## Compilazione
È richiesto .NET 8 SDK su Windows. Controllare `dotnet build` e i test e l'apertura WPF sul PC reale prima di considerare la build verificata.
