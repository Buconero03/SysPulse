# ProcessLens R26: scorrimento e riduzione

Base R25: SHA-256 `a879ff1363aad04bde00658a5585185b36370c7b98e395a25bf6de0efb366fc3`.
Questa revisione continua quella base. Non ripete la revisione terminologica storica.

## Modifiche

| Caso | R25 | R26 |
|---|---|---|
| Inventario e analisi asincrona | Sostituzione ItemsSource, righe e gruppi | ObservableCollection stabile, riuso delle righe soltanto con identità compatibile, gruppi riutilizzati per chiave |
| CPU/RAM ogni secondo | Refresh ordinato anche durante la lettura | Valori aggiornati, ordine fermo con offset > 0,5 pixel, puntatore o fuoco tastiera nella lista |
| Scorrimento | Ripristino numerico con callback non invalidata | Ancora visiva, ticket annullato dai nuovi input, una sola operazione pendente dopo il lavoro Background del testo; in cima resta offset zero |
| Dettagli | Riscrittura senza protezione del viewport | Protezione degli aggiornamenti automatici e intercettazione BringIntoView sotto ScrollViewer, prima del suo handler di classe |
| Gruppi chiusi | Gruppo selezionato riaperto al refresh | Stato conservato; apertura forzata soltanto per navigazione esplicita/prima selezione |
| Riduzione | Attesa EnsureTray, comando WPF duplicato e fallback a ContextIdle | Priorità Input, ShowWindow(SW_MINIMIZE) su HWND posseduto, fallback WPF se IsIconic è falso, verifica a Loaded |
| Comandi di sistema | Percorsi distinti | Pulsante, SC_MINIMIZE, WM_CLOSE e Closing condividono il coordinatore; Esci e fine sessione restano uscite effettive |

La modalità a pixel mantiene CanContentScroll e virtualizzazione Recycling anche nei gruppi. Nessuna scansione nativa aggiuntiva, cronologia illimitata, thread o dipendenza aggiunta. CPU/RAM mantengono l'intervallo nominale di un secondo a finestra visibile.

L'ordine rimane fermo finché si torna in cima e puntatore/fuoco escono dalla lista, oppure si sceglie esplicitamente un ordinamento. Durante la lettura nuovi PID vengono aggiunti senza riordinare i superstiti, quelli usciti vengono rimossi. Una famiglia realmente cambiata può richiedere un refresh del raggruppamento, sempre protetto dal guard. L'ancora viene conservata se ancora realizzata; altrimenti si usa l'offset precedente entro i limiti del contenuto. Non si può conservare una riga scomparsa o una posizione oltre il contenuto residuo.

## Verifiche correnti

- 13 suite portabili, **661 asserzioni**: CPU, RAM, copia, presentazione, terminologia, gruppi, filtro, porte e regressioni precedenti. Dati sintetici e processi fixture posseduti; nessuna cattura o sonda di rete.
- 3 controlli negativi su copie di produzione: vecchio ticket accettato, riordino durante lettura, uscita effettiva intercettata. Ciascuno causa il fallimento previsto.
- 33 controlli PowerShell, 17 casi isolati del launcher. Ricevuta R26 distinta: una build fallita non può avviare una pubblicazione precedente.
- 14 controlli del guard risorse con relativi casi alterati.
- Controllo C# di 9 assembly contro riferimenti WindowsDesktop. I campi del markup sono stub esterni al pacchetto: **non è una compilazione WPF/BAML né una prova di esecuzione Windows**.
- XML, scope esatto, patch riapplicata con confronto byte, manifest e integrità ZIP: risultati nei JSON allegati.

Dopo la manutenzione che ha rimosso lo spazio temporaneo, i sorgenti R25 sono stati recuperati dal pacchetto salvato e verificati con l'hash sopra. Le modifiche sono state riapplicate dalla sessione e i controlli R26 rieseguiti. I log storici R25 restano tali: i risultati correnti sono solo in questa cartella.

## Prove Windows non eseguite qui

Il problema di riduzione segnalato dall'utente non è stato riprodotto su Windows in questo ambiente; non se ne dichiara dimostrata la causa. La modifica rimuove la dipendenza della riduzione dalla registrazione tray e unifica i percorsi nativi. ShowWindow restituisce la visibilità precedente, non il successo: la conferma usa IsIconic e il log riporta lo stato effettivo.

Dopo la build, dalla cartella progetto:

```powershell
dotnet run --project tests/ProcessLens.ProcessGroupsUiTests -c Release -- --scroll-smoke
dotnet run --project tests/ProcessLens.ProcessGroupsUiTests -c Release -- --shell-smoke
```

La prova scroll apre il MainWindow reale con raccolta disabilitata e dati simulati: verifica lista a metà pagina, stabilità dell'ordine e delle due posizioni, annullamento di un vecchio ripristino. La prova shell opera solo sulla propria finestra/icona: HWND/WPF, cinque cicli nativi di riduzione/riapertura, stato ingrandito, X ed uscita. Queste opzioni sono state controllate come C#, **non eseguite qui**. La suite UI normale non mostra finestre e non avvia acquisizioni reali.

Verificare inoltre rotellina sui testi selezionabili, barra trascinata, PageDown, espansione gruppi e DPI 100/150/200%; pulsante, icona tray, taskbar, X, Esci ed Explorer riavviato. Ripetere durante l'arrivo di campioni e metadati. Il log deve riportare `Riduzione verificata R26: HWND ridotto=True`, e la finestra deve rimanere ridotta. Nessun costo o risparmio RAM misurato qui.

## Riferimenti

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow
- https://learn.microsoft.com/en-us/dotnet/framework/wpf/controls/scrollviewer-styles-and-templates

Nessuna modifica a Core, acquisizioni Windows, Guardian, MemoryAgent, profiler o monitor RAM. Restano i limiti di acquisizione, privacy e conteggio già documentati in R25. Disposizione e testi non pertinenti restano invariati.
