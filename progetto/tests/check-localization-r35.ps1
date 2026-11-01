$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $root 'localization-manifest.json') -Raw|ConvertFrom-Json
foreach($group in @($manifest.SourceFiles,$manifest.GeneratedFiles)) {
 foreach($entry in $group.PSObject.Properties) {
  $path=Join-Path $root $entry.Name
  if(-not(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value){throw ('R35 localization source/generated mismatch: '+$entry.Name+'. Run tools/refresh-localization.ps1.')}
 }
}
$folders=@('ProcessLens.Core','ProcessLens.Semantics','ProcessLens.Windows','ProcessLens.App','ProcessLens.RamMonitor','ProcessLens.MemoryAgent','ProcessLens.Profiler')
foreach($name in $folders){
 foreach($file in Get-ChildItem -LiteralPath (Join-Path $root ('src/'+$name)) -Filter '*.cs' -File){
  $relative='src/'+$name+'/'+$file.Name
  if(-not($manifest.SourceFiles.PSObject.Properties.Name -contains $relative)){throw ('R35 new canonical file not generated: '+$relative)}
 }
}
Write-Host ('PASS R35: '+@($manifest.GeneratedFiles.PSObject.Properties).Count+' generated files and canonical source hashes verified; dictionary coverage is checked by LocalizationTests.')
