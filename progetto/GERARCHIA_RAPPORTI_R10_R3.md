# ProcessLens R10 R3 — lettura ordinata dei rapporti

La candidata parte dalla R10 R2 già consegnata. La modifica risponde alle due schermate ricevute: il rapporto allocazioni presenta titoli troncati e lunghi blocchi consecutivi; le evidenze mescolano avvio, DLL ed esito delle letture. Non si ripete la revisione R6–R10. Le schermate non identificano con certezza la versione avviata e non dimostrano l'esecuzione della suite R2.

## Prima e dopo

| Prima | Dopo |
|---|---|
| Sintesi e dettagli poco distinguibili | Quadro generale iniziale, poi capitoli numerati |
| Titolo allocazioni tagliato nel pannello stretto | Titolo a capo, con larghezza legata al capitolo |
| VirtualQueryEx, PSAPI e QueryWorkingSetEx nello stesso blocco | Blocchi con etichetta API e separatori |
| Cinque contatori PSAPI nella stessa riga | Ogni campo già presente su una riga visuale distinta |
| Ogni allocazione adiacente alla successiva | Blocchi separati, mantenendo tipo, indirizzo, numeri e limiti |
| DLL, finestre ed esito sotto Altre osservazioni | Capitoli Moduli e librerie, Finestre osservate, Esito delle letture |
| Evidenze tutte distese | Quadro generale con conteggio voci/capitoli e capitoli chiusi inizialmente |
| Parser che scartava paragrafi non preceduti da ■ | Ogni paragrafo rimane nella propria sezione; premessa e titoli senza corpo conservati |
| Processi / Mostrati senza distinzione esplicita | Rilevati / Mostrati, con spiegazione nei tooltip |

Il contenuto non viene trasformato in una nuova diagnosi. Le categorie di osservazione vengono riorganizzate nella sola GUI. Originali, numeri, fonti e limiti restano nei rapporti. I titoli provengono dal testo esistente; i nuovi titoli Quadro generale e quelli dei capitoli indicano l'organizzazione del contenuto, non il livello di affidabilità dei dati.

## Copia e aggiornamento

I testi dei blocchi rimangono selezionabili e in sola lettura. Copia rapporto completo usa ancora `result.Answer` originale, non una ricostruzione dei controlli visuali. Restano quindi anche paragrafi senza titolo, ritorni a capo originali e sezioni chiuse. Il testo originale delle evidenze rimane nell'espansore esistente in fondo al pannello. I separatori visuali fra campi PSAPI non cambiano i numeri o le unità.

I controlli dei capitoli vengono creati aprendo il capitolo e rilasciati chiudendolo. Se le evidenze non cambiano, non si ricrea la gerarchia; se cambiano per lo stesso PID e timestamp di avvio, si conservano i capitoli aperti. La pulizia dei dettagli azzera la cache. Non vengono aggiunte acquisizioni, timer o letture di memoria. Nessun risparmio RAM misurato o promesso.

## Il programma cattura tutti i processi?

Il collector attuale chiama `CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)` e percorre `Process32FirstW`/`Process32NextW`, creando una riga per ogni voce enumerata. Nella vista processi il filtro Tutti e una ricerca vuota non introducono limiti numerici: la lista usa tutte le righe dello snapshot. Gli altri filtri e la ricerca restringono le righe mostrate. I 303/35 e 300/7 delle immagini sono conteggi rilevati/mostrati in istanti differenti; non provano perdite nell'enumerazione.

Questo non è un monitoraggio completo di ogni avvio/uscita fra aggiornamenti. Un processo può terminare prima della lettura e alcuni contatori, percorsi o moduli possono non essere accessibili. Il collector attuale restituisce un elenco vuoto se falliscono la creazione dello snapshot o la prima enumerazione, senza un risultato di completezza distinto: la GUI non può quindi certificare che l'elenco sia sempre completo. Questo limite preesistente non viene nascosto né corretto modificando l'acquisizione in un intervento di impaginazione. Non viene aggiunta la registrazione continua degli eventi di tutti i processi.

Fonte primaria Microsoft: https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/nf-tlhelp32-createtoolhelp32snapshot

## Verifica e limiti

Eseguiti qui: controlli strutturali Python/XML, conservazione byte per byte dei componenti protetti, replay reale delle patch, confronto SHA-256, integrità ZIP e manifest. Le nuove verifiche .NET sono fornite nella suite esistente CopyTests (paragrafi, API, numeri e copia) e CopyUiTests (titoli a capo, capitoli differiti e rilascio dei controlli).

**NON ESEGUITO:** build/test .NET, script PowerShell, WPF reale, DPI 100/125/150/200%, selezione/scorrimento reali. Ambiente Linux senza dotnet e pwsh. I controlli di sorgente non dimostrano compilazione o corretto rendering WPF. I risultati positivi del log utente precedente restano storici e non qualificano la R3.

Le criticità di contabilità RAM e inferenze già riportate in `COPIA_TESTI_E_REVISIONE_R10.md` e `audit/R10/review-findings.csv` restano aperte. Non vengono modificati Core, Semantics, collector Windows, Guardian, MemoryAgent, profiler, contratti o identificativi. Nessuna installazione, terminazione o profilazione eseguita.

Estrarre in una cartella nuova e, dalla radice con `src` e `tests`, eseguire:

```powershell
.\build-dashboard.ps1
```

Solo se termina con successo:

```powershell
.\run-release.ps1
```

Controllare in particolare i titoli lunghi a pannello stretto, copia Unicode, apertura/chiusura capitoli, mantenimento dei capitoli durante refresh e passaggio fra PID. Tutto resta sotto un'unica radice. Manifest corrente: `SHA256SUMS_R10.txt`; audit R3: `audit/R10_R3/`. I documenti delle revisioni precedenti sono storici.
