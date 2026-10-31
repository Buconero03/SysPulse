$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. (Join-Path $PSScriptRoot 'build-diagnostics.ps1')

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK non trovato.'
}

& (Join-Path $PSScriptRoot 'tests\check-elevation.ps1')
$out = Join-Path $PSScriptRoot 'publish\win-x64'
# Do not terminate a running GUI to overwrite its binaries. Preserve all existing output on this path.
$runningGui = @(Get-Process -Name 'ProcessLens' -ErrorAction SilentlyContinue)
if ($runningGui.Count -gt 0) {
    throw 'Chiudi ProcessLens prima di pubblicare la nuova build. Nessun processo e stato terminato.'
}
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out -Force | Out-Null

Invoke-CheckedDotnet -Stage 'Publish.ProcessLens.App' -Project '.\src\ProcessLens.App\ProcessLens.App.csproj' -Arguments @(
    'publish', '.\src\ProcessLens.App\ProcessLens.App.csproj', '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'false', '-o', $out, '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None',
    '-p:DebugSymbols=false', '-p:PublishReadyToRun=false')
# Profile sidecar only starts on user request; publish in separate folder to isolate dependencies.
$profOut = Join-Path $out 'Profiler'
Invoke-CheckedDotnet -Stage 'Publish.ProcessLens.Profiler' -Project '.\src\ProcessLens.Profiler\ProcessLens.Profiler.csproj' -Arguments @(
    'publish', '.\src\ProcessLens.Profiler\ProcessLens.Profiler.csproj', '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'false', '-o', $profOut, '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None',
    '-p:DebugSymbols=false', '-p:PublishReadyToRun=false')
if (-not (Test-Path (Join-Path $profOut 'ProcessLensProfiler.exe'))) {
    throw 'Pubblicazione profiler separato non riuscita.'
}
Write-Host "Profiler on-demand pubblicato separatamente: $profOut" -ForegroundColor Green
dotnet build-server shutdown | Out-Host

Write-Host ''
Write-Host "Build pubblicabile pronta in: $out" -ForegroundColor Green
Write-Host 'Avvia ProcessLens.exe direttamente. Richiede .NET 8 Desktop Runtime installato.' -ForegroundColor Cyan

Write-Host "Avvio amministratore: Windows mostrera la richiesta UAC all'apertura dell'EXE." -ForegroundColor Yellow
