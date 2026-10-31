# ProcessLens — R10 revisione 11 R2 · UX writing

Revisione della candidata R10 R10. Testi diretti, log telegrafici e quantità separate in righe leggibili. Restano inclusi lo spazio di analisi ridimensionabile, Guardian, il monitor RAM e la correzione della copia.

## Correzione R11 R2

I due log Windows ricevuti riportano lo stesso arresto in CopyTests, riga 95: il riepilogo produceva già «3 voci · ultima: ESITO · 23:31:34.611», ma il test attendeva ancora «3 voci nel registro. Ultima voce: ESITO · 23:31:34.611.». Allineata l’asserzione al formato compatto. Aggiunti casi C# per registro vuoto, ultima voce senza timestamp e singolo evento con continuazione nelle culture it-IT/en-US. Corretto anche il singolare «1 voce». Conteggi, timestamp, parsing e copia del log originale restano invariati.

Esteso il controllo di sorgente alle asserzioni di uguaglianza che confrontano stringhe letterali con vecchi testi interpolati. La mutazione reintroduce l’esatta frase che ha causato l’arresto e deve essere rifiutata. È un controllo mirato di sorgente, non un parser C# né un’esecuzione della suite; non copre tutte le espressioni concatenate o multilinea. Le fixture del vecchio formato RAM restano intenzionalmente presenti per la compatibilità.

Il log ricevuto conferma il completamento di PresentationTests nelle due culture e di CopyTests fino alla verifica delle continuazioni e del percorso del log. Non documenta le fasi successive. La suite .NET/WPF completa resta da rieseguire su Windows.

## Correzione R11 R1

Il log Windows fornito dall’utente segnala un arresto in MemoryTests: etichetta, testo numerico e valore erano corretti; il test cercava ancora «Private Working Set» nella descrizione riscritta. Aggiornata l’asserzione a «Pagine private presenti in RAM». Nessuna modifica ai calcoli o ai contatori.

Aggiunto un controllo di sorgente sui quattro casi di visualizzazione RAM e una mutazione che reintroduce esattamente il frammento obsoleto: il controllo deve rifiutarla. Restano tutte le verifiche C# su priorità del privato, fallback totale, N/D, zero misurato e culture it-IT/en-US. La suite Windows completa resta da rieseguire: le fasi dopo MemoryTests non risultano eseguite nel log ricevuto.

## Vocabolario applicato

| Prima | Ora |
|---|---|
| Analisi basata su evidenze | Badge rimosso |
| I processi più recenti sono mostrati per primi… | Seleziona un processo per analizzarlo. |
| N processi figli omonimi riportati | N sottoprocessi |
| Ruolo indicativo | Ruolo stimato |
| Funzione non determinata | Funzione sconosciuta |
| funzione specifica non classificata | Non classificato |
| RAM privata residente | Working Set privato |
| SALDO IN DIMINUZIONE… nella finestra | CONSUMO IN CALO… negli ultimi Y min |
| SALDO QUASI INVARIATO | VARIAZIONE NETTA LIMITATA |
| La quota principale… è costituita da… | La memoria impegnata è composta soprattutto da… |
| Non emerge una pressione elevata… | Il carico sulla RAM del sistema è normale. |

«Working Set privato» mantiene il nome del contatore: pagine private presenti in RAM. «RAM privata attiva» non viene usato, perché potrebbe suggerire che quelle pagine siano state usate di recente. Il totale include pagine private e condivisibili; il commit resta un contatore distinto. N/D resta N/D anche nei report brevi.

La tendenza riporta durata e delta. Valori iniziale e finale vicini non diventano «consumo stabile»: minimo, picco e oscillazioni restano visibili. Il calo non viene attribuito automaticamente a garbage collection o svuotamento cache. Le pagine RW sono chiamate pagine in lettura/scrittura: non bastano a identificare heap e oggetti.

## Sintesi RAM

Esempio di formato, non una misurazione:

```text
La memoria impegnata è composta soprattutto da allocazioni private: 638,3 MiB.
• Pagine private in lettura/scrittura (RW): 560,8 MiB.
• Blocchi ≥4 MiB: 31 · totale 407,5 MiB.

CONSUMO IN CALO: -20,0 MiB negli ultimi 2,0 min.
```

Tolte le parentesi annidate. Quantità, variazione e stato della RAM del sistema occupano righe separate. Anche il rapporto delle allocazioni separa regioni, pagine campionate e contatori PSAPI. Le indicazioni sui componenti tipici rimangono sotto «COMPONENTI TIPICI · NON MISURATI».

La differenza tra commit e Working Set privato è indicata come differenza tra contatori: non viene trasformata in una misura esatta delle pagine fuori RAM. La frase sulle pagine non residenti è una definizione generale, non l'attribuzione di quel delta a oggetti o allocazioni specifiche.

