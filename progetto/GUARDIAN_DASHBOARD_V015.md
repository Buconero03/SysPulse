# ProcessLens v0.15.0 — Guardian Dashboard

## Ambito e modello di sicurezza

Questo pacchetto contiene tutto il progetto ProcessLens, compresi i sorgenti Guardian v0.14.1.
La modifica v0.15.0 riguarda soltanto la GUI WPF e il suo nuovo lettore dello storico.
Il servizio `ProcessLensGuardian` installato sul PC continua a funzionare come prima: **non usare**
`upgrade-guardian.ps1` per installare questa sola novità grafica.

La dashboard apre `C:\ProgramData\ProcessLens\Guardian\guardian.sqlite3` in `SqliteOpenMode.ReadOnly`,
con `PRAGMA query_only=ON`, connessioni non pooled e timeout brevi. Esegue le query fuori dal thread WPF,
con aggiornamento di 10 secondi mentre la sezione Guardian è visibile. Non apre sessioni ETW proprie,
non arresta processi, non interagisce con SCM e non modifica la configurazione di Guardian.

## Viste

1. **Eventi recenti**: ultimi 250 eventi al massimo entro 5/15/30/60 minuti. Filtro testuale limitato al campione, non all'intera cronologia.
2. **Attività e processi padre**: istogramma ETW, top programmi per avvio, PPID con possibili nomi estratti dallo storico. La parentela è *ipotizzata*, perché PID e timestamp al secondo possono essere ambigui.
3. **CPU e RAM storiche**: aggregati disponibili nella tabella `resources`, per nome; una riga non rappresenta necessariamente un singolo processo. I bucket includono dati fino a cinque minuti prima del limite della finestra.
4. **Salute ETW/SQLite**: ultimo evento ETW nella finestra scelta, numero di avvii e terminazioni, diagnostiche di overflow, perdite batch segnalate, dimensione dei file principale e WAL.

Un database mancante, occupato o non leggibile mostra un errore nella pagina; non chiude ProcessLens.
L'assenza di diagnostiche non garantisce che non siano stati persi eventi.
Il WAL può mantenere la stessa dimensione pur riutilizzando pagine; non equivale a crescita permanente.

## Compilazione e prova su Windows

PowerShell da cartella progetto:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build-dashboard.ps1
.\run-release.ps1
```

`build-dashboard.ps1` esegue i preflight XML/XAML, i test del lettore su un SQLite temporaneo,
compila la GUI .NET 8 WPF e pubblica `publish\win-x64\ProcessLens.exe`.
La pubblicazione richiede il .NET 8 SDK; il programma pubblicato richiede .NET 8 Desktop Runtime.
L'applicazione originale usa l'elevazione UAC tramite manifest, rimasta invariata.

Verifiche manuali essenziali:

- All'apertura la vista Processi e la vista Servizi devono funzionare come prima.
- Seleziona **Guardian** e osserva l'aggiornamento dell'orario di lettura dopo 10 secondi.
- Cambia finestra 5/15/30/60 minuti; verifica che le cifre cambino coerentemente.
- Cerca `git` nella lista Eventi e controlla i filtri.
- Passa a Processi e verifica che la dashboard smetta di eseguire query finché non torni su Guardian.
- Con Guardian disinstallato, la scheda deve mostrare un errore senza bloccare le altre funzionalità.

## Limiti e test

Il lettore utilizza soltanto tabelle già presenti nello schema 0.14.1; non introduce migrazioni.
Non è un flusso ETW push: è un aggiornamento del report dal database ogni 10 secondi.
La cronologia dei processi figli ricostruita per PPID è prudente, non una prova causale definitiva.

Gli script di test sono inclusi, ma **la compilazione e il rendering WPF reali non possono essere garantiti
finché non vengono eseguiti su Windows con .NET SDK**. Nessun benchmark sulla GUI è stato ancora effettuato.
