# ProcessLens v0.19.10 R1 - Hotfix compilazione WPF

## Problema riscontrato su Windows

Il log Windows ha confermato che test Core/RAM/Behavior/Terminology/RefreshPolicy/Profiler e la compilazione del profiler passano. La compilazione WPF si interrompeva con `MC3072` in `MainWindow.xaml`, riga 755, perche `TextBox` non riconosce `LineHeight` come proprieta diretta.

## Correzioni

- Rimosso soltanto `LineHeight="19"` dal `TextBox` della riga di comando formattata; rimangono `TextWrapping="Wrap"`, `Padding="12"`, scrolling, max height e formato originale separato.
- Aggiunta al controllo preflight `tests/check-detail-hierarchy-v01910.ps1` un'asserzione XML che rifiuta attributi `LineHeight` direttamente su qualunque `TextBox`.
- Banner aggiornato a `v0.19.10 R1` per distinguere la nuova estrazione dalla vecchia candidata.

## Sicurezza e limiti

Nessuna modifica ai sorgenti Guardian, MemoryAgent, collector Windows o profiler. Nessun test disattivato. L'hotfix non prova da solo che la GUI compili o funzioni su Windows: occorre la compilazione con `AVVIA-TUTTO.cmd` e la verifica del pannello 'Dettagli tecnici e fonti'.

Per test: estrarre in una nuova cartella e lanciare `AVVIA-TUTTO.cmd` senza reinstallare Guardian.