## Log

Nuovo formato del registro RAM; numeri dell'esempio dimostrativi:

```text
[13:14:15.123] [RAM] PSAPI: Working Set privato: 26,0 MiB
[13:14:15.456] [RAM] STORICO: 3 campioni · 60 s -> delta -2,0 MiB
```

Il monitor separato usa `[timestamp UTC] [RAM Monitor] Evento: dati`. Una riga per evento; PID, valori, errori e ambito restano presenti. I valori parziali restano segnalati; il consumo del misuratore resta separato.

Il parser legge sia il nuovo formato sia `[ora] [VERIFICA RAM] FASE — messaggio`. Conserva continuazioni e testo sorgente. Non inventa timestamp per eventi che ne sono privi. La copia integrale usa il rapporto originale, senza ricostruirlo dai controlli visibili. Le schede del registro sono più compatte, con testo selezionabile e a capo.

CSV e JSON mantengono schema, nomi dei campi, codici e valori macchina. Sono riscritti i messaggi destinati alla lettura umana, compresi i testi descrittivi del monitor. Nessuna traduzione degli identificativi diagnostici o degli stati macchina.

## Test e controlli

Aggiornate le asserzioni C# di terminologia, presentazione, comportamento, copia e WPF, più le suite memoria, origine, servizi, cronologia, Guardian e monitor che verificano i testi cambiati. Le nuove regressioni coprono log nuovi e precedenti, culture it-IT/en-US, quantità delle allocazioni su righe distinte e conservazione dei limiti tecnici. Le verifiche di identità, dati mancanti, contatori e privacy non sono state disabilitate.

Aggiornati i controlli PowerShell e Python indicati nella richiesta e quelli che controllavano testi effettivamente cambiati. `check_r10.py` e `check_workspace_r10r10.py` riconoscono soltanto le modifiche di sorgente registrate con hash nel delta editoriale; non equiparano più una stringa cambiata a una modifica arbitraria dei collector. La verifica corrente `check_ux_writing_r10r11.py` confronta anche condizioni, soglie numeriche e criteri delle regole con la candidata precedente.

Verifiche disponibili qui: sorgenti Python/XML, controlli di CPU/ripristino e monitor/navigazione, confronto dei componenti esclusi, patch applicata e confrontata byte per byte, manifest e CRC ZIP. I confronti di sorgente non sono prove di equivalenza semantica o esecuzioni C#.

**Compilazione, test .NET/WPF, PowerShell e prova visuale DPI NON ESEGUITI qui: SDK .NET e PowerShell non disponibili. RAM/CPU NON MISURATE.** Gli audit precedenti rimangono storici; i risultati correnti sono sotto `audit/R10_R11`.

Su Windows:

```powershell
.\build-dashboard.ps1
```

Dopo la build, provare:

1. Analisi RAM con almeno tre campioni: sintesi, quantità, tendenza e copia del rapporto completo.
2. Registro RAM: nuova sintassi, selezione del testo e copia. Nessun valore mancante deve diventare zero.
3. Monitor RAM: `Avvia-ProcessLens-con-log-RAM.cmd`, log per PID, campioni parziali e monitor escluso.
4. Ruoli stimati, sconosciuti e sottoprocessi; nessuna probabilità inventata o attività attribuita dal solo ruolo.
5. Scala 100%, 125%, 150%, 200%: etichette, wrapping, pannello espanso e pulsanti, entro la superficie disponibile.

## Consegna e ambito

Registro stringhe corrente: `audit/R10_R11/string-ledger.csv`. Il registro richiesto `audit/R10_R10/string-ledger.csv` conserva le righe precedenti e aggiunge le sostituzioni R11. Glossario aggiornato in `docs/terminology/GLOSSARIO_TERMINOLOGICO_PROCESSLENS.csv`. Patch completa R10→R11 e manifest SHA256 inclusi.

`ProcessDisplayFormatter.cs` era già numerico e telegrafico: formati, arrotondamenti e gestione N/D restano invariati. `MemoryEvidence.cs` contiene tipi e calcoli, senza messaggi da riscrivere: resta invariato. Sono stati inclusi anche `ProcessQueryEngine.cs`, vero generatore della sintesi con parentesi annidate, e i testi di presentazione della knowledge base. ID, criteri, punteggi e codici di rischio delle regole non cambiano.

Acquisizione Windows, servizio Guardian, reader SQLite, MemoryAgent, profiler e acquisizione del monitor restano identici alla candidata precedente. Nessuna installazione, profilazione o terminazione avviata. Una sola cartella sorgente attiva; baseline consegnata conservata e cartella temporanea del confronto rimossa dopo verifica.
