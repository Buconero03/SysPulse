$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resolver=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessIconResolver.cs') -Raw -Encoding UTF8
$drawing=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessSymbolDrawing.cs') -Raw -Encoding UTF8
$groups=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessGroupRow.cs') -Raw -Encoding UTF8
foreach($token in @('NativeIconPreference.ResolveExecutable','ProcessSymbolCatalog.GroupOverride','ProcessSymbolDrawing.ForName')){if(-not $resolver.Contains($token)){throw ('FAIL R33 resolver: '+$token)}}
if($drawing.Contains('FormattedText') -or $resolver.Contains('FormattedText')){throw 'FAIL R33: initials must not be rendered.'}
foreach($token in @('new(128, StringComparer.OrdinalIgnoreCase)','image.Freeze()','ProcessIconPlaceholder.Pattern')){if(-not $drawing.Contains($token)){throw ('FAIL R33 vector/cache: '+$token)}}
if(-not $groups.Contains('ProcessIconResolver.GetGroupIcon(_name, Members.FirstOrDefault())')){throw 'FAIL R33: group image must follow its actual displayed title.'}
[xml]$project=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessLens.App.csproj') -Raw -Encoding UTF8
$symbol=@($project.Project.ItemGroup.EmbeddedResource|Where-Object {$_.LogicalName -eq 'ProcessLens.ProcessSymbols'})
if($symbol.Count -ne 1){throw 'FAIL R33: symbols must be a raw embedded resource, with no XAML URI.'}
$data=Get-Content (Join-Path $root 'src/ProcessLens.App/Assets/ProcessSymbols.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($data.schema -ne 1 -or $data.entries.Count -lt 60){throw 'FAIL R33: process catalogue missing.'}
Write-Host 'PASS R33 source: original icons first, meaningful exact-name symbols, no initials, title-aware group image, bounded frozen vectors and embedded catalogue. Windows rendering remains separate.'
