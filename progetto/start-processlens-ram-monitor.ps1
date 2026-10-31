[CmdletBinding()]
param(
    [string]$GuiExe,
    [switch]$StartGui,
    [ValidateRange(1,60)][int]$IntervalSeconds = 5,
    [ValidateRange(0,2147483647)][int]$DurationSeconds = 0
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Windows richiesto.' }
# Resolve an exact GUI executable. File selection supports an already built older candidate without rebuilding its GUI.
if (-not $GuiExe) {
    foreach ($candidate in @('publish\win-x64\ProcessLens.exe','src\ProcessLens.App\bin\Release\net8.0-windows\ProcessLens.exe')) {
        $path = Join-Path $PSScriptRoot $candidate
        if (Test-Path -LiteralPath $path) { $GuiExe = $path; break }
    }
}
if (-not $GuiExe) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    try {
        $dialog.Title = 'Seleziona il ProcessLens.exe da misurare'
        $dialog.Filter = 'ProcessLens.exe|ProcessLens.exe'
        $dialog.CheckFileExists = $true
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host 'Avvio annullato.'; return }
        $GuiExe = $dialog.FileName
    }
    finally { $dialog.Dispose() }
}
$GuiExe = [IO.Path]::GetFullPath($GuiExe)
if (-not (Test-Path -LiteralPath $GuiExe) -or [IO.Path]::GetFileName($GuiExe) -ine 'ProcessLens.exe') { throw 'Selezionare un percorso esistente di ProcessLens.exe.' }
$monitor = Join-Path $PSScriptRoot 'publish\RamMonitor-win-x64\ProcessLens.RamMonitor.exe'
if (-not (Test-Path -LiteralPath $monitor)) { & (Join-Path $PSScriptRoot 'build-ram-monitor.ps1') }
# Paths are quoted as native Windows arguments; double trailing backslashes before a closing quote.
$root = $PSScriptRoot -replace '(\\+)$','$1$1'
$arguments = '--project-root "' + $root + '" --gui-exe "' + $GuiExe + '" --interval-seconds ' + $IntervalSeconds + ' --duration-seconds ' + $DurationSeconds
Start-Process -FilePath $monitor -ArgumentList $arguments -WorkingDirectory $PSScriptRoot | Out-Null
Write-Host 'Monitor avviato in una finestra separata. Log: verification-r10r8\ram-monitor.'
if ($StartGui) {
    # Explicit user launcher only: no service, agent or profiler is started.
    Start-Process -FilePath $GuiExe -WorkingDirectory ([IO.Path]::GetDirectoryName($GuiExe)) | Out-Null
}
