[CmdletBinding()]
param([switch]$RunCapture)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

Write-Host '=== ProcessLens v0.19.12 - Verifica profiler .NET ===' -ForegroundColor Cyan
Write-Host 'Controllo non invasivo: nessun dump, nessuna GC forzata.'
& (Join-Path $PSScriptRoot 'profiler-status.ps1')

$helper = Join-Path $PSScriptRoot 'publish\win-x64\Profiler\ProcessLensProfiler.exe'
$fixtureProject = Join-Path $PSScriptRoot 'tests\ProcessLens.ProfilerFixture\ProcessLens.ProfilerFixture.csproj'
$fixtureExecutable = Join-Path $PSScriptRoot 'tests\ProcessLens.ProfilerFixture\bin\Release\net8.0\ProcessLens.ProfilerFixture.exe'
$tool = Get-Command 'dotnet-gcdump.exe' -ErrorAction SilentlyContinue
if ($null -eq $tool) {
    $candidate = Join-Path $env:USERPROFILE '.dotnet\tools\dotnet-gcdump.exe'
    if (Test-Path -LiteralPath $candidate) { $tool = Get-Item -LiteralPath $candidate }
}

if (-not $RunCapture) {
    Write-Host ''
    Write-Host '[SOLO VERIFICA] La profilazione non e stata avviata.' -ForegroundColor Green
    Write-Host 'Per provare un processo sintetico usa: .\verify-dotnet-profiler.ps1 -RunCapture'
    Write-Host 'La prova richiede una conferma aggiuntiva digitando PROFILA.'
    return
}

Write-Host ''
Write-Warning 'La prova induce una GC Gen2 completa nel solo processo di test creato dallo script.'
Write-Warning 'Possibili pause e memoria extra. Non usare se il PC e gia sotto carico.'
$answer = Read-Host 'Per avviare una singola prova su un target .NET sintetico, scrivi PROFILA'
if ($answer -cne 'PROFILA') {
    Write-Host '[ANNULLATO] Nessuna acquisizione eseguita.'
    return
}

if (-not (Test-Path -LiteralPath $helper)) {
    throw 'Profiler non pubblicato. Esegui prima AVVIA-TUTTO.cmd e verifica la build.'
}
if ($null -eq $tool) {
    throw 'dotnet-gcdump.exe non trovato. Nessun download o installazione automatica.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'SDK .NET non rilevato: impossibile compilare il processo sintetico.'
}

# Extra conservative preflight, in addition to the enforced 85 percent gate in the profiler.
$os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
$total = [double]$os.TotalVisibleMemorySize
$free = [double]$os.FreePhysicalMemory
if ($total -le 0 -or $free -lt 0 -or $free -gt $total) {
    throw 'RAM fisica non misurabile: prova annullata.'
}
$usedPercent = 100 * ($total - $free) / $total
Write-Host ('RAM sistema prima della prova: {0:N1}%' -f $usedPercent)
if ($usedPercent -ge 80) {
    throw 'RAM sistema almeno all 80 percento: prova rimandata, nessuna acquisizione.'
}

Write-Host '[1/3] Compilazione del solo processo sintetico (nessuna profilazione)...' -ForegroundColor Cyan
& dotnet build $fixtureProject -c Release --nologo
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $fixtureExecutable)) {
    throw 'Build del processo sintetico non riuscita. Nessuna acquisizione.'
}
# Recheck system pressure after build, because compiling can increase memory use.
$osAfterBuild = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
$systemTotal = [double]$osAfterBuild.TotalVisibleMemorySize
$systemFree = [double]$osAfterBuild.FreePhysicalMemory
if ($systemTotal -le 0 -or $systemFree -lt 0 -or $systemFree -gt $systemTotal -or
    (100 * ($systemTotal - $systemFree) / $systemTotal) -ge 80) {
    throw 'RAM non misurabile o oltre 80 percento dopo la build: acquisizione annullata.'
}

