# Prevent a known WPF BAML connectionId crash: event-wired ContextMenu
# inside Style.Setter.Value was misconnected to a Grid during InitializeComponent.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xaml = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml'
$code = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs'
$document = [System.Xml.XmlDocument]::new()
$document.Load($xaml)
$namespaces = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
$namespaces.AddNamespace('wpf', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$namespaces.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
$style = $document.SelectSingleNode('//wpf:Style[@x:Key="ProcessListItemStyle"]', $namespaces)
if ($null -eq $style) { throw 'Preflight: ProcessListItemStyle non trovato.' }
$bad = $style.SelectNodes('.//wpf:Setter[@Property="ContextMenu"]//wpf:ContextMenu', $namespaces)
if ($bad.Count -gt 0) { throw 'Preflight: ContextMenu in Style.Setter.Value: possibile crash connectionId/BAML.' }
$loaded = $style.SelectSingleNode('./wpf:EventSetter[@Event="Loaded" and @Handler="ProcessItem_Loaded"]', $namespaces)
if ($null -eq $loaded) { throw 'Preflight: EventSetter Loaded / ProcessItem_Loaded mancante.' }
$cs = [System.IO.File]::ReadAllText($code)
foreach ($token in @('void ProcessItem_Loaded(', 'menu.Opened += ProcessContextMenu_Opened;', 'terminate.Click += TerminateProcessMenuItem_Click;', 'copyPid.Click += CopyPidMenuItem_Click;')) {
    if (-not $cs.Contains($token)) { throw "Preflight: collegamento C# mancante: $token" }
}
# Validate both process and service rows; creating a ContextMenu in XAML
# resources triggered a runtime BAML connectionId / invalid cast exception.
$serviceStyle = $document.SelectSingleNode('//wpf:Style[@x:Key="ServiceListItemStyle"]', $namespaces)
if ($null -eq $serviceStyle) { throw 'Preflight: ServiceListItemStyle non trovato.' }
$badService = $serviceStyle.SelectNodes('.//wpf:Setter[@Property="ContextMenu"]//wpf:ContextMenu', $namespaces)
if ($badService.Count -gt 0) { throw 'Preflight: menu servizi in Style.Setter.Value: rischio crash WPF.' }
foreach ($pair in @(@('Loaded', 'ServiceItem_Loaded'), @('PreviewMouseRightButtonDown', 'ServiceItem_RightMouseDown'))) {
    $eventName = $pair[0]
    $handlerName = $pair[1]
    $eventNode = $serviceStyle.SelectSingleNode("./wpf:EventSetter[@Event='$eventName' and @Handler='$handlerName']", $namespaces)
    if ($null -eq $eventNode) { throw "Preflight: handler mancante su riga servizi: $handlerName" }
}
foreach ($token in @('void ServiceItem_Loaded(', 'void ServiceItem_RightMouseDown(',
    'menu.Opened += ServiceContextMenu_Opened;', 'stop.Click += ServiceContextStop_Click;',
    'await RequestStopServiceAsync(service);', 'Tag = service',
    'host.Click += ServiceContextShowHost_Click;', 'copyName.Click += ServiceContextCopyName_Click;')) {
    if (-not $cs.Contains($token)) { throw "Preflight: menu servizi, collegamento C# mancante: $token" }
}
Write-Host 'PASS menu servizi: tasto destro, azione SCM e PID ospitante collegati.' -ForegroundColor Green
Write-Host 'PASS XAML: nessun ContextMenu con handler dentro Style.Setter.Value.' -ForegroundColor Green
Write-Host 'PASS menu contestuale: creazione e handlers in C#.' -ForegroundColor Green
