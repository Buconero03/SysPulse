# ProcessLens build diagnostics v1 - PowerShell 5.1 compatible, no dependencies.
# Diagnostic stream stores project-relative file name and compiler code, never native raw command lines.
if (-not $script:ProcessLensBuildId) { $script:ProcessLensBuildId = [guid]::NewGuid().ToString('N') }

function Write-BuildDiagnostic {
    param(
        [string]$EventId, [string]$Stage, [string]$Result, [string]$Severity,
        [string]$Project = '', [string]$CompilerCode = '', [string]$SourceFile = '',
        [int]$SourceLine = 0, [Nullable[int]]$ExitCode = $null
    )
    $entry = [ordered]@{
        schema_version = 1
        timestamp_utc = [DateTimeOffset]::UtcNow.ToString('o')
        event_id = $EventId
        component = 'ProcessLens.Build'
        operation_id = $script:ProcessLensBuildId
        operation = 'DotNet.BuildOrTest'
        stage = $Stage
        severity = $Severity
        status = $Result
        project = $Project
    }
    if ($null -ne $ExitCode) { $entry['native_exit_code'] = [int]$ExitCode }
    if ($CompilerCode) { $entry['compiler_code'] = $CompilerCode }
    if ($SourceFile) { $entry['source_file_name'] = [System.IO.Path]::GetFileName($SourceFile) }
    if ($SourceLine -gt 0) { $entry['source_line'] = $SourceLine }
    try {
        $directory = Join-Path $env:LOCALAPPDATA 'ProcessLens\logs'
        if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
        $path = Join-Path $directory 'build.jsonl'
        $line = $entry | ConvertTo-Json -Compress -Depth 4
        $encodedLength = [System.Text.Encoding]::UTF8.GetByteCount($line)
        if ($encodedLength -ge 4096) { return }
        if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Length + $encodedLength + 2 -gt 1MB)) {
            Move-Item -LiteralPath $path -Destination ($path + '.previous') -Force
        }
        [System.IO.File]::AppendAllText($path, $line + [Environment]::NewLine, ([System.Text.UTF8Encoding]::new($false)))
    } catch { # Diagnostic output never overrides the build result.
    }
}

function Invoke-CheckedDotnet {
    param([string]$Stage, [string]$Project, [string[]]$Arguments)
    Write-BuildDiagnostic -EventId 'PL-BUILD-5001' -Stage $Stage -Result 'Started' -Severity 'Information' -Project $Project
    $firstCode = ''; $firstFile = ''; $firstLine = 0
    $previousPreference = $ErrorActionPreference
    try {
        # PS5.1 turns native STDERR into ErrorRecords on 2>&1; preserve their diagnostic text.
        $ErrorActionPreference = 'Continue'
        & dotnet @Arguments 2>&1 | ForEach-Object {
            $text = [string]$_
            Write-Host $text
            if (-not $firstCode) {
                $match = [regex]::Match($text,
                    '(?i)(?<file>[^\r\n]*?)\((?<line>\d+),(?<col>\d+)\):\s*error\s+(?<code>[A-Z]{2,5}\d+):')
                if ($match.Success) {
                    $firstFile = $match.Groups['file'].Value.Trim()
                    $firstLine = [int]$match.Groups['line'].Value
                    $firstCode = $match.Groups['code'].Value.ToUpperInvariant()
                } else {
                    $match = [regex]::Match($text, '(?i)\berror\s+(?<code>(?:CS|MC|MSB|NU|NETSDK)\d+):')
                    if ($match.Success) { $firstCode = $match.Groups['code'].Value.ToUpperInvariant() }
                }
            }
        }
        $nativeExitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if ($null -eq $nativeExitCode) { $nativeExitCode = 1 }
    if ($nativeExitCode -ne 0) {
        Write-BuildDiagnostic -EventId 'PL-BUILD-5002' -Stage $Stage -Result 'Failed' -Severity 'Error' `
            -Project $Project -CompilerCode $firstCode -SourceFile $firstFile -SourceLine $firstLine -ExitCode $nativeExitCode
        Write-Host ''
        Write-Host ('[ERRORE COMPILAZIONE] PL-BUILD-5002 | Fase: ' + $Stage) -ForegroundColor Red
        Write-Host ('Progetto: ' + $Project + ' | Exit code: ' + $nativeExitCode) -ForegroundColor Red
        if ($firstCode) { Write-Host ('Codice: ' + $firstCode + ' | File: ' + [IO.Path]::GetFileName($firstFile) + ' | Riga: ' + $firstLine) -ForegroundColor Red }
        Write-Host 'Fasi successive: NON ESEGUITE. Vedere il messaggio compiler completo sopra e build.jsonl.' -ForegroundColor Yellow
        throw ('PL-BUILD-5002: fase ' + $Stage + ' non riuscita, codice compilatore ' + $(if($firstCode){$firstCode}else{'non disponibile'}) + ', exit code ' + $nativeExitCode)
    }
    Write-BuildDiagnostic -EventId 'PL-BUILD-5003' -Stage $Stage -Result 'Completed' -Severity 'Information' -Project $Project
}
