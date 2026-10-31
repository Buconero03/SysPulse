# Verification only: no GUI launch, service installation, capture or process termination.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'NON ESEGUITO: questa procedura richiede Windows.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'NON ESEGUITO: SDK .NET non trovato. Nessuna installazione automatica.'
}
$root = $PSScriptRoot
$results = Join-Path $root 'verification-r7'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$records = New-Object System.Collections.Generic.List[object]
$failures = 0
Push-Location $root
try {
    & dotnet --info | Out-File (Join-Path $results 'dotnet-info.txt') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'dotnet --info non riuscito.' }
    foreach ($script in Get-ChildItem (Join-Path $root 'tests') -Filter 'check-*.ps1' | Sort-Object Name) {
        $status = 'PASS'
        try { & $script.FullName *> (Join-Path $results ($script.BaseName + '.log')) }
        catch { $status = 'FAIL'; $failures++; $_ | Out-File (Join-Path $results ($script.BaseName + '.log')) -Append -Encoding utf8 }
        $records.Add([PSCustomObject]@{ Kind='PowerShell'; Name=$script.Name; Status=$status; ExitCode=$null })
    }
    foreach ($project in Get-ChildItem (Join-Path $root 'tests') -Filter '*.csproj' -Recurse | Sort-Object FullName) {
        # Fixture is compiled only. Running it would create a target process for profiling.
        $verb = if ($project.BaseName -eq 'ProcessLens.ProfilerFixture') { 'build' } else { 'run' }
        $arguments = if ($verb -eq 'run') { @('run','--project',$project.FullName,'-c','Release') }
            else { @('build',$project.FullName,'-c','Release','--nologo') }
        & dotnet @arguments *> (Join-Path $results ($project.BaseName + '.log'))
        $exitCode = $LASTEXITCODE
        $status = if ($exitCode -eq 0) { 'PASS' } else { $failures++; 'FAIL' }
        $records.Add([PSCustomObject]@{ Kind='dotnet'; Name=$project.BaseName; Status=$status; ExitCode=$exitCode })
    }
    foreach ($project in Get-ChildItem (Join-Path $root 'src') -Filter '*.csproj' -Recurse | Sort-Object FullName) {
        & dotnet build $project.FullName -c Release --nologo *> (Join-Path $results ('build-' + $project.BaseName + '.log'))
        $exitCode = $LASTEXITCODE
        $status = if ($exitCode -eq 0) { 'PASS' } else { $failures++; 'FAIL' }
        $records.Add([PSCustomObject]@{ Kind='build'; Name=$project.BaseName; Status=$status; ExitCode=$exitCode })
    }
    # Run the same semantic regression suite with copied knowledge omitted, to check C# fallbacks.
    $semanticTests = Join-Path $root 'tests\ProcessLens.TerminologyTests\bin\Release\net8.0'
    $knowledge = Join-Path $semanticTests 'knowledge'
    $disabled = Join-Path $semanticTests 'knowledge.disabled-r7'
    if (Test-Path $disabled) { throw 'Backup knowledge gia presente; risolvere prima di rieseguire.' }
    if (Test-Path $knowledge) {
        Move-Item -LiteralPath $knowledge -Destination $disabled
        try {
            & dotnet (Join-Path $semanticTests 'ProcessLens.TerminologyTests.dll') *> (Join-Path $results 'terminology-without-knowledge.log')
            $exitCode = $LASTEXITCODE
            $status = if ($exitCode -eq 0) { 'PASS' } else { $failures++; 'FAIL' }
            $records.Add([PSCustomObject]@{ Kind='fallback'; Name='TerminologyWithoutKnowledge'; Status=$status; ExitCode=$exitCode })
        }
        finally { Move-Item -LiteralPath $disabled -Destination $knowledge }
    }
    else {
        $records.Add([PSCustomObject]@{ Kind='fallback'; Name='TerminologyWithoutKnowledge'; Status='NON ESEGUITO'; ExitCode=$null })
        $failures++
    }
}
finally {
    $records | Export-Csv (Join-Path $results 'test-results.csv') -NoTypeInformation -Encoding UTF8
    Pop-Location
}
if ($failures -gt 0) { throw ($failures.ToString() + ' verifiche fallite o non eseguite. Consultare verification-r7. Candidata non promossa.') }
Write-Host 'Suite e build superate. Verifica visuale WPF e scenari Windows ancora da completare; candidata non promossa.'
