$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$generator=Join-Path $PSScriptRoot 'LocalizationGenerator/LocalizationGenerator.csproj'
$source=Join-Path $root 'src'
$inventory=Join-Path $root 'audit/R10_R35/message-inventory.json'
& dotnet run --project $generator -- $source $inventory
if($LASTEXITCODE -ne 0){throw 'Localization generation failed.'}
& (Join-Path $root 'tests/check-localization-r35.ps1')
