$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'offline-common.ps1')
$installRoot = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'processlens-owned-r20.txt')) { $PSScriptRoot } else { Get-ProcessLensInstallRoot }
$exe = Join-Path $installRoot 'app\win-x64\ProcessLens.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'ProcessLens non installato. Esegui INSTALLA_PROCESSLENS.cmd.' }
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $script = Join-Path $installRoot 'start-installed.ps1'
    Start-Process -FilePath $ps -Verb RunAs -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $script + '"')
    exit
}
Set-ProcessLensDotnetEnvironment $installRoot
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
