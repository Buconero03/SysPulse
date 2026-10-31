# Verifica Windows — ProcessLens R7 candidata

## Prerequisiti e stato

Su Work/Linux: **NON ESEGUITO** per tutti i comandi .NET/PowerShell/WPF riportati qui. SDK .NET 8 compatibile e .NET Desktop Runtime devono già essere disponibili su Windows; nessuna installazione viene eseguita dagli strumenti nuovi. Conservare la R6 originale.

Estrarre l’unico ZIP R7 in una directory radice propria; sorgenti, audit, test e risultati restano in quella radice. Non copiare gli audit nella home e non creare una successione di ZIP intermedi. La versione R7 è candidata e non sostituisce automaticamente una distribuzione stabile.

## Suite e build senza avviare componenti operativi

Da PowerShell nella radice estratta:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\verify-candidate-r7.ps1
```

Il verificatore controlla tutti gli script `tests/check-*.ps1`, esegue tutti i progetti di test, compila soltanto la fixture del profiler e compila tutti i progetti `src`. Compila anche Guardian/MemoryAgent/Profiler, **senza installarli o avviarli**. Esegue il test terminologico anche senza la knowledge copiata; il backup è ripristinato in `finally`. Non tocca la knowledge di sorgente. Output e exit code sono salvati in `verification-r7/test-results.csv` e nei log dello stesso sottoalbero. Esito diverso da zero o controllo non eseguito impediscono una conclusione positiva.

Verifiche rapide riproducibili:

```powershell
dotnet run --project .\tests\ProcessLens.TerminologyTests\ProcessLens.TerminologyTests.csproj -c Release
dotnet run --project .\tests\ProcessLens.BehaviorTests\ProcessLens.BehaviorTests.csproj -c Release
dotnet run --project .\tests\ProcessLens.MemoryTests\ProcessLens.MemoryTests.csproj -c Release
dotnet run --project .\tests\ProcessLens.ServiceTests\ProcessLens.ServiceTests.csproj -c Release
```

Controllare `$LASTEXITCODE` dopo ciascun comando. Il verificatore lo registra automaticamente; un messaggio stampato dal test non sostituisce l’exit code.

## Pubblicazione e avvio per controllo manuale

Solo dopo la suite positiva, usare il flusso esistente:

```powershell
.\build-dashboard.ps1
.\AVVIA-TUTTO.cmd
```

`build-dashboard.ps1` pubblica i binari senza installare servizi o attivare gli avvisi. Il launcher può chiedere l’attivazione facoltativa degli avvisi: per questa revisione premere Invio alla richiesta `ATTIVA`. Non invocare installer/upgrade Guardian, `PROFILA`, `-RunCapture` o operazioni di terminazione. Il programma può richiedere UAC secondo il manifest esistente. Non interpretare la mera creazione del processo come prova della finestra o dei dati.

## Matrice manuale da compilare

| Scenario | Dati / comportamento da verificare | Esito iniziale |
|---|---|---|
| DPI 100%, 125%, 150%; 1180×800 e minimo 950×680 DIP | Niente sovrapposizioni; nomi lunghi, badge, testo della terna memoria, menu e guida leggibili; scroll e tastiera utilizzabili. A 150% il minimo può eccedere lo schermo: registrare area utile e limite. | NON ESEGUITO |
| Python/Conda: script, `-m`, `-c`, multiprocessing | Padre proposto senza attribuire l’avvio a Conda; script/modulo dal testo, nessun codice inline o credenziale in sintesi. | NON ESEGUITO |
| Node, .NET/MSBuild/Roslyn, Java, PowerShell | Parametri riconosciuti, esecuzione interna e richiedente non inventati; XML/API/contratti invariati. | NON ESEGUITO |
| Chromium/Electron: renderer, GPU, utility Node/network | Parametri come fonte; GPU non significa utilizzo GPU misurato; servizio interno distinto da SCM. | NON ESEGUITO |
| TCP LISTEN, ESTABLISHED, UDP, solo socket di famiglia | Conteggi distinti; nessuna prova di byte, richieste, delega o reachability; LISTEN non sparisce se la famiglia ha rete. | NON ESEGUITO |
| Processo protetto o lettura negata/parziale | N/D per contatori disponibili; zero socket non garantisce assenza; limiti consultabili. Documentare eventuali default non distinguibili. | NON ESEGUITO |
| Memoria: PWS, WS totale, commit; tre campioni con picco intermedio | MiB e nome contatore esatti; no somme doppie; saldo diverso da stabilità; limite runtime esplicito. | NON ESEGUITO |
| CPU primo campione, media breve, bassa/alta | N/D senza campioni; finestra fino a 30 s; CPU non misura progresso e non esclude blocchi. | NON ESEGUITO |
| Servizio automatico/manuale/arrestato e host condiviso | Configurazione distinta da causa; capacità di arresto su conferma descritta correttamente. Aprire e annullare dialogo, senza arrestare. | NON ESEGUITO |
| Query progresso/ETA e testo-obiettivo | Fonte del valore esplicita; niente confidenza statistica o certezza del lavoro corrente. | NON ESEGUITO |
| Guardian e agente già presenti | Consultazione soltanto; schema/log/codici/policy conservati; nessun riavvio o installazione per il controllo. | NON ESEGUITO |

Conservare screenshot solo dell’applicazione e dati sintetici, senza percorsi/credenziali personali. Inserire esiti, timestamp, versione Windows/.NET e dimensioni reali in `verification-r7/`. Nessuna promozione automatica alla fine della procedura.

## Integrità

`SHA256SUMS_R7.txt` copre tutti i file dello ZIP eccetto il manifest stesso. I manifesti precedenti sono documentazione storica della base; non vanno usati per verificare R7.

```powershell
$bad = @()
Get-Content .\SHA256SUMS_R7.txt | ForEach-Object {
    if ($_ -match '^([0-9a-f]{64})  (.+)$') {
        $expected = $Matches[1]; $file = $Matches[2]
        if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant() -ne $expected) { $bad += $file }
    }
}
if ($bad.Count) { throw ('Hash non validi: ' + ($bad -join ', ')) }
```

Il diff `audit/R7/R6_to_R7.patch` usa percorsi `a/` e `b/`; se si preferisce applicarlo alla R6 invece dell’estrazione completa, usare una copia pulita R6 e `git apply --check` prima di `git apply`. La candidata completa è la consegna principale.
