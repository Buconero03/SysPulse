# ProcessLens v0.19.16 — Evidence Boundaries & Terminology Audit Fixes

## Provenienza e scopo

Base: `ProcessLens-v0.19.15-glossario-integrato-candidate.zip` (sorgente originale conservato). Interventi guidati dal rapporto `ProcessLens_Audit_Revisionato_v01915.zip`, con 37 segnalazioni e 11 gruppi di ripetizioni. Nessuna sostituzione automatica indiscriminata.

## Correzioni

- **TCP LISTEN:** il socket è in ascolto; non significa che siano state ricevute/accettate richieste. I dettagli registrano lo stato socket attribuito al PID.
- **TCP ESTABLISHED:** rappresenta uno stato della connessione, non una misura dei byte trasferiti, dell'attività in quel momento né del servizio remoto.
- **Omonimia del processo padre e framework:** `ParentName == Name` da solo non verifica che la relazione padre-figlio sia temporalmente coerente. Esplicitato il requisito di controllo PID e orari di avvio. Le classificazioni di 'coordinatore' basate su processi figli omonimi sono ora qualificate come possibili.
- **Spiegazione dell'attività:** i conteggi TCP compaiono fra le osservazioni; la cautela su stato e traffico non misurato compare una volta nel riepilogo dei limiti, quando pertinente.
- **Terminologia:** introdotta l'etichetta 'Funzione attribuita dal classificatore'; migliorate le spiegazioni del processo GPU Chromium, con riferimento preciso a `--type=gpu-process`.
- **GUI:** semplificata l'avvertenza sulle conseguenze di terminazione: nessuna terminazione automatica.
- **Versione:** allineati `VERSION.txt`, intestazione dello script `start-processlens-all.ps1` e nuova documentazione.
- **Test:** aggiunti casi sintetici a `ProcessLens.TerminologyTests` senza disabilitare i test precedenti.

## Non modificato

I sorgenti di Guardian (ETW/SQLite), MemoryAgent, i componenti di misurazione Windows e il motore del profiler .NET non sono modificati rispetto alla v0.19.15. Non viene cambiato il launcher `AVVIA-TUTTO.cmd`, il database installato né alcun servizio del sistema. Restano invariati il consenso prima del dump, i gate RAM, le policy di scansione a basso impatto e la gestione dei dati sensibili.

## Verifiche e limiti

Archivio ZIP, CRC, validità sintattica XML/JSON, riferimenti ai file e confronto dei componenti invariati verificati offline. **Non sono stati eseguiti i test .NET, il preflight PowerShell 5.1, la build WPF né la profilazione reale su Windows**: la versione resta `Candidate` fino alla prova via `AVVIA-TUTTO.cmd`. Nessun processo reale è stato profilato.

## Sequenza di verifica Windows

1. Estrarre la nuova versione in una cartella indipendente, senza sovrascrivere la precedente.
2. Avviare `AVVIA-TUTTO.cmd` e verificare che inizi con `ProcessLens v0.19.16`.
3. Richiedere build verde di tutte le suite, in particolare `ProcessLens.TerminologyTests`, `ProcessLens.BehaviorTests`, profiler e WPF. Non bypassare test.
4. Verificare visivamente il pannello delle evidenze e l'effetto sui consumi in condizioni controllate; non eseguire dump senza consenso.
5. Ripetere audit Colab sullo ZIP v0.19.16 e confrontare qualitativamente le segnalazioni. Il conteggio grezzo non è una metrica di accuratezza.

## Audit e criteri di accettazione

Sono conservate le stringhe `knowledge:` del classificatore nel rapporto tecnico espandibile e le verifiche `PRAGMA query_only` dei due lettori SQLite. Non sono rimossi i limiti sullo stack, heap, rete e sullo stato dei processi solo per ridurre il numero di occorrenze testuali. Si correggono esclusivamente affermazioni non supportate e ridondanze confermate nello stesso percorso di visualizzazione.

## Manifesti

`SHA256SUMS_V01916.txt` è il manifesto **corrente** del contenuto non compresso (escluso il manifesto stesso). I manifesti di versioni precedenti sono mantenuti come documenti storici e non verificano questa versione.
