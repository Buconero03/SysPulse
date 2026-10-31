$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet run --configuration Release --project .\tests\ProcessLens.ServiceHistoryTests\ProcessLens.ServiceHistoryTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Service history tests failed.' }
