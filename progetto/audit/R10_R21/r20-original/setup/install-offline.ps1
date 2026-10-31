$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'offline-common.ps1')
if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitProcess -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') {
    throw 'Questo pacchetto richiede Windows x64 e PowerShell a 64 bit. ARM64 e x86 non supportati.'
}
$sourceRoot = Split-Path -Parent $PSScriptRoot
$installRoot = Get-ProcessLensInstallRoot
$logRoot = Join-Path $env:LOCALAPPDATA 'ProcessLens\logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$transcriptStarted = $false
try {
    Start-Transcript -LiteralPath (Join-Path $logRoot 'installazione-r20.txt') -Force | Out-Null
    $transcriptStarted = $true
    Write-Host '[1/5] Verifica integrita del pacchetto completo...'
    $manifest = Test-ProcessLensBundle $sourceRoot
    if (@(Get-Process -Name ProcessLens -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Chiudi ProcessLens prima di installare. Nessun processo e stato terminato.'
    }
    Set-Location -LiteralPath $sourceRoot
    $marker = Join-Path $installRoot 'processlens-owned-r20.txt'
    if ((Test-Path -LiteralPath $installRoot) -and
        (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $manifest.bundle)) {
        throw 'La cartella di destinazione esiste e non appartiene a questa installazione. Nessun file sovrascritto.'
    }
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Set-Content -LiteralPath $marker -Value $manifest.bundle -Encoding ASCII
    Write-Host '[2/5] Preparazione .NET SDK e Desktop Runtime privati...'
    $sdkZip = Join-Path $PSScriptRoot 'payload\dotnet-sdk-8.0.426-win-x64.zip'
    if ((Get-FileHash -LiteralPath $sdkZip -Algorithm SHA512).Hash -ne $manifest.sdk_sha512) { throw 'SHA512 Microsoft SDK non corrispondente.' }
    $dotnetRoot = Join-Path $installRoot 'dotnet'
    Expand-Archive -LiteralPath $sdkZip -DestinationPath $dotnetRoot -Force
    Set-ProcessLensDotnetEnvironment $installRoot
    $dotnet = Join-Path $dotnetRoot 'dotnet.exe'
    $version = & $dotnet --version
    if ($LASTEXITCODE -ne 0 -or "$version".Trim() -ne '8.0.426') { throw 'SDK privato non avviabile. Vedere il log.' }
    # Configuration local to the extracted project; no user/machine NuGet settings changed.
    $feed = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'payload\nuget'))
    $config = '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear/><add key="ProcessLensOffline" value="' + $feed + '"/></packageSources><disabledPackageSources><clear/></disabledPackageSources></configuration>'
    $configPath = Join-Path $sourceRoot 'NuGet.Config'
    if ((Test-Path -LiteralPath $configPath) -and (Get-Content -LiteralPath $configPath -Raw).Trim() -ne $config) {
        throw 'NuGet.Config esistente e diverso: estrai il pacchetto in una nuova cartella.'
    }
    Set-Content -LiteralPath $configPath -Value $config -Encoding UTF8
    Write-Host '[3/5] Test completi e compilazione WPF. Puo richiedere diversi minuti...'
    & (Join-Path $sourceRoot 'build-dashboard.ps1')
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'publish\win-x64\ProcessLens.exe'))) { throw 'Build senza eseguibile GUI.' }
    Write-Host '[4/5] Installazione applicazione e collegamenti...'
    $appRoot = Join-Path $installRoot 'app'
    New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $sourceRoot 'publish\*') -Destination $appRoot -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'start-installed.ps1') -Destination $installRoot -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'offline-common.ps1') -Destination $installRoot -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'diagnose-network.ps1') -Destination $installRoot -Force
    $shell = New-Object -ComObject WScript.Shell
    $menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'ProcessLens'
    New-Item -ItemType Directory -Path $menu -Force | Out-Null
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    foreach ($item in @(@{ Name='ProcessLens'; Script='start-installed.ps1' }, @{ Name='ProcessLens - Diagnostica rete'; Script='diagnose-network.ps1' })) {
        $link = $shell.CreateShortcut((Join-Path $menu ($item.Name + '.lnk')))
        $link.TargetPath = $powershell
        $scriptPath = Join-Path $installRoot $item.Script
        $link.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '"'
        $link.WorkingDirectory = $installRoot
        $link.Save()
    }
    Write-Host '[5/5] Installazione completata. Nessun servizio o criterio audit modificato.' -ForegroundColor Green
    Write-Host 'Apri ProcessLens dal menu Start. Windows mostrera la richiesta amministratore.'
    Write-Host 'Per i problemi rete usa DIAGNOSTICA_RETE.cmd o ProcessLens - Diagnostica rete nel menu Start.'
    Write-Host 'Wi-Fi: verifica anche Privacy e sicurezza > Posizione e il servizio WLAN AutoConfig.'
    Write-Host 'Le dipendenze sono locali. La verifica vulnerabilita online NuGet non e stata eseguita.'
} catch {
    Write-Host ('[INSTALLAZIONE INTERROTTA] ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('Log: ' + (Join-Path $logRoot 'installazione-r20.txt'))
    throw
} finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
}
