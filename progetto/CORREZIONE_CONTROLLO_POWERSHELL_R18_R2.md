# ProcessLens v0.19.16 — candidata R18 R2

Corretto il blocco `FAIL R18 R1 must retain native theme without showing windows.` del controllo `tests/check-copy-layout-r18r1.ps1`.

In PowerShell `-match` ignora le maiuscole: il controllo precedente scambiava `template=` nella stringa diagnostica C# per un'assegnazione alla proprietà `Template`. Il vecchio script riproduce il problema anche senza avviare WPF.

Il controllo aggiornato esclude stringhe e commenti, usa il confronto sensibile alle maiuscole e distingue `Template = ...` da `Template == ...`. Tredici casi interni verificano stringhe, commenti, uguaglianze e modifiche vietate. Restano tutte le verifiche originali su Dispatcher, limiti, altezza, righe, ritorno a capo e copia del testo.

## Verifiche eseguite

- PowerShell 7.4.21 su Linux: riprodotto il fallimento del vecchio script; il nuovo passa sullo stesso sorgente WPF.
- Eseguiti tutti i 30 controlli statici nell'ordine di `build-dashboard.ps1`, inclusa la sintassi dei 69 script: PASS.
- Eseguite otto varianti del sorgente: accettate le diagnostiche legittime; respinte assegnazioni al template, apertura di finestre e rimozione delle verifiche originali.
- Rieseguite 260 verifiche Python su sorgenti, regressioni e corrispondenza delle prove conservate: PASS.
- Verificata l'identità dei sorgenti per le precedenti 332 asserzioni portabili e i sei controlli tipi C#: prove conservate, non rieseguite in questa revisione.

Il codice C# operativo, XAML, test C# e sequenza di compilazione sono identici alla R18 R1. Cambiano lo script di controllo e l'identificativo della candidata nel progetto App. Gli strumenti di audit riconoscono i soli hash di questa revisione.

**Non eseguiti qui:** Windows PowerShell 5.1, compilazione completa Windows, caricamento XAML/BAML, esecuzione WPF, cattura di rete reale e misurazione RAM. Il superamento dei controlli statici non dimostra il superamento dei successivi test WPF.

## Verifica su Windows

Estrarre lo ZIP in una nuova cartella. Aprire PowerShell nella cartella `ProcessLens-v0.19.16-R10` ed eseguire:

```powershell
.\build-dashboard.ps1
```

Il log atteso per il controllo corretto include:

```text
PASS R18 R2 source guard: 13 regression cases; original false positive reproduced.
PASS R18 R1 source: bounded native Dispatcher layout; original height, wrap and selection assertions retained.
```

La pipeline prosegue con gli altri controlli e test previsti. Non avvia catture reali, non cambia criteri di rete e non modifica Guardian.

Diff, registro delle modifiche, prove d'esecuzione e matrice dei test: `audit/R10_R18_R2/`. Manifest completo: `SHA256SUMS_R10.txt`.
