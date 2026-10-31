$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $root 'src\ProcessLens.App\app.manifest'
$project = Join-Path $root 'src\ProcessLens.App\ProcessLens.App.csproj'
if (-not (Test-Path $manifest)) { throw 'app.manifest mancante.' }
[xml]$xml = Get-Content $manifest -Raw
$manager = [System.Xml.XmlNamespaceManager]::new($xml.NameTable)
$manager.AddNamespace('m', 'urn:schemas-microsoft-com:asm.v3')
$elevation = $xml.SelectSingleNode('//m:requestedExecutionLevel', $manager)
if ($null -eq $elevation -or $elevation.GetAttribute('level') -ne 'requireAdministrator') {
    throw 'Manifest non valido: richiesto requireAdministrator.'
}
[xml]$csproj = Get-Content $project -Raw
$setting = @($csproj.Project.PropertyGroup.ApplicationManifest | Where-Object { $_ })
if ($setting.Count -ne 1 -or $setting[0] -ne 'app.manifest') {
    throw 'ProcessLens.App.csproj non punta a app.manifest.'
}
Write-Host 'PASS: manifest requireAdministrator collegato al progetto WPF.' -ForegroundColor Green
