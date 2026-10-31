# ProcessLens v0.19.6 — Correzione parser PowerShell 5.1

## Problema riscontrato sul Windows dell'utente

Durante l'avvio automatico della v0.19.5, subito dopo i controlli WPF e Guardian superati, PowerShell ha interrotto la build con `ParseException` in `tests/check-memory-ui-v0195.ps1` (righe 15, 17, 20). Non era una failure dei contatori RAM e non era una prova di malfunzionamento della GUI. La build non era ancora arrivata ai test .NET successivi.

## Root cause e fix

Il messaggio `throw 'FAIL RAM UI: barra non collegata alla gravita''` conteneva due apici finali, che in PowerShell prolungano la stringa invece di chiuderla. Ora l'apice finale e singolo. Il resto del test rimane invariato, senza disabilitare verifiche.

## Protezione aggiuntiva

Il nuovo `tests/check-powershell-syntax.ps1` usa il parser ufficiale di PowerShell per verificare tutti i `.ps1` inclusi prima dell'esecuzione dei test. In caso di altri errori stampa nome script, riga e colonna. `build-dashboard.ps1` lo esegue come primo controllo statico.

Aggiornate le versioni `ProcessLens.App` e `ProcessLens.Profiler` a `0.19.6`. Il resto dei sorgenti applicativi, Guardian, agente RAM e policy 85/80 rimangono byte per byte uguali alla v0.19.5. `AVVIA-TUTTO.cmd` continua a essere il punto di ingresso standard, senza diritti amministrativi all'avvio.

## Stato delle verifiche

- Integrita ZIP, struttura, riferimenti e XML: controllati nel pacchetto candidato.
- PowerShell 5.1 parser nativo, build WPF, test .NET e avvio GUI: da eseguire realmente su Windows.
- Guardian e database: non reinstallati, modificati o ripuliti.

## Procedura

1. Estrarre **tutto** lo ZIP in una cartella nuova.
2. Fare doppio clic su `AVVIA-TUTTO.cmd`, non come amministratore.
3. Confermare l'output `PASS Sintassi PowerShell: ...` e i test che seguono.
4. Se compare un errore, condividere il nuovo log completo. Non saltare i test.
