$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK non trovato. Installa/abilita .NET 8 SDK e riprova.'
}

if (-not (Test-Path .\ProcessLens.sln)) {
    dotnet new sln -n ProcessLens
    dotnet sln .\ProcessLens.sln add .\src\ProcessLens.Core\ProcessLens.Core.csproj
    dotnet sln .\ProcessLens.sln add .\src\ProcessLens.Windows\ProcessLens.Windows.csproj
    dotnet sln .\ProcessLens.sln add .\src\ProcessLens.Semantics\ProcessLens.Semantics.csproj
    dotnet sln .\ProcessLens.sln add .\src\ProcessLens.App\ProcessLens.App.csproj
}

Write-Host 'Preflight WPF menu contestuale...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'tests\check-wpf-contextmenu.ps1')
Write-Host 'Preflight UAC amministratore...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'tests\check-elevation.ps1')
Write-Host 'Build Release di ProcessLens...' -ForegroundColor Cyan
dotnet build .\ProcessLens.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build fallita. ProcessLens non sarà avviato.' }

Write-Host 'Test deterministici CPU...' -ForegroundColor Cyan
dotnet run --configuration Release --project .\tests\ProcessLens.CpuTests\ProcessLens.CpuTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test CPU falliti. ProcessLens non sarà avviato.' }

# Roslyn/MSBuild can keep compiler-server processes alive after a development build.
# Shut them down before launching ProcessLens so the test itself does not leave
# a cluster of ".NET Host" processes in Task Manager.
Write-Host 'Test deterministici memoria...' -ForegroundColor Cyan
dotnet run --configuration Release --project .\tests\ProcessLens.MemoryTests\ProcessLens.MemoryTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test memoria falliti. ProcessLens non sara avviato.' }

Write-Host 'Test di sicurezza della terminazione...' -ForegroundColor Cyan
dotnet run -c Release --project .\tests\ProcessLens.TerminationTests\ProcessLens.TerminationTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test terminazione falliti. ProcessLens non sara avviato.' }

Write-Host 'Test policy arresto ordinato servizi...' -ForegroundColor Cyan
dotnet run -c Release --project .\tests\ProcessLens.ServiceStopTests\ProcessLens.ServiceStopTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test arresto servizi falliti. Avvio annullato.' }

Write-Host 'Test deterministici della spiegazione servizi...' -ForegroundColor Cyan
dotnet run --configuration Release --project .\tests\ProcessLens.ServiceTests\ProcessLens.ServiceTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test servizi falliti. ProcessLens non sara avviato.' }

Write-Host 'Test cronologia SCM e attribuzione prudente...' -ForegroundColor Cyan
dotnet run --configuration Release --project .\tests\ProcessLens.ServiceHistoryTests\ProcessLens.ServiceHistoryTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test cronologia servizi falliti. Avvio annullato.' }

Write-Host 'Chiusura build-server .NET usati per la compilazione...' -ForegroundColor DarkGray
dotnet build-server shutdown | Out-Host

$exe = Join-Path $PSScriptRoot 'src\ProcessLens.App\bin\Release\net8.0-windows\ProcessLens.exe'
if (-not (Test-Path $exe)) {
    throw "Eseguibile non trovato dopo la build: $exe"
}

Write-Host ''
Write-Host 'Build completata. Avvio diretto di ProcessLens.exe (non dotnet run).' -ForegroundColor Green
$diagnosticLog = Join-Path $env:LOCALAPPDATA 'ProcessLens\logs\startup.log'
Write-Host "Registro diagnostico: $diagnosticLog" -ForegroundColor DarkCyan
Write-Host 'Windows potrebbe mostrare la richiesta UAC. Conferma per avviare ProcessLens con privilegi amministrativi.' -ForegroundColor Cyan
try {
    $started = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru -ErrorAction Stop
} catch {
    throw ('Impossibile avviare ProcessLens: UAC rifiutato o errore Windows. ' + $_.Exception.Message)
}
# An active PID is not sufficient: v0.11.2 crashed during WPF XAML loading,
# sometimes after the three-second check had already announced success.
$windowCreated = $false
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    Start-Sleep -Milliseconds 500
    $started.Refresh()
    if ($started.HasExited) { break }
    if ($started.MainWindowHandle -ne [IntPtr]::Zero) {
        $windowCreated = $true
        break
    }
}
if ($started.HasExited) {
    $otherInstance = Get-Process -Name ProcessLens -ErrorAction SilentlyContinue |
        Where-Object { $_.Id -ne $started.Id } | Select-Object -First 1
    if ($null -ne $otherInstance) {
        Write-Warning "Il nuovo processo si e' chiuso (codice $($started.ExitCode)), ma un'altra istanza ProcessLens PID $($otherInstance.Id) e' ancora attiva."
    } else {
        Write-Warning "ProcessLens si e' chiuso subito (ExitCode: $($started.ExitCode))."
    }
    if (Test-Path $diagnosticLog) {
        Write-Host 'Ultime righe del registro diagnostico:' -ForegroundColor Yellow
        Get-Content $diagnosticLog -Tail 35
    } else {
        Write-Warning 'Nessun log prodotto: controlla il registro eventi di Windows con .\diagnose-startup.ps1 -NoLaunch.'
    }
    Write-Host 'Per raccogliere eventi .NET Runtime / Application Error: .\diagnose-startup.ps1 -NoLaunch' -ForegroundColor Cyan
    if ($null -eq $otherInstance) { throw 'ProcessLens terminato prima di mostrare la finestra: controlla la diagnostica riportata sopra.' }
} elseif ($windowCreated) {
    Write-Host "ProcessLens: finestra creata (PID $($started.Id), Handle $($started.MainWindowHandle))." -ForegroundColor Green
} else {
    Write-Warning "ProcessLens PID $($started.Id) e' attivo, ma non ha una finestra principale dopo 10 secondi."
    if (Test-Path $diagnosticLog) {
        Get-Content $diagnosticLog -Tail 35
    }
    Write-Warning "Verifica anche .\diagnose-startup.ps1 -NoLaunch e l'eventuale finestra nascosta."
}
