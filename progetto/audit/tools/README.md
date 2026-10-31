# Controlli strutturali portabili

Usare Python 3 con la R6 originale estratta e la R7 candidata:

```powershell
python .\audit\tools\check_r7.py --baseline C:\percorso\ProcessLens-v0.19.16-R6 --candidate . --output .\verification-r7\static-results.json
```

Nessuna installazione o acquisizione viene avviata. Il confronto richiede l’originale R6 fornito nel trasferimento. Il risultato non è un test C# o WPF. Il registro statico allegato è quello eseguito in Work/Linux. Il diff e i CSV tracciano sorgenti e prove; `MATRICE_NUOVI_CONTESTI_R7.csv` integra le righe testuali introdotte/cambiate, senza chiamarle messaggi unici.
