param(
    [string]$ExePath,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $PSScriptRoot 'src\ProcessLens.App\bin\Release\net8.0-windows\ProcessLens.exe'
}

$log = Join-Path $env:LOCALAPPDATA 'ProcessLens\logs\startup.log'
Write-Host "Diagnostica ProcessLens - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "Log locale: $log"

if (-not $NoLaunch) {
    if (-not (Test-Path $ExePath)) {
        Write-Warning "Eseguibile mancante: $ExePath. Esegui prima .\bootstrap.ps1"
        exit 1
    }
    try {
        $process = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path $ExePath) -PassThru -ErrorAction Stop
    } catch {
        Write-Warning ('Avvio annullato o non riuscito: conferma UAC necessaria. ' + $_.Exception.Message)
        exit 1
    }
    Start-Sleep -Seconds 3
    $process.Refresh()
    if ($process.HasExited) {
        Write-Warning "Il processo e' uscito entro 3 secondi. PID $($process.Id); codice $($process.ExitCode)."
    } else {
        Write-Host "ProcessLens e' in esecuzione. PID $($process.Id)." -ForegroundColor Green
    }
}

if (Test-Path $log) {
    Write-Host "`nUltime 50 righe del registro ProcessLens:" -ForegroundColor Yellow
    Get-Content $log -Tail 50
} else {
    Write-Warning "Nessun registro ProcessLens presente. Un errore potrebbe verificarsi prima dell'avvio del codice gestito."
}

Write-Host "`nEventi Windows degli ultimi 20 minuti pertinenti a ProcessLens:" -ForegroundColor Cyan
try {
    $events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-20) } -MaxEvents 400 -ErrorAction Stop |
        Where-Object {
            $_.ProviderName -in @('.NET Runtime', 'Application Error', 'Windows Error Reporting') -and
            $_.Message -match '(?i)ProcessLens'
        } | Select-Object -First 4
    if ($events) {
        $events | Select-Object TimeCreated, Id, ProviderName, Message | Format-List
    } else {
        Write-Host 'Nessun evento di crash ProcessLens trovato nel registro Applicazione.'
    }
} catch {
    Write-Warning "Il registro Applicazione non e' leggibile: $($_.Exception.Message)"
}
