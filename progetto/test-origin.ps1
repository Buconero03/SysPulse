$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
Write-Host 'Test del motore Origine Processi' -ForegroundColor Cyan
dotnet run --configuration Release --project .\tests\ProcessLens.OriginTests\ProcessLens.OriginTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test Origine falliti.' }
