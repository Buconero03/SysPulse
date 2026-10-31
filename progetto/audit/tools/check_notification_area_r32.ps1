$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$tray=Get-Content (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
$app=Get-Content (Join-Path $root 'src/ProcessLens.App/App.xaml.cs') -Raw -Encoding UTF8
$icons=(Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessIconResolver.cs') -Raw -Encoding UTF8) + (Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessSymbolDrawing.cs') -Raw -Encoding UTF8)
foreach($v in @('internal void RequestHideToTray()','if (!EnsureTray())','_window.Hide()','HIDDEN_TO_TRAY','_hiddenChildren.Clear()','TaskbarCreated','!_window.IsVisible','DestroyIcon(_trayIcon)')) { if(-not $tray.Contains($v)){throw ('FAIL R32 tray: '+$v)} }
if(-not $app.Contains('window.MinimizeRequested += _windowController.RequestHideToTray')){throw 'FAIL R32 header must use notification area hide.'}
foreach($v in @('NativeIconPreference.ResolveExecutable','Placeholder(processName)','ProcessSymbolCatalog.Find','Distintivo grafico','image.Freeze()')) { if(-not $icons.Contains($v)){throw ('FAIL R32 icons: '+$v)} }
[xml]$view=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml') -Raw -Encoding UTF8
$button=$view.SelectSingleNode("//*[@*[local-name()='Name']='MinimizeWindowButton']")
if($button.GetAttribute('Content') -ne ('Nascondi accanto all'+[char]0x2019+'orologio')){throw 'FAIL R32 explicit action label.'}
if(-not (Test-Path (Join-Path $root 'src/ProcessLens.App/Assets/ProcessLens.ico'))){throw 'FAIL R32 notification icon asset missing.'}
Write-Host 'PASS R32 source: explicit notification-area hide, registered icon prerequisite, owned windows and restore, resource-first icons and distinct placeholders. Physical Windows verification remains separate.'
