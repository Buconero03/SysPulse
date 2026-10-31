[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'SDK .NET 8 richiesto per compilare il monitor. Nessuna installazione automatica.' }
$out = Join-Path $PSScriptRoot 'publish\RamMonitor-win-x64'
$running = @(Get-Process -Name 'ProcessLens.RamMonitor' -ErrorAction SilentlyContinue)
foreach ($process in $running) {
    try { if ($process.Path -eq (Join-Path $out 'ProcessLens.RamMonitor.exe')) { throw 'Chiudi il monitor con Ctrl+C prima di ricompilarlo.' } }
    finally { $process.Dispose() }
}
. (Join-Path $PSScriptRoot 'build-diagnostics.ps1')
$project = Join-Path $PSScriptRoot 'src\ProcessLens.RamMonitor\ProcessLens.RamMonitor.csproj'
Invoke-CheckedDotnet -Stage 'Publish.ProcessLens.RamMonitor' -Project $project -Arguments @('publish',$project,'-c','Release','-r','win-x64','--self-contained','false','-o',$out,'--nologo')
if (-not (Test-Path -LiteralPath (Join-Path $out 'ProcessLens.RamMonitor.exe'))) { throw 'EXE monitor mancante dopo publish.' }
Write-Host ('Monitor pronto: ' + $out)
