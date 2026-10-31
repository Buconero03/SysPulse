$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet run -c Release --project .\tests\ProcessLens.TerminationTests\ProcessLens.TerminationTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Test terminazione falliti.' }
