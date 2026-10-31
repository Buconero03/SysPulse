param([string] $Launcher)
$ErrorActionPreference = 'Stop'
. $Launcher -FunctionsOnly
$count = 0
function Check([bool] $Ok, [string] $Label) {
    if (-not $Ok) { throw ('FAIL light launcher: ' + $Label) }
    Write-Host ('PASS light launcher: ' + $Label); $script:count++
}
function Touch([string] $Path) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    Set-Content -LiteralPath $Path -Value 'fixture only' -Encoding ASCII
}
function Make-Runtime([string] $Path, [string] $DesktopVersion='8.0.32') {
    Touch (Join-Path $Path 'dotnet.exe')
    New-Item -ItemType Directory -Path (Join-Path $Path 'shared/Microsoft.NETCore.App/8.0.32') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $Path ('shared/Microsoft.WindowsDesktop.App/' + $DesktopVersion)) -Force | Out-Null
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('processlens-light-' + [Guid]::NewGuid().ToString('N'))
try {
    $root = Join-Path $temporary 'bundle with space'
    $profile = Join-Path $temporary 'profile with space'
    New-Item -ItemType Directory -Path $root,$profile -Force | Out-Null
    Check ($null -eq (Resolve-ProcessLensLaunch $root $profile)) 'missing application returns unavailable without installing or compiling'
    $installed = Join-Path $profile 'Programs/ProcessLens/R20'
    Touch (Join-Path $installed 'start-installed.ps1')
    Check ($null -eq (Resolve-ProcessLensLaunch $root $profile)) 'launcher alone cannot be mistaken for installed application'
    Touch (Join-Path $installed 'app/win-x64/ProcessLens.exe')
    $target = Resolve-ProcessLensLaunch $root $profile
    Check ($target.Kind -eq 'Installed' -and $target.Entry -eq (Join-Path $installed 'start-installed.ps1')) 'completed R20 installation reuses its own existing starter'
    Make-Runtime (Join-Path $installed 'dotnet')
    Touch (Join-Path $root 'progetto/publish/win-x64/ProcessLens.exe')
    $target = Resolve-ProcessLensLaunch $root $profile
    Check ($target.Kind -eq 'Exe' -and $target.Entry -eq (Join-Path $root 'progetto/publish/win-x64/ProcessLens.exe')) 'local publication overrides older installed copy'
    Check ($target.Runtime -eq (Join-Path $installed 'dotnet')) 'local publication can reuse existing private runtime without SDK setup'
    Touch (Join-Path $root 'app/ProcessLens.exe')
    $target = Resolve-ProcessLensLaunch $root $profile
    Check ($target.Entry -eq (Join-Path $root 'app/ProcessLens.exe')) 'optional portable app publication takes precedence'
    Make-Runtime (Join-Path $root 'runtime')
    Check ((Resolve-ProcessLensLaunch $root $profile).Runtime -eq (Join-Path $root 'runtime')) 'optional complete local runtime takes precedence'
    $incomplete = Join-Path $temporary 'incomplete-runtime'
    Touch (Join-Path $incomplete 'dotnet.exe')
    New-Item -ItemType Directory -Path (Join-Path $incomplete 'shared/Microsoft.NETCore.App/8.0.32') -Force | Out-Null
    Check (-not (Test-DesktopRuntime8 $incomplete)) 'core runtime without Desktop cannot be accepted as WPF runtime'
    Make-Runtime $incomplete '9.0.1'
    Check (-not (Test-DesktopRuntime8 $incomplete)) 'Desktop major version nine cannot satisfy private runtime eight'
    Check (-not (Test-DesktopRuntime8 '')) 'missing runtime path is not treated as available'
    Check ((Quote-LaunchArgument 'C:\Program Files\ProcessLens\ProcessLens.exe') -eq '"C:\Program Files\ProcessLens\ProcessLens.exe"') 'paths with spaces remain one launch argument'
    foreach ($bad in @('C:\bad"path', "C:\bad`npath")) {
        $caught = $false
        try { Quote-LaunchArgument $bad | Out-Null } catch { $caught = $true }
        Check $caught 'quote or line break cannot create a second launch argument'
    }
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($Launcher, [ref]$tokens, [ref]$errors)
    Check ($errors.Count -eq 0) 'actual PowerShell parser accepts launcher syntax'
    $commands = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.CommandAst] }, $true) | ForEach-Object { $_.GetCommandName() })
    Check (@($commands | Where-Object { $_ -in @('dotnet','Expand-Archive','Invoke-WebRequest','Install-Package','Install-Module','Set-Service','Start-Service','Stop-Service') }).Count -eq 0) 'launch has no compiler package download extraction or service action'
    Check (-not ((Get-Content -LiteralPath $Launcher -Raw) -match 'Start-Sleep|Read-Host|Get-Process')) 'launch adds no fixed wait prompt or process scan'
    Write-Host ('Light launcher fixture complete: ' + $count + ' checks; no Windows app, UAC or clipboard used.')
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
