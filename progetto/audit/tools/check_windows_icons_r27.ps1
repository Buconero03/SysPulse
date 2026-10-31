$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$policy=Get-Content (Join-Path $root 'src/ProcessLens.App/WindowsProcessIconPolicy.cs') -Raw -Encoding UTF8
$resolver=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessIconResolver.cs') -Raw -Encoding UTF8
$row=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessRow.cs') -Raw -Encoding UTF8
$group=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessGroupRow.cs') -Raw -Encoding UTF8
$main=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml.cs') -Raw -Encoding UTF8
foreach($token in @('WindowsComponentCatalog.Match','SystemProcessPresentation.UnderWindows','Microsoft Corporation','Microsoft Windows Operating System','Firma digitale non verificata')) {
 if(-not $policy.Contains($token)){throw ('FAIL R27 icon policy: '+$token)}
}
foreach($token in @('ProcessSymbolCatalog.Find','ProcessSymbolDrawing.ForName','GetResolution(process.ExecutablePath, process.Name)')) {
 if(-not $resolver.Contains($token)){throw ('FAIL R27 resolver: '+$token)}
}
if(-not $row.Contains('ProcessIconResolver.GetIcon(Process)') -or -not $group.Contains('ProcessIconResolver.GetGroupIcon(_name, Members.FirstOrDefault())')){throw 'R27 rows/groups must use the same icon path.'}
if(-not $main.Contains('DetailProcessIcon.ToolTip = row.IconDescription')){throw 'R27 details must explain the icon.'}
[xml]$view=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml') -Raw -Encoding UTF8
if(-not $view.OuterXml.Contains('Name.IconDescription') -or -not $view.OuterXml.Contains('{Binding IconDescription}')){throw 'R27 missing icon tooltips.'}
Write-Host 'PASS R27 source: snapshot-only OS icon classification, shared frozen vector, row/group/detail bindings; WPF/DPI execution remains separate.'
