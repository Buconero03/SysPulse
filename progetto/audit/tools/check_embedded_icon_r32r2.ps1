$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
[xml]$project=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessLens.App.csproj') -Raw -Encoding UTF8
$embedded=@($project.Project.ItemGroup.EmbeddedResource | Where-Object { $_.Include -eq 'Assets/ProcessLens.ico' })
if($embedded.Count -ne 1 -or $embedded[0].LogicalName -ne 'ProcessLens.AppIcon'){throw 'FAIL R32 R2: icon must have one explicit manifest resource name.'}
if(@($project.Project.ItemGroup.Content | Where-Object { $_.Include -eq 'Assets/ProcessLens.ico' }).Count){throw 'FAIL R32 R2: optional icon cannot become WPF external content.'}
[xml]$view=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml') -Raw -Encoding UTF8
if($view.DocumentElement.HasAttribute('Icon')){throw 'FAIL R32 R2: optional icon cannot abort XAML construction.'}
$icon=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessLensIcon.cs') -Raw -Encoding UTF8
foreach($token in @('BitmapCacheOption.OnLoad','image.Freeze()','catch (Exception','IconFile.ReadEmbedded')){if(-not $icon.Contains($token)){throw ('FAIL R32 R2 icon loader: '+$token)}}
$tray=Get-Content (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
foreach($token in @('IconFile.TrySelectImage','CreateIconFromResourceEx','DestroyIcon(_trayIcon)')){if(-not $tray.Contains($token)){throw ('FAIL R32 R2 tray: '+$token)}}
$build=Get-Content (Join-Path $root 'build-dashboard.ps1') -Raw -Encoding UTF8
if(-not $build.Contains('Tests.ProcessLens.IconResourceTests')){throw 'FAIL R32 R2: actual resource test missing from build.'}
Write-Host 'PASS R32 R2: embedded optional window/tray icon, no mandatory XAML path or Content declaration; actual Windows startup remains separate.'
