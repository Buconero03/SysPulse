$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$published = Join-Path $PSScriptRoot 'publish\win-x64\ProcessLens.exe'
$built = Join-Path $PSScriptRoot 'src\ProcessLens.App\bin\Release\net8.0-windows\ProcessLens.exe'

if (Test-Path $published) {
    & (Join-Path $PSScriptRoot 'diagnose-startup.ps1') -ExePath $published
    exit 0
}

if (Test-Path $built) {
    & (Join-Path $PSScriptRoot 'diagnose-startup.ps1') -ExePath $built
    exit 0
}

throw 'Nessuna build Release trovata. Esegui prima .\bootstrap.ps1 o .\publish-release.ps1.'
