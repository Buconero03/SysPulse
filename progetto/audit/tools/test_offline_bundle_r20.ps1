param([string] $ProjectRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $ProjectRoot 'setup/offline-common.ps1')
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('processlens-setup-fixture-' + [Guid]::NewGuid().ToString('N'))
$count = 0
function Check([bool] $Ok, [string] $Label) {
    if (-not $Ok) { throw ('FAIL bundle: ' + $Label) }
    Write-Host ('PASS bundle: ' + $Label)
    $script:count++
}
function Throws([scriptblock] $Action, [string] $Label) {
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $true }
    Check $caught $Label
}
try {
    New-Item -ItemType Directory -Path (Join-Path $temporary 'setup') -Force | Out-Null
    $file = Join-Path $temporary 'sample with space.txt'
    Set-Content -LiteralPath $file -Value 'original sample' -Encoding UTF8
    $row = @{ file='sample with space.txt'; sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
    $manifest = @{ schema=1; bundle='ProcessLens-R20-win-x64-offline'; files=@($row) }
    $path = Join-Path $temporary 'setup/bundle-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
    Check ((Test-ProcessLensBundle $temporary).schema -eq 1) 'valid package with spaces accepted'
    Add-Content -LiteralPath $file -Value 'tampered'
    Throws { Test-ProcessLensBundle $temporary } 'tampered payload rejected'
    Remove-Item -LiteralPath $file
    Throws { Test-ProcessLensBundle $temporary } 'missing payload rejected'
    foreach ($bad in @('../outside.txt', 'a/../../outside.txt', [IO.Path]::GetFullPath((Join-Path $temporary 'absolute.txt')))) {
        $manifest.files[0].file = $bad
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
        Throws { Test-ProcessLensBundle $temporary } ('unsafe path rejected: ' + $bad)
    }
    $manifest.schema = 99
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
    Throws { Test-ProcessLensBundle $temporary } 'unknown schema rejected'
    $setup = Get-Content -LiteralPath (Join-Path $ProjectRoot 'setup/install-offline.ps1') -Raw
    Check ($setup.IndexOf('Test-ProcessLensBundle') -lt $setup.IndexOf('Expand-Archive')) 'integrity checked before extracting SDK'
    Check ($setup.IndexOf('build-dashboard.ps1') -lt $setup.IndexOf('Copy-Item -Path')) 'full build required before publishing installation'
    Check ($setup.Contains('processlens-owned-r20.txt') -and $setup.Contains('Nessun file sovrascritto')) 'unowned destination protected'
    Check ($setup.Contains('SHA512') -and $setup.Contains('sdk_sha512')) 'official SDK hash checked again at install'
    Check ($setup.Contains('<clear/>') -and $setup.Contains('payload\nuget')) 'local NuGet feed isolated from user sources'
    $diagnostic = Get-Content -LiteralPath (Join-Path $ProjectRoot 'setup/diagnose-network.ps1') -Raw
    Check ($diagnostic.Contains(' /get ') -and -not $diagnostic.Contains('/set')) 'audit query remains read only'
    foreach ($forbidden in @('Set-Service', 'Start-Service', 'Stop-Service', 'WlanScan(', 'AuditSetSystemPolicy', 'FwpmFilterAdd')) {
        Check (-not $diagnostic.Contains($forbidden)) ('no automatic action: ' + $forbidden)
    }
    Write-Host ('Bundle fixture complete: ' + $count + ' checks. No Windows install or network access.')
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
