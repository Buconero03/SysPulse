$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project=Join-Path $root 'tests/ProcessLens.ProcessGroupsTests/ProcessLens.ProcessGroupsTests.csproj'
[xml]$xml=Get-Content $project -Raw -Encoding UTF8
$refs=@($xml.SelectNodes('//ProjectReference') | ForEach-Object { $_.Include.Replace('\','/') })
if('../../src/ProcessLens.Semantics/ProcessLens.Semantics.csproj' -notin $refs){throw 'FAIL R31 R1: ProcessGroupsTests must declare its Semantics ProjectReference; extra compiler references cannot substitute the MSBuild graph.'}
$policy=Get-Content (Join-Path $root 'src/ProcessLens.App/WindowsProcessIconPolicy.cs') -Raw -Encoding UTF8
if(-not $policy.Contains('path is null || !SystemProcessPresentation.UnderWindows(path, directory)')){throw 'FAIL R31 R1: icon path must be proven non-null before dereferencing.'}
Write-Host 'PASS R31 R1 source: explicit Semantics project reference and nullable icon path guard. Actual MSBuild compilation is verified separately.'
