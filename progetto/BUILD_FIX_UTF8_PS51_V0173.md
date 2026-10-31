# ProcessLens v0.17.3 — Build preflight Windows PowerShell 5.1

## Problema riprodotto

La versione v0.17.2 si interrompeva in `tests/check-structured-queries.ps1` con
`ERRORE: rapporto memoria strutturato non collegato correttamente.`
Il titolo `VALUTAZIONE: È UN CONSUMO ECCESSIVO?` era presente correttamente
nel sorgente C#. Il controllo cercava una stringa letterale con `È` in uno
script PowerShell salvato UTF-8 *senza BOM*. Windows PowerShell 5.1 può leggere
uno script senza BOM utilizzando la pagina di codici ANSI, corrompendo la
stringa cercata: falsi negativi del controllo preliminare.

## Correzioni

- Script preflight salvato UTF-8 **con BOM** per Windows PowerShell 5.1.
- Il carattere non ASCII nella condizione viene costruito con `[char]0x00C8`.
- Il test mostra il nome di ogni collegamento mancante anziché un messaggio generico.
- Il banner del lanciatore riporta la versione corretta.

Non è stato modificato il motore RAM, il servizio Guardian o il database.

## Test sul PC Windows

Da PowerShell nella cartella del progetto:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\tests\check-structured-queries.ps1
.\build-dashboard.ps1
```

Oppure doppio clic su `AVVIA-TUTTO.cmd`.

Il controllo statico non sostituisce `dotnet build`: confermare 0 errori/avvisi
sul PC Windows prima di avviare ProcessLens.