$child = $null
$session = Join-Path $env:TEMP ('ProcessLensValidation-' + [Guid]::NewGuid().ToString('N'))
New-Item -Path $session -ItemType Directory -Force | Out-Null
try {
    Write-Host '[2/3] Avvio target locale .NET, senza dati reali...' -ForegroundColor Cyan
    $child = Start-Process -FilePath $fixtureExecutable -ArgumentList @('--seconds', '150') -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3
    $child.Refresh()
    if ($child.HasExited) { throw 'Il processo sintetico e terminato prima della raccolta.' }
    $pidFixture = $child.Id
    $startTicks = $child.StartTime.ToUniversalTime().Ticks
    $child.Refresh()
    $beforeCpu = $child.TotalProcessorTime.TotalSeconds
    $beforeWorkingSet = $child.WorkingSet64
    Write-Host ('Target sintetico: PID ' + $pidFixture + '; oggetti noti nel managed heap.')
    Write-Host '[3/3] Acquisizione tramite profiler esterno (GC Gen2 nel solo target sintetico)...' -ForegroundColor Cyan
    $report = Join-Path $session 'result.json'
    $sessionGuid = [Guid]::NewGuid().ToString('N')
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    & $helper --mode managed-heap --pid $pidFixture --start-utc-ticks $startTicks --output $report --session-guid $sessionGuid
    $workerExitCode = $LASTEXITCODE
    $watch.Stop()
    $child.Refresh()
    $targetStillRunning = -not $child.HasExited
    Write-Host ('Durata acquisizione: {0:N1} s; codice profiler: {1}' -f $watch.Elapsed.TotalSeconds, $workerExitCode)
    if ($targetStillRunning) {
        $cpuDelta = $child.TotalProcessorTime.TotalSeconds - $beforeCpu
        $residentDeltaMiB = ($child.WorkingSet64 - $beforeWorkingSet) / 1MB
        Write-Host ('CPU usata dal target durante la finestra: {0:N2} s; variazione working set: {1:N2} MiB.' -f $cpuDelta, $residentDeltaMiB)
        Write-Host 'Queste differenze non misurano la durata della pausa GC.'
    }
    if (-not (Test-Path -LiteralPath $report)) { throw 'Nessun report JSON prodotto dal profiler.' }
    $data = Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($workerExitCode -ne 0 -or $data.Status -ne 'ok' -or -not $data.Complete) {
        Write-Warning ('Profiler non verificato: ' + $data.Detail)
        throw 'Acquisizione non completata; nessun risultato heap dichiarato valido.'
    }
    if ($data.Pid -ne $pidFixture -or $data.ManagedHeapBytes -le 0 -or $data.ManagedObjects -le 0) {
        throw 'Il report non soddisfa i requisiti di coerenza del target e delle statistiche.'
    }
    $knownType = @($data.TopTypes | Where-Object { $_.TypeName -like '*KnownPayload*' })
    Write-Host ('Heap GC gestito: {0:N2} MiB; oggetti riportati: {1}' -f ($data.ManagedHeapBytes / 1MB), $data.ManagedObjects)
    if ($knownType.Count -gt 0) {
        Write-Host ('PASS: tipo sintetico KnownPayload riconosciuto tra i primi ' + @($data.TopTypes).Count + ' tipi.') -ForegroundColor Green
    } else {
        Write-Warning 'Tipo KnownPayload non presente fra i primi tipi: rapporto valido, ma prova di riconoscimento specifico non conclusiva.'
    }
    Write-Host 'Non e stato profilato alcun processo reale o di sistema.' -ForegroundColor Green
    Write-Host 'Nessuna diagnosi di memory leak ricavabile da questa singola acquisizione.'
} finally {
    if ($null -ne $child) {
        try {
            $child.Refresh()
            if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit(5000) | Out-Null }
        } catch { Write-Warning 'Non e stato possibile confermare la chiusura del solo target sintetico.' }
        $child.Dispose()
    }
    Remove-Item -LiteralPath $session -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host '[PULIZIA] Cartella temporanea della prova eliminata quando possibile; nessun dump conservato volontariamente.'
}
