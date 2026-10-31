#Requires -RunAsAdministrator
param([string]$UtcDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd'))
$exe = Join-Path $env:ProgramFiles 'ProcessLens\Guardian\ProcessLens.Guardian.Service.exe'
if (-not (Test-Path $exe)) { throw 'Guardian non installato. Esegui prima build-guardian.ps1 e install-guardian.ps1.' }
& $exe --report $UtcDate
if ($LASTEXITCODE -ne 0) { throw 'Impossibile generare il report.' }
