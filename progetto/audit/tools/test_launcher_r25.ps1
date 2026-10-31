param([string] $Launcher)
$ErrorActionPreference = 'Stop'
. $Launcher -FunctionsOnly
$count = 0
function Check([bool] $Ok, [string] $Label) {
    if (-not $Ok) { throw ('FAIL R25 launcher: ' + $Label) }
    Write-Host ('PASS R25 launcher: ' + $Label); $script:count++
}
function Touch([string] $Path) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    Set-Content -LiteralPath $Path -Value 'fixture only' -Encoding ASCII
}
function Make-Runtime([string] $Path, [string] $Version='8.0.32') {
    Touch (Join-Path $Path 'dotnet.exe')
    New-Item -ItemType Directory -Path (Join-Path $Path 'shared/Microsoft.NETCore.App/8.0.32') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $Path ('shared/Microsoft.WindowsDesktop.App/' + $Version)) -Force | Out-Null
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('processlens-r23-' + [Guid]::NewGuid().ToString('N'))
$saved = @{}
foreach ($name in @('PATH','DOTNET_ROOT','DOTNET_ROOT_X64','NUGET_PACKAGES')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $root = Join-Path $temporary 'bundle with space'; $profile = Join-Path $temporary 'profile with space'
    New-Item -ItemType Directory -Path $root,$profile,(Join-Path $root 'supporto') -Force | Out-Null
    Check ($null -eq (Resolve-ProcessLensLaunch $root $profile)) 'empty package has no runnable GUI'
    $installed = Join-Path $profile 'Programs/ProcessLens/R20'
    Touch (Join-Path $installed 'app/win-x64/ProcessLens.exe'); Touch (Join-Path $installed 'start-installed.ps1')
    Check ($null -eq (Resolve-ProcessLensLaunch $root $profile)) 'completed old installation cannot silently substitute its old GUI'
    $exe = Join-Path $root 'progetto/publish/win-x64/ProcessLens.exe'; Touch $exe
    $receipt = Join-Path $root 'supporto/build-r25.ok'
    Check ($null -eq (Resolve-ProcessLensLaunch $root $profile)) 'publication without successful R25 receipt is not accepted'
    Set-Content -LiteralPath $receipt -Value 'older version'
    Check ($null -eq (Resolve-ProcessLensLaunch $root $profile)) 'receipt for another revision is rejected'
    Remove-Item -LiteralPath $receipt
    $sdk = Join-Path $installed 'dotnet'; Make-Runtime $sdk
    New-Item -ItemType Directory -Path (Join-Path $sdk 'sdk/8.0.426'),(Join-Path $installed 'packages') -Force | Out-Null
    # Stub only the compiler entry point and project build script; no dotnet, app or service is launched.
    function global:dotnet { throw 'fixture must not launch a real compiler' }
    $build = Join-Path $root 'progetto/build-dashboard.ps1'
    Set-Content -LiteralPath $build -Value 'throw "synthetic failed tests"'
    $failed = $false
    try { Invoke-ProcessLensBuild $root $profile } catch { $failed = $_.Exception.Message.Contains('synthetic failed tests') }
    Check ($failed -and -not (Test-Path -LiteralPath $receipt)) 'failed tests cannot issue a build receipt or launch a stale publication'
    Check ($env:NUGET_PACKAGES -eq (Join-Path $installed 'packages')) 'completed R20 cache reused without copying packages into ZIP'
    [xml]$config = Get-Content -LiteralPath (Join-Path $root 'progetto/NuGet.Config') -Raw
    Check ($config.configuration.packageSources.clear -ne $null -and -not $config.configuration.packageSources.add) 'R20 cache build has no configured network feed'
    Remove-Item -LiteralPath $exe
    Set-Content -LiteralPath $build -Value '# fixture successful script but no executable'
    $failed = $false
    try { Invoke-ProcessLensBuild $root $profile } catch { $failed = $_.Exception.Message.Contains('senza eseguibile') }
    Check ($failed -and -not (Test-Path -LiteralPath $receipt)) 'missing executable after build remains an error'
    Set-Content -LiteralPath $build -Value 'New-Item -ItemType Directory -Path "publish/win-x64" -Force | Out-Null; Set-Content -LiteralPath "publish/win-x64/ProcessLens.exe" -Value "fixture"'
    Invoke-ProcessLensBuild $root $profile
    $target = Resolve-ProcessLensLaunch $root $profile
    Check ($target.Kind -eq 'Exe' -and $target.Entry -eq $exe -and $target.Runtime -eq $sdk) 'successful owned build resolves new publication with existing private runtime'
    Set-Content -LiteralPath $build -Value 'throw "fast path must not rebuild"'
    Check ((Resolve-ProcessLensLaunch $root $profile).Entry -eq $exe) 'subsequent resolution needs no build or SDK command'
    $incomplete = Join-Path $temporary 'incomplete'; Touch (Join-Path $incomplete 'dotnet.exe')
    Check (-not (Test-DesktopRuntime8 $incomplete)) 'missing desktop runtime is not accepted'
    Make-Runtime $incomplete '9.0.1'
    Check (-not (Test-DesktopRuntime8 $incomplete)) 'wrong desktop runtime major is not accepted'
    Check ((Quote-LaunchArgument 'C:\Program Files\ProcessLens\ProcessLens.exe') -eq '"C:\Program Files\ProcessLens\ProcessLens.exe"') 'paths with spaces remain one UAC argument'
    foreach ($bad in @('C:\bad"path', "C:\bad`npath")) {
        $failed = $false; try { Quote-LaunchArgument $bad | Out-Null } catch { $failed = $true }
        Check $failed 'quotes and newlines cannot create extra launch arguments'
    }
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($Launcher,[ref]$tokens,[ref]$errors)
    Check ($errors.Count -eq 0) 'real PowerShell parser accepts complete launcher'
    $commands = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.CommandAst] },$true) | ForEach-Object { $_.GetCommandName() })
    Check (@($commands | Where-Object { $_ -in @('Expand-Archive','Invoke-WebRequest','Install-Package','Install-Module','Set-Service','Start-Service','Stop-Service','Stop-Process') }).Count -eq 0) 'launcher installs no dependencies and changes no services or running processes'
    Write-Host ('R25 launcher fixture complete: ' + $count + ' checks; synthetic files only, no Windows app or UAC.')
} finally {
    Remove-Item function:global:dotnet -ErrorAction SilentlyContinue
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name]) }
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
