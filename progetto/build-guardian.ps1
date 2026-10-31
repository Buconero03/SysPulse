$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Installare .NET 8 SDK per compilare Guardian.' }
Write-Host 'Controllo ordine ETW...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'tests\check-etw-startup-order.ps1')
Write-Host 'Esecuzione test Guardian.Core...' -ForegroundColor Cyan
dotnet run --project '.\tests\ProcessLens.GuardianTests\ProcessLens.GuardianTests.csproj' -c Release
if ($LASTEXITCODE -ne 0) { throw 'Test Guardian falliti. Pubblicazione annullata.' }
Write-Host 'Compilazione Guardian.Service (verifica ETW, Windows Service e storage)...' -ForegroundColor Cyan
dotnet build '.\src\ProcessLens.Guardian.Service\ProcessLens.Guardian.Service.csproj' -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Compilazione Guardian.Service fallita. Nessuna pubblicazione o installazione eseguita.' }
Write-Host 'Test database SQLite e report...' -ForegroundColor Cyan
dotnet run --project '.\tests\ProcessLens.GuardianStorageTests\ProcessLens.GuardianStorageTests.csproj' -c Release
if ($LASTEXITCODE -ne 0) { throw 'Test storage Guardian falliti. Pubblicazione annullata.' }
$out = Join-Path $PSScriptRoot 'dist\Guardian-win-x64'
Write-Host "Pubblicazione servizio Windows standalone: $out" -ForegroundColor Cyan
dotnet publish '.\src\ProcessLens.Guardian.Service\ProcessLens.Guardian.Service.csproj' `
    -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None `
    -o $out
if ($LASTEXITCODE -ne 0) { throw 'Pubblicazione Guardian fallita.' }
if (-not (Test-Path (Join-Path $out 'ProcessLens.Guardian.Service.exe'))) { throw 'EXE Guardian non trovato.' }
Write-Host 'Guardian pubblicato. Nessun servizio installato automaticamente.' -ForegroundColor Green
Write-Host 'Installazione esplicita: .\install-guardian.ps1' -ForegroundColor Yellow
