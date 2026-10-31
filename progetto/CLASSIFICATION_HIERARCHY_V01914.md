# ProcessLens v0.19.14 — gerarchia delle evidenze

## Problema
Il vecchio pannello `Rapporto diagnostico originale` mostrava in un unico TextBlock
stringhe generate dalle regole del classificatore, metadati e spiegazioni accostate.
A causa della scarsa separazione visiva era difficile distinguere ciò che viene
osservato da ciò che viene dedotto.

## Implementazione
- `Dettagli tecnici della classificazione`: espansione volontaria.
- Sottosezioni per valutazione interna, identità/applicazione, funzione/runtime,
  avvio/relazioni e altre osservazioni.
- Etichette separate dal valore, pannelli con margini/padding e line wrapping.
- Le sottosezioni con molto testo partono chiuse; la valutazione interna è aperta
  soltanto quando si apre volontariamente il pannello tecnico.
- Le evidenze di classificazione con metadati separati da `; ` sono mostrate
  come singole righe con etichette precise (es. nome prodotto, descrizione file,
  editore dichiarato); il testo sorgente resta disponibile senza modifiche.
- Max 32 righe di evidenza rese in modo strutturato; le altre sono contate,
  senza inventare né perdere contenuto.
- `Rapporto diagnostico originale - testo integrale` rimane disponibile dentro
  un ulteriore Expander e usa un TextBox read-only alto massimo 230 WPF unit.
- Nessuna riscrittura dei dati originali, del motore semantico o dei punteggi.
- Rendering dell'audit soltanto quando l'utente apre i dettagli, con cache per
  l'istanza di analisi; gli aggiornamenti periodici non lo ricostruiscono inutilmente.

## Verifiche
- `tests/check-classification-hierarchy-v01914.ps1`: struttura XAML,
  nomi univoci, collegamenti C# e regressione `TextBox.LineHeight` MC3072.
- `ProcessLens.TerminologyTests`: separazione in categorie, conservazione raw,
  punteggi non presentati come probabilità, cap di rendering.

## Confini di privacy
Il testo integrale può contenere identificativi tecnici, percorsi e metadati;
non è esportato né trasmesso automaticamente da questa modifica. Va verificato
prima di condividerlo. Si conserva la semantica evidence-first.

## Componenti invariati
Guardian ETW/SQLite, MemoryAgent e soglie RAM, Windows collector, profiling
isolato/consensuale, frequenze di polling e launcher CMD.

## Stato
Controlli statici disponibili in questa sessione; compilazione e test WPF
su Windows richiesti prima di dichiarare la candidata valida.
