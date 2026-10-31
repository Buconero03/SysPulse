# ProcessLens v0.9.3 — regressione cultura nei test della memoria

## Perché la v0.9.2 non si avviava su Windows italiano

Il modello `ProcessSnapshot.MemoryDisplayText` formatta correttamente i numeri
con la cultura Windows corrente (`$"{MemoryDisplayMb:F1} MB"`). In Italia
26 MiB diventano `26,0 MB`; i test della v0.9.2, invece, attendevano la
stringa inglese `26.0 MB` e fallivano anche quando i contatori erano corretti.

## Correzione

I test confrontano il valore numerico oltre all'etichetta e alla descrizione,
creano il testo atteso usando la cultura attiva ed eseguono gli stessi quattro
casi per `it-IT` e `en-US`. Non è stato modificato alcun algoritmo o valore
della telemetria RAM/CPU. La build Release viene avviata soltanto se i test passano.

## Verifica locale (PowerShell)

```powershell
.\bootstrap.ps1
```

Risultato atteso dei test RAM: 8 PASS in tutto (4 in italiano e 4 in inglese).

## Limitazioni

Non è stata effettuata compilazione reale Windows nell'ambiente di generazione.
L'esito della build e il funzionamento reale dei contatori restano da verificare
sulla macchina Windows dell'utente.
