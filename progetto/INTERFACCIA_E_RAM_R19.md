# ProcessLens v0.19.16 — candidata R19

La R19 parte dalla R18 R2 e aggiunge la RAM del sistema alla barra superiore, subito dopo CPU logici. Esempio con 16 GiB totali e 6 GiB disponibili:

**RAM sistema: 10,0 / 16,0 GiB · 62,5 %**

Il primo valore è la RAM fisica in uso; il secondo è il totale utilizzabile da Windows. La percentuale usa gli stessi contatori: `(totale − disponibile) / totale × 100`. I valori seguono la cultura corrente: virgola in it-IT, punto in en-US. Le unità sono GiB (1.073.741.824 byte).

Il dato proviene dalla lettura già disponibile di `GlobalMemoryStatusEx`, eseguita nel worker CPU a intervalli nominali di due secondi. Non sono stati aggiunti timer, scansioni dei processi o storici. Una lettura RAM fallita non scarta il campione CPU. Letture mancanti o incoerenti mostrano N/D; quando la finestra è ridotta l'indicatore mostra Sospeso. I risultati tardivi non vengono pubblicati dopo la sospensione.

La RAM della sola GUI rimane distinta nell'espansore Risorse di ProcessLens. Il nuovo indicatore è globale: non somma i Working Set e non rappresenta il commit. Nessuna nuova misura del consumo di ProcessLens è dichiarata da questa revisione.

## Schermata Sistema Windows

- Tabella con righe alternate, soli separatori orizzontali, celle più distanziate e intestazioni leggibili.
- Dettagli in un riquadro separato, con testo più grande e titolo dedicato.
- Selettore dell'istanza e pulsante Analizza PID selezionato nello stesso riquadro.
- Separatore trascinabile fra tabella e dettagli; il testo rimane selezionabile e copiabile.
- Informazioni di copertura del catalogo spostate nell'espansore esistente Memoria del kernel, fonte e copertura.

L'elenco dei componenti, i dati letti e le regole di classificazione non cambiano. Restano l'aggiornamento manuale, la creazione della vista su richiesta e la virtualizzazione della tabella.

## Verifiche

- PASS: 86 asserzioni .NET di presentazione su Linux, di cui 18 nuove su RAM e culture it-IT/en-US.
- PASS: controllo tipi C# di sei assembly con riferimenti WindowsDesktop e stub dei campi generati dal markup; zero warning. Non è compilazione XAML/BAML.
- PASS: 30 controlli statici eseguiti con PowerShell 7.4.21 su Linux, inclusa la sintassi dei 69 script.
- PASS: controlli di sorgente e di conservazione, con registrazione degli hash e del diff completo.
- Prove conservate: 332 asserzioni portabili di rete e sistema, con input identici verificati; non rieseguite.

**Non eseguiti:** compilazione completa su Windows, Windows PowerShell 5.1, esecuzione WPF, verifica visiva a differenti DPI, confronto live con Gestione attività e misurazione dell'impatto RAM/CPU. Non è stata eseguita alcuna cattura reale di rete.

## Prova su Windows

Estrarre in una nuova cartella e avviare dalla radice del progetto:

```powershell
.\build-dashboard.ps1
```

Aprire ProcessLens e verificare:

1. RAM sistema visibile dopo CPU logici in tutte le schede; valore, totale e percentuale leggibili. Su una finestra più stretta la barra può andare a capo.
2. Ridurre e ripristinare la finestra: il campionamento si sospende e riprende secondo il comportamento della CPU.
3. Aprire Sistema Windows, aggiornare il campione, selezionare Memory Compression o un host servizi e scegliere un'istanza. Verificare tabella, dettagli, copia e navigazione.
4. Trascinare il separatore fra tabella e dettagli; provare DPI 100%, 150% e 200%, tenendo conto dell'area utile dello schermo.

Le prove e il registro delle stringhe sono in `audit/R10_R19`. Il manifest completo è `SHA256SUMS_R10.txt`. Nessuna modifica a Guardian, MemoryAgent, profiler, API di acquisizione o identificativi knowledge.
