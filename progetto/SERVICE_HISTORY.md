# ProcessLens v0.11.0 — Cronologia Servizi Windows

## Cos'è stato implementato

- Lettura **on-demand**, in background, del registro eventi locale `System` filtrando `Service Control Manager`.
- Eventi SCM: `7035` (controllo inviato), `7036` (cambio stato), `7040` (configurazione avvio modificata), `7045` (installazione).
- Date e orari locali, ID evento, valori originali strutturati, filtro esatto su **nome tecnico o nome visualizzato**.
- Limiti: ultimi **14 giorni**, **massimo 24 eventi** per servizio, **massimo 5000 record SCM letti**.
- `EventLogWatcher` attivo soltanto mentre si visualizza la sezione **Servizi**; aggiorna il servizio selezionato all'arrivo di nuove registrazioni. L'aggiornamento manuale resta disponibile.
- Consultazione **sola lettura**: non arresta, avvia o modifica servizi.
- Nessuna telemetria inviata a server e nessun file di log utente salvato da ProcessLens.

## Come provarlo

1. Aprire PowerShell nella cartella `ProcessLens-v0.11.0`.
2. Eseguire `Set-ExecutionPolicy -Scope Process Bypass` e poi `.\bootstrap.ps1`.
3. Aprire `Servizi`, selezionare una voce, scorrere al riquadro **Cronologia avvio / arresto**.
4. Premere **Aggiorna eventi** per rileggere il registro.
5. Test indipendente: `.\test-service-history.ps1`.

## Cosa NON sappiamo automaticamente

L'evento SCM 7036 indica che il servizio ha cambiato stato. Il 7035 registra un controllo inviato.
**Nessuno dei due dimostra, di per sé, quale programma o processo abbia richiesto l'operazione**.
Eventi 7040/7045 registrano variazioni di configurazione/installazione ma non provano il motivo dell'avvio.

**Limitazioni**: i log possono essere stati cancellati, sovrascritti o non essere accessibili; la corrispondenza usa i nomi registrati negli eventi (un servizio rinominato potrebbe essere difficile da ritrovare). Il watcher richiede privilegi di lettura adeguati e, se assenti, il monitoraggio live non è disponibile. Non viene mai elevato automaticamente.

## Evoluzione successiva

Correlare richieste start/stop e creazioni processo usando provider ETW idonei e, se disponibili, eventi di auditing. Presentare sempre la correlazione temporale come *indizio* finché non esiste una relazione causale esplicita registrata.
