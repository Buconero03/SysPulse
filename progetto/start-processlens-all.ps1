param(
    [ValidateSet('Setup', 'Quick')]
    [string]$Mode = 'Setup'
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
. (Join-Path $PSScriptRoot 'build-diagnostics.ps1')

function Write-Step([string]$Title) {
    Write-Host ''
    Write-Host ('========== ' + $Title + ' ==========') -ForegroundColor Cyan
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Show-AgentState {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $configured = (Get-ItemProperty -Path $key -Name 'ProcessLensMemoryAlerts' -ErrorAction SilentlyContinue).ProcessLensMemoryAlerts
    $target = Join-Path $env:LOCALAPPDATA 'ProcessLens\MemoryAgent\ProcessLens.MemoryAgent.exe'
    $running = @(Get-Process -Name 'ProcessLens.MemoryAgent' -ErrorAction SilentlyContinue)
    return [PSCustomObject]@{ Configured = -not [string]::IsNullOrWhiteSpace($configured); Target = $target; Running = $running.Count -gt 0 }
}

try {
    Write-Host 'ProcessLens v0.19.16 R10 R7 candidata - Avvio guidato in sequenza' -ForegroundColor Green
    Write-Host ('Modalita: ' + $Mode)
    Write-Host ('Cartella: ' + $PSScriptRoot)
    $isAdmin = Test-IsAdministrator
    if ($isAdmin) {
        Write-Warning 'PowerShell e in modalita amministratore. La prima installazione degli avvisi RAM richiede una sessione NORMALE; sara saltata.'
    }

    Write-Step '1/5 - Verifica/compilazione'
    $guiExe = Join-Path $PSScriptRoot 'publish\win-x64\ProcessLens.exe'
    $agentExe = Join-Path $PSScriptRoot 'publish\MemoryAgent-win-x64\ProcessLens.MemoryAgent.exe'
    if ($Mode -eq 'Setup') {
        Write-Host 'Eseguo test, compilazione WPF e pubblicazione MemoryAgent...'
        & (Join-Path $PSScriptRoot 'build-dashboard.ps1')
        if (-not (Test-Path -LiteralPath $guiExe)) {
            throw 'Build apparentemente terminata, ma ProcessLens.exe non e stato pubblicato.'
        }
        if (-not (Test-Path -LiteralPath $agentExe)) {
            throw 'Build apparentemente terminata, ma MemoryAgent.exe non e stato pubblicato.'
        }
    } elseif (-not (Test-Path -LiteralPath $guiExe)) {
        throw 'Build mancante: eseguire AVVIA-TUTTO.cmd una volta prima dell avvio rapido.'
    } else {
        Write-Host 'Build precedente rilevata; compilazione saltata.' -ForegroundColor Green
    }

    Write-Step '2/5 - Avvisi RAM 85/80'
    $agent = Show-AgentState
    if ($agent.Running) {
        Write-Host 'MemoryAgent e gia in esecuzione. Nessun duplicato avviato.' -ForegroundColor Green
    } elseif ($agent.Configured -and (Test-Path -LiteralPath $agent.Target)) {
        if ($isAdmin) {
            Write-Warning 'Agente gia configurato; non lo avvio da console elevata. Usare AVVIA-VELOCE.cmd senza amministratore.'
        } else {
            Start-Process -FilePath $agent.Target -WorkingDirectory (Split-Path -Path $agent.Target)
            Write-Host 'MemoryAgent gia autorizzato: avviato nella sessione utente.' -ForegroundColor Green
        }
    } elseif ($Mode -eq 'Setup' -and -not $isAdmin) {
        Write-Host 'Attivazione facoltativa: l installer chiedera di digitare ATTIVA.' -ForegroundColor Yellow
        Write-Host 'Se non vuoi attivare gli avvisi, premi Invio senza scrivere ATTIVA.'
        & (Join-Path $PSScriptRoot 'install-memory-alerts.ps1')
    } elseif ($isAdmin) {
        Write-Warning 'Installazione MemoryAgent saltata: serve PowerShell NON amministrativo.'
    } else {
        Write-Warning 'MemoryAgent non configurato. Esegui AVVIA-TUTTO.cmd per attivarlo.'
    }

    Write-Step '3/5 - Servizio Guardian (sola verifica)'
    $service = Get-Service -Name 'ProcessLensGuardian' -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        Write-Warning 'ProcessLensGuardian non e installato. Non lo installo automaticamente.'
    } else {
        Write-Host ('Guardian: ' + $service.Status + ' | Avvio: ' + $service.StartType)
        if ($service.Status -ne 'Running') {
            Write-Warning 'Guardian non e in esecuzione; non avvio servizi automaticamente.'
        }
    }

    Write-Step '4/5 - Avvio interfaccia ProcessLens'
    Write-Host 'Apro la GUI aggiornata. Se Windows chiede UAC, conferma per utilizzare le funzioni avanzate.'
    $guiProcess = Start-Process -FilePath $guiExe -WorkingDirectory (Split-Path -Path $guiExe) -PassThru -ErrorAction Stop
    Start-Sleep -Seconds 3
    $guiProcess.Refresh()
    if ($guiProcess.HasExited) {
        throw ('La GUI si e chiusa subito dopo l avvio. PID: ' + $guiProcess.Id + '; ExitCode: ' + $guiProcess.ExitCode)
    }
    Write-Host ('ProcessLens avviato. PID: ' + $guiProcess.Id) -ForegroundColor Green

    Write-Step '5/5 - Stato memoria e agenti'
    & (Join-Path $PSScriptRoot 'memory-alerts-status.ps1')
    & (Join-Path $PSScriptRoot 'memory-footprint.ps1')

    Write-Host ''
    Write-Host 'Sequenza terminata. Il servizio Guardian e l agente RAM, se attivi, funzionano indipendentemente da questa finestra.' -ForegroundColor Green
    Write-Host 'Prossimi avvii senza ricompilazione: AVVIA-VELOCE.cmd.' -ForegroundColor Green
    Write-Host 'Profiler heap .NET facoltativo: .\profiler-status.ps1 (non installato automaticamente).' -ForegroundColor Cyan
    Write-Host 'Chiudi pure questa finestra dopo aver letto i risultati.'
} catch {
    Write-Host ''
    Write-Host ('[ERRORE] Operazione interrotta: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('[DETTAGLIO] Tipo eccezione: ' + $_.Exception.GetType().Name) -ForegroundColor Yellow
    Write-Host ('[DETTAGLIO] HRESULT: 0x' + $_.Exception.HResult.ToString('X8')) -ForegroundColor Yellow
    Write-Host ('[DETTAGLIO] Sorgente: ' + $(if ($_.InvocationInfo.ScriptName) { [IO.Path]::GetFileName($_.InvocationInfo.ScriptName) } else { 'non disponibile' })) -ForegroundColor Yellow
    Write-Host 'Log eventi strutturati: %LOCALAPPDATA%\ProcessLens\logs\build.jsonl' -ForegroundColor Yellow
    Write-BuildDiagnostic -EventId 'PL-BUILD-5002' -Stage 'Launcher' -Result 'Failed' -Severity 'Error' -Project 'start-processlens-all.ps1' -ExitCode 1
    Write-Host 'Sequenza interrotta. Controlla l errore prima di riprovare; Guardian non e stato modificato.' -ForegroundColor Yellow
    exit 1
}
