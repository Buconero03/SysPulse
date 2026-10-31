$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
Write-Host 'ProcessLens CPU deterministic tests...' -ForegroundColor Cyan
dotnet run --configuration Release --project .\tests\ProcessLens.CpuTests\ProcessLens.CpuTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'CPU tests failed.' }
