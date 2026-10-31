# ProcessLens v0.19.16 R10 R2 — layout del controllo nel test WPF

Il log Windows ricevuto il 17 ottobre 2026 mostra un'asserzione fallita in `ProcessLens.CopyUiTests`: `multiline output measures at finite height`. Le sei verifiche precedenti di sola lettura, copia Unicode e selezione sono PASS. Anche CopyTests e PresentationTests risultano completati; le porzioni di BehaviorTests e TerminologyTests presenti nel log terminano con successo. Il log inizia a metà suite e non dimostra l'esecuzione delle fasi omesse. Le fasi successive al blocco non sono state eseguite; il launcher riporta Guardian non modificato.

È un errore a runtime nel test, non un errore del compilatore, anche se il wrapper lo classifica genericamente come PL-BUILD-5002. Il vecchio test istanziava un controllo WPF isolato e lo misurava senza inizializzarlo tramite XAML, un albero visuale o `BeginInit`/`EndInit`. Il log non registrava né template né altezza effettiva: non permette di dimostrare retroattivamente il valore misurato.

La correzione segue il ciclo documentato da Microsoft per elementi fuori da un albero: `BeginInit`, proprietà, `EndInit`, quindi misura, disposizione e aggiornamento layout. Nessun template artificiale o stile di test sostituisce il tema nativo.

Prima: un'unica misura senza inizializzazione e un messaggio privo dei valori ottenuti.

Dopo:

- Inizializzazione esplicita del solo controllo isolato della suite.
- Verifica che il template nativo contenga `PART_ContentHost`.
- Conservata l'asserzione originale: altezza finita e maggiore di 20 DIP.
- Verifica di layout valido e almeno tre righe reali.
- Confronto a 210 e 70 DIP: restringendo il campo devono aumentare righe e altezza.
- Verifica che layout e wrapping preservino testo, selezione e copia Unicode.
- Diagnostica con stato di inizializzazione, presenza del template e dimensioni effettive.

I test usano il destinatario fittizio della copia. Non mostrano finestre e non avviano monitor, acquisizioni o profiler. I sorgenti applicativi C# e XAML restano identici alla R1; cambia soltanto il programma della suite WPF, oltre a versione, documentazione e controlli di sorgente. Le criticità RAM già documentate restano aperte. Nessuna riduzione di memoria misurata.

Verifiche eseguite qui: controlli strutturali Python/XML, confronto esatto con la R1, applicazione reale delle patch, integrità ZIP e manifest SHA-256. **NON ESEGUITO:** nuova suite CopyUiTests, altre suite .NET, PowerShell, build/pubblicazione WPF, verifica visuale e DPI. Il risultato della correzione richiede la nuova esecuzione Windows; i controlli di sorgente non la sostituiscono.

Estrarre in una cartella nuova, poi nella radice contenente `src` e `tests`:

```powershell
.\build-dashboard.ps1
```

Se la sequenza termina con successo:

```powershell
.\run-release.ps1
```

Il testo `┬á` visibile nel log dei test numerici è compatibile con una decodifica console errata dello spazio non separabile; i confronti di formato sono PASS. Questo log non dimostra un difetto della stringa WPF. Nessuna conversione dei dati o modifica del formato delle unità viene introdotta in questo hotfix.

Audit corrente: `audit/R10_R2/`; audit delle revisioni precedenti conservati come storici. Manifest corrente `SHA256SUMS_R10.txt`.

Fonti primarie:

- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/initialization-for-object-elements-not-in-an-object-tree
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.applytemplate?view=windowsdesktop-8.0
