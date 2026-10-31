$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$profiler = Join-Path $PSScriptRoot 'publish\win-x64\Profiler\ProcessLensProfiler.exe'
$tool = Get-Command 'dotnet-gcdump.exe' -ErrorAction SilentlyContinue
if ($null -eq $tool) {
    $toolPath = Join-Path $env:USERPROFILE '.dotnet\tools\dotnet-gcdump.exe'
    if (Test-Path -LiteralPath $toolPath) { $tool = Get-Item -LiteralPath $toolPath }
}
Write-Host '=== ProcessLens v0.19.12 - Runtime Profiler (sola verifica) ==='
Write-Host ('Profiler separato pubblicato: ' + (Test-Path -LiteralPath $profiler))
Write-Host ('Tool ufficiale dotnet-gcdump installato: ' + ($null -ne $tool))
Write-Host 'Adapter attivo: .NET GC heapstat. Java/Python/Chromium/native heap sono fasi successive.'
Write-Host 'Nessuna profilazione avviata. Nessuna GC indotta.'
if ($null -eq $tool) {
    Write-Host 'Opzionale, solo se vuoi profilare .NET: dotnet tool install --global dotnet-gcdump' -ForegroundColor Yellow
}
