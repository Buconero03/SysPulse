# ProcessLens v0.19.15 — Revisione manuale audit terminologico

**Sorgente:** ZIP Colab `ProcessLens_Audit_Terminologia_v01915_Risultati.zip`, confrontato con `ProcessLens-v0.19.15-glossario-integrato-candidate.zip`.

## Interpretazione

Sono state rilevate **37 corrispondenze lessicali in 109 file** e **11 gruppi di stringhe ripetute**. Le corrispondenze non sono bug confermati. **21/37** sono generate dalla regola A06 (espressione `non prova`), ma spesso preservano limiti diagnostici essenziali.

## Decisioni

- **PRESERVARE_LIMITE**: 12 casi.
- **MIGLIORARE_DICITURA**: 5 casi.
- **PRESERVARE_DATO_TECNICO**: 5 casi.
- **RIDURRE_RIPETIZIONE_UI**: 5 casi.
- **PRESERVARE_GUARDIAN**: 2 casi.
- **VERIFICARE_NON_UTILIZZATO**: 2 casi.
- **CORREGGERE_ASSERZIONE**: 1 casi.
- **FALSO_POSITIVO_COMMENTO**: 1 casi.
- **MIGLIORARE_ETICHETTA**: 1 casi.
- **PRESERVARE_AUDIT**: 1 casi.
- **PRESERVARE_TECNICO**: 1 casi.
- **VERIFICARE_PRESENTAZIONE**: 1 casi.


## Correzioni prioritarie suggerite

1. **`RoleClassifier.cs` (circa riga 150):** TCP `LISTEN` prova uno stato del socket attribuito al PID, **non** che siano gia state accettate connessioni. Sostituire l'affermazione `Questo dimostra che il processo accetta connessioni in ingresso` con `Windows rileva una socket TCP in ascolto attribuita al processo; non sono state osservate richieste accettate` (salvo altra telemetria).
2. **`RoleClassifier.cs` (circa riga 171):** stesso nome del processo padre e figlio non basta a dimostrare un legame fra istanze verificato temporalmente. Usare `parentela riportata` fino a conferma con PID + timestamp.
3. **`ProcessBehaviorExplainer.cs`:** separare conteggio di connessioni TCP stabilite da limitazione sui dati effettivamente trasferiti, evitando di ripetere l'avvertenza due volte nella medesima risposta.
4. **`ProcessDetailPresentation.cs`:** evitare di mostrare la stessa avvertenza sia nella scheda riepilogativa sia nella valutazione espansa. Preservare punteggio e ID interni nel pannello tecnico opzionale.
5. **`role-signatures.json`:** la regola per `--type=gpu-process` supporta una dicitura piu tecnica, ad es. `Processo GPU di Chromium`, senza affermare quale operazione grafica sia attiva.
6. **`VERSION.txt`:** contiene `0.19.13 Candidate - Structured Diagnostics & Technical Logging` anche nell'archivio chiamato v0.19.15. Aggiornare metadati e launcher nella prossima release, verificando la coerenza dei test.

## Non modificare automaticamente

- Le stringhe `knowledge:` sono identificatori interni legittimi e devono restare nei dati e nel rapporto diagnostico originale.
- I due comandi `PRAGMA query_only=ON; PRAGMA busy_timeout=1500;` proteggono due reader SQLite distinti.
- Le cautele su TCP, limiti delle API, SCM e memoria virtuale devono restare dove rilevanti, anche se la ricerca lessicale le classifica come ripetizioni.
- I file Guardian non devono essere alterati senza specifica autorizzazione e senza piano di migrazione del servizio.

## Metodologia e limiti

Le decisioni sono frutto di confronto manuale fra le righe segnalate e il codice sorgente nel pacchetto. Non sono state eseguite compilationi .NET/WPF né prove di acquisizione in Windows. Nessuna modifica al progetto è stata applicata. Ogni eventuale patch deve avere test di regressione specifici e un pacchetto completo distinto dalla v0.19.15.

I due CSV allegati contengono una decisione motivata per ognuna delle 37 segnalazioni e degli 11 gruppi di ripetizioni.
