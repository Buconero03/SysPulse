# ProcessLens v0.19.16 R9 — formattazione e leggibilità

Candidata completa derivata dalla R8 consegnata, con le modifiche dell’utente e le ottimizzazioni RAM conservate. La revisione terminologica precedente non viene riaperta. Questa correzione riguarda i problemi mostrati nello screenshot del 15 ottobre 2026: tempo CPU in formato grezzo, unità attaccate alle cifre e testo denso nel pannello inferiore. Nessuna promozione stabile.

| Elemento | Prima | R9 |
| --- | --- | --- |
| Tempo CPU dello screenshot | `00:10:36.0625000` | `10 min 36,063 s` in it-IT; `10 min 36.063 s` in en-US |
| Dieci millisecondi | Formato TimeSpan con ore e zeri iniziali | `10 ms` |
| Lettura positiva inferiore a un millisecondo | Molte cifre frazionarie | `< 1 ms`; non viene mostrata come zero |
| Campione CPU | `16,5s` | `16,5 s` |
| Percentuale CPU | `2,8%` | `2,8 %` |
| Stato rete | Un unico paragrafo con punti e virgola | Un contatore per riga; blocchi distinti per PID, gruppo, ruolo e limiti |
| Pulsanti di azione | Etichetta su una sola riga senza altezza minima comune | Testo a capo, interlinea 20 DIP, altezza minima 36 DIP |
| Menu Analisi approfondita | Etichetta senza template di ritorno a capo | Etichetta con wrapping, padding e altezza minima |
| Celle CPU e RAM | Alcuni valori senza wrapping | Valori e legenda del grafico vanno a capo nella propria cella |

Le unità CPU usano uno spazio non separabile fra numero e simbolo, così il simbolo non finisce da solo sulla riga successiva. Le cifre decimali rispettano la cultura corrente. Il tempo CPU della sintesi è arrotondato al millisecondo, con riporto corretto a secondi, minuti, ore e giorni. Il valore originale del contatore è disponibile nel tooltip, con rappresentazione G17 in secondi. Le letture mancanti restano non disponibili. Il contatore non viene modificato.

Il tempo CPU è la somma del tempo CPU dei thread. Non è il tempo trascorso dall’avvio: data di avvio e durata trascorsa restano nelle loro sezioni con le fonti precedenti.

La rete mantiene i conteggi del PID distinti da quelli del gruppo e il ruolo suggerito dai parametri distinto dalle osservazioni. Lo stato LISTEN non dimostra richieste accettate; ESTABLISHED non dimostra trasferimento di byte. Gli eventuali errori di acquisizione sono ora visibili anche nella sintesi. La selezione del processo correlato usa gli stessi criteri della R8. Nessuna nuova scansione o acquisizione.

Gli stili conservano il template nativo dei pulsanti di azione e i gestori esistenti. I filtri possono andare a capo con margine fra righe; il layout usa arrotondamento alle coordinate dei pixel. Non sono state cambiate impostazioni DPI di Windows.

## Verifiche eseguite e limiti

- Controlli strutturali portabili: vedere `audit/R9/structural-results.json`.
- Confronto byte per byte dei componenti protetti e delle regole con la R8: incluso nel controllo strutturale.
- Diff completo R8 → R9, registro file/riga/motivazione e replay esatto della patch: `audit/R9/`.
- Manifest corrente: `SHA256SUMS_R9.txt`. I manifesti R7/R8 e i loro rapporti sono storici, non attestano la R9.
- **NON ESEGUITO**: compilazione e test .NET, PowerShell, avvio e verifica visuale WPF. L’ambiente disponibile è Linux senza SDK .NET né PowerShell. Il parsing XML e i controlli sui sorgenti non sostituiscono queste prove.
- **NON MISURATO**: consumo RAM della R9 e risparmio rispetto ai 116,9 MB precedentemente segnalati. Le ottimizzazioni R8 restano presenti; questa revisione non promette un ulteriore risparmio.

La nuova suite `ProcessLens.PresentationTests` usa direttamente gli stessi formatter dell’app, con dati sintetici it-IT/en-US: durata dello screenshot, precisione, riporto fra unità, zero reale, valori invalidi, contatori rete diretti e correlati, acquisizioni parziali e note di errore. È collegata a `build-dashboard.ps1`. Anche il nuovo controllo strutturale PowerShell `check-ui-format-r9.ps1` è collegato alla build. Le suite precedenti restano incluse.

## Verifica riproducibile su Windows

Estrarre lo ZIP in una cartella nuova; eseguire dalla radice che contiene `src` e `tests`:

```powershell
.\build-dashboard.ps1
```

Se la build termina con successo, avviare la candidata con:

```powershell
.\run-release.ps1
```

Per la verifica estesa senza avvio della GUI, installazioni o acquisizioni, usare invece `verify-candidate-r9.ps1`, che esegue tutti i progetti di test e le build. La fixture del profiler viene soltanto compilata. Non avviare profilazioni per controllare l’interfaccia.

Controllo visuale manuale da registrare nella matrice:

1. Ripetere a 100%, 125%, 150% e 200% di ridimensionamento, con finestra 1180×800 e 950×680 DIP dove lo schermo lo permette; verificare anche la massimizzazione. Per le configurazioni non disponibili registrare NON ESEGUITO.
2. In Processi, selezionare un PID e scorrere fino al pannello CPU e allo stato rete. Controllare tempi leggibili, virgola/punto secondo cultura e unità con spazio. Un valore e la sua unità non devono separarsi. Confermare che il tooltip conservi il valore in secondi e distingua tempo CPU e durata trascorsa.
3. Ridurre la larghezza: Media, Picco, RAM e legenda non devono disegnare sopra la cella vicina. Controllare pulsanti, filtri, righe a capo e margini verticali; nessun testo tagliato o sovrapposto.
4. Aprire Analisi approfondita con mouse e tastiera; verificare menu, voci e focus. Non scegliere la voce di profilazione per questa prova. Controllare Chiedi e Mostra servizi Windows ospitati dal PID.
5. In Servizi, controllare la leggibilità dei pulsanti Mostra processo ospitante, Aggiorna eventi e Arresta servizio senza eseguire arresti. Controllare anche gli stati disabilitati.
6. Passare a Guardian e Memoria RAM, poi tornare a Processi; aprire e chiudere i dettagli e selezionare più PID. Controllare che la visualizzazione differita R8 e l’aggiornamento dei campi restino funzionanti.

Per ripetere il controllo portabile, usando lo ZIP R8 originale come baseline:

```powershell
python .\audit\tools\check_r9.py --baseline-zip C:\percorso\ProcessLens-v0.19.16-R8-memory-candidate.zip --candidate . --output .\verification-r9\structural-results.json
```

Il confronto richiede esattamente la candidata R8 consegnata. Sorgenti Core/Semantics, collector Windows, Guardian, MemoryAgent, profiler, regole JSON, contratti e diagnostica strutturata rimangono invariati rispetto a tale base.
