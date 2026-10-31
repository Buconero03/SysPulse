# ProcessLens v0.19.9 R2 — Correzione del preflight PowerShell

## Problema riscontrato su Windows 11

`AVVIA-TUTTO.cmd` si interrompe durante il controllo sintattico iniziale,
prima di avviare i test .NET e la build WPF.
La stringa `ATTENDIBILITÀ DELL’IDENTIFICAZIONE` all'interno di un apice
singolo in `tests/check-terminology-ui.ps1` contiene U+2019 (apostrofo
tipografico), che Windows PowerShell può trattare come delimitatore di stringa.
Gli altri errori segnalati alle righe 30 e 31 sono a cascata.

## Correzione

- Il test costruisce l'apostrofo tipografico con `[char]0x2019`,
  senza cambiare il testo XAML che deve verificare.
- `tests/check-powershell-syntax.ps1` ora segnala esplicitamente apici
  tipografici U+2018/U+2019 e virgolette U+201C/U+201D negli script,
  oltre a eseguire il parser PowerShell di tutti i file `.ps1`.
- Le intestazioni di `start-processlens-all.ps1` e `profiler-status.ps1`
  riportano v0.19.9 e non piu' v0.19.8.

## Vincoli

Nessuna modifica ai sorgenti C#/XAML, a Guardian, a MemoryAgent, ai
launcher CMD, alla politica RAM o ai contenuti del database SQLite.
Non sono stati saltati test o disabilitati controlli.

## Esecuzione Windows

Estrarre l'archivio completo R2 in una nuova cartella; avviare come sempre
`AVVIA-TUTTO.cmd`, senza opzione Esegui come amministratore.
Il preflight deve stampare `PASS Sintassi PowerShell: ... script verificati.`
e `PASS Terminologia UI: ...`. Se ulteriori test falliscono, inviare
l'output completo; R2 non e' una build WPF gia' qualificata su Windows.
