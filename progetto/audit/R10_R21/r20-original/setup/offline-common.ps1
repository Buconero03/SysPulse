$ErrorActionPreference = 'Stop'
function Get-ProcessLensInstallRoot {
    if (-not $env:LOCALAPPDATA) { throw 'LOCALAPPDATA non disponibile.' }
    return Join-Path $env:LOCALAPPDATA 'Programs\ProcessLens\R20'
}
function Test-ProcessLensBundle([string] $Root) {
    $manifestPath = Join-Path $Root 'setup\bundle-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or $manifest.bundle -ne 'ProcessLens-R20-win-x64-offline') { throw 'Manifest del pacchetto non valido.' }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($entry in $manifest.files) {
        $relative = [string]$entry.file
        if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Percorso non valido nel manifest.' }
        $full = [IO.Path]::GetFullPath((Join-Path $Root $relative))
        if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'File esterno al pacchetto.' }
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "File mancante: $relative" }
        if ((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ne $entry.sha256) { throw "File modificato o incompleto: $relative" }
    }
    return $manifest
}
function Set-ProcessLensDotnetEnvironment([string] $InstallRoot) {
    $env:DOTNET_ROOT = Join-Path $InstallRoot 'dotnet'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:NUGET_CERT_REVOCATION_MODE = 'offline'
    $env:DOTNET_CLI_HOME = Join-Path $InstallRoot 'cli-home'
    $env:NUGET_PACKAGES = Join-Path $InstallRoot 'packages'
    $env:NuGetAudit = 'false' # Offline install only; no online vulnerability audit is claimed.
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
    $env:UseSharedCompilation = 'false'
}
