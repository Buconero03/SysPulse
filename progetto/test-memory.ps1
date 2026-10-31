$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet run --project .\tests\ProcessLens.MemoryTests\ProcessLens.MemoryTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Memory tests falliti.' }
