param([switch] $FunctionsOnly, [string] $Executable, [string] $RuntimeRoot, [string] $Language)
$ErrorActionPreference = 'Stop'
function Test-DesktopRuntime8([string] $Root) {
    if (-not $Root -or -not (Test-Path -LiteralPath (Join-Path $Root 'dotnet.exe') -PathType Leaf)) { return $false }
    foreach ($name in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        $folder = Join-Path $Root ('shared/' + $name)
        if (-not (Test-Path -LiteralPath $folder -PathType Container)) { return $false }
        if (@(Get-ChildItem -LiteralPath $folder -Directory | Where-Object { $_.Name -match '^8\.0\.\d+$' }).Count -eq 0) { return $false }
    }
    return $true
}
function Resolve-ProcessLensLaunch([string] $Root, [string] $UserLocalAppData) {
    $installed = if ($UserLocalAppData) { Join-Path $UserLocalAppData 'Programs/ProcessLens/R20' } else { $null }
    $runtime = $null
    foreach ($candidate in @((Join-Path $Root 'runtime'), $(if ($installed) { Join-Path $installed 'dotnet' }))) {
        if (Test-DesktopRuntime8 $candidate) { $runtime = $candidate; break }
    }
    $exe = Join-Path $Root 'progetto/publish/win-x64/ProcessLens.exe'
    $receipt = Join-Path $Root 'supporto/build-r35r2.ok'
    if ((Test-Path -LiteralPath $exe -PathType Leaf) -and (Test-Path -LiteralPath $receipt -PathType Leaf) -and
        (Get-Content -LiteralPath $receipt -Raw).Trim() -eq 'ProcessLens-R35-R2-wpf-inheritance') {
        return [PSCustomObject]@{ Kind='Exe'; Entry=$exe; Runtime=$runtime }
    }
    return $null
}
function Invoke-ProcessLensBuild([string] $Root, [string] $UserLocalAppData) {
    $project = Join-Path $Root 'progetto'
    $installed = if ($UserLocalAppData) { Join-Path $UserLocalAppData 'Programs/ProcessLens/R20' } else { $null }
    $privateSdk = if ($installed) { Join-Path $installed 'dotnet' } else { $null }
    $sdkFolder = if ($privateSdk) { Join-Path $privateSdk 'sdk' } else { $null }
    $usePrivate = $sdkFolder -and (Test-Path -LiteralPath $sdkFolder -PathType Container) -and
        @(Get-ChildItem -LiteralPath $sdkFolder -Directory | Where-Object { $_.Name -match '^8\.0\.\d+$' }).Count -gt 0 -and
        (Test-DesktopRuntime8 $privateSdk)
    if ($usePrivate) {
        $env:DOTNET_ROOT = $privateSdk; $env:DOTNET_ROOT_X64 = $privateSdk
        $env:PATH = $privateSdk + [IO.Path]::PathSeparator + $env:PATH
        $cache = Join-Path $installed 'packages'
        if (Test-Path -LiteralPath $cache -PathType Container) {
            $env:NUGET_PACKAGES = $cache
            # Project-local configuration only. Use the completed R20 package cache without network feeds.
            $config = Join-Path $project 'NuGet.Config'
            if (-not (Test-Path -LiteralPath $config)) {
                Set-Content -LiteralPath $config -Encoding UTF8 -Value '<configuration><packageSources><clear/></packageSources></configuration>'
            }
        }
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'SDK .NET non trovato. Usa lo SDK 8 gia installato oppure quello della precedente installazione R20. Nessun download automatico.'
    }
    Write-Host 'Primo avvio R35 R2: test e compilazione della nuova versione. I successivi avvii saranno diretti.' -ForegroundColor Cyan
    Write-Host 'Non installo SDK, driver o servizi. Non avvio la vecchia applicazione R20.'
    Push-Location -LiteralPath $project
    try { & (Join-Path $project 'build-dashboard.ps1') }
    finally { Pop-Location }
    $exe = Join-Path $project 'publish/win-x64/ProcessLens.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Compilazione senza eseguibile R35. Avvio interrotto.' }
    Set-Content -LiteralPath (Join-Path $Root 'supporto/build-r35r2.ok') -Value 'ProcessLens-R35-R2-wpf-inheritance' -Encoding ASCII
}
function Quote-LaunchArgument([string] $Value) {
    if ($Value.Contains('"') -or $Value -match "[`r`n]") { throw 'Percorso non valido.' }
    return '"' + $Value + '"'
}
if ($FunctionsOnly) { return }
try {
    if ($env:OS -ne 'Windows_NT') { throw 'Avvio disponibile su Windows.' }
    $root = Split-Path -Parent $PSScriptRoot
    if (-not $Executable) {
        $target = Resolve-ProcessLensLaunch $root $env:LOCALAPPDATA
        if (-not $target) { Invoke-ProcessLensBuild $root $env:LOCALAPPDATA; $target = Resolve-ProcessLensLaunch $root $env:LOCALAPPDATA }
        if (-not $target) { throw 'Pubblicazione R23 non disponibile dopo la compilazione.' }
        $Executable = $target.Entry; $RuntimeRoot = $target.Runtime
    }
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'Eseguibile ProcessLens non trovato.' }
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin) {
        $ps = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
        $argsText = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File ' + (Quote-LaunchArgument $PSCommandPath) +
            ' -Executable ' + (Quote-LaunchArgument $Executable)
        if ($RuntimeRoot) { $argsText += ' -RuntimeRoot ' + (Quote-LaunchArgument $RuntimeRoot) }
        if ($Language) { $argsText += ' -Language ' + (Quote-LaunchArgument $Language) }
        Start-Process -FilePath $ps -Verb RunAs -ArgumentList $argsText
        exit
    }
    if ($RuntimeRoot) {
        if (-not (Test-DesktopRuntime8 $RuntimeRoot)) { throw 'Runtime .NET 8 privato incompleto.' }
        $env:DOTNET_ROOT = $RuntimeRoot; $env:DOTNET_ROOT_X64 = $RuntimeRoot
    }
    $launchArgs = if ($Language) { @('--lang', $Language) } else { @() }
    Start-Process -FilePath $Executable -ArgumentList $launchArgs -WorkingDirectory (Split-Path -Parent $Executable)
} catch {
    Write-Host ('[ERRORE] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
