$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$main = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml'
$dashboard = Join-Path $root 'src\ProcessLens.App\GuardianDashboardView.xaml'
$code = Join-Path $root 'src\ProcessLens.App\GuardianDashboardView.xaml.cs'
$reader = Join-Path $root 'src\ProcessLens.App\GuardianDashboardReader.cs'
$lineage = Join-Path $root 'src\ProcessLens.App\GuardianLineageReader.cs'
$mainCode = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs'
foreach ($xaml in @($main,$dashboard)) {
    $xml = [System.Xml.XmlDocument]::new()
    $xml.Load($xaml)
}
$mainText = [IO.File]::ReadAllText($main)
$mainLogic = [IO.File]::ReadAllText($mainCode)
$viewText = [IO.File]::ReadAllText($dashboard)
$viewLogic = [IO.File]::ReadAllText($code)
$readLogic = [IO.File]::ReadAllText($reader)
$lineageLogic = [IO.File]::ReadAllText($lineage)
foreach ($token in @('NavGuardianButton_Click','GuardianDashboardView','GuardianDashboard?.SetActive(guardian)')) {
    if (-not ($mainText.Contains($token) -or $mainLogic.Contains($token))) { throw "Dashboard navigation token missing: $token" }
}
foreach ($name in @('EventsTable','TopProcessesTable','ParentsTable','ResourcesTable','DiagnosticsTable','ActivityTrendList','LineagePidBox','LineageAncestorsTable','LineageChildrenTable')) {
    if (-not $viewText.Contains('x:Name="' + $name + '"')) { throw "Dashboard control missing: $name" }
    if (-not $viewLogic.Contains($name)) { throw "Dashboard binding missing: $name" }
}
foreach ($token in @('SqliteOpenMode.ReadOnly','Pooling = false','LIMIT $limit','CommandTimeout = 2')) {
    if (-not $readLogic.Contains($token)) { throw "Read-only guard missing: $token" }
}
foreach ($token in @('SqliteOpenMode.ReadOnly','query_only=ON','focusStartEventId','LastLifecycle','maxChildren')) {
    if (-not $lineageLogic.Contains($token)) { throw "Lineage read-only/correlation guard missing: $token" }
}
foreach ($token in @('AnalyzeLineage_Click','UseParent_Click','ExploreChild_Click','ParentsTable_MouseDoubleClick','LineageChildrenTable_MouseDoubleClick')) {
    if (-not $viewText.Contains($token) -or -not $viewLogic.Contains($token)) { throw "Lineage UI handler missing: $token" }
}
# Regression: do not shadow the direct-child query command with the count-query
# command name. This produced CS0136 and prevented v0.15.2 tests from compiling.
if ($lineageLogic -notmatch 'using \(var countCmd = Query\(' -or
    $lineageLogic -notmatch 'totalChildren = Convert\.ToInt64\(countCmd\.ExecuteScalar\(') {
    throw 'GuardianLineageReader.cs: missing distinct countCmd for direct-child count (CS0136 regression).'
}
if ($lineageLogic.Contains('ReadWriteCreate') -or $lineageLogic.Contains('DELETE FROM') -or $lineageLogic.Contains('INSERT INTO')) {
    throw 'Lineage reader contains a writing operation.'
}
# Catch missing System.IO imports (Path/File/FileInfo/FileNotFoundException),
# which synthetic SQLite tests alone cannot detect in the WPF project.
if ($readLogic -notmatch '(?m)^\s*using\s+System\.IO\s*;') {
    throw 'GuardianDashboardReader.cs: missing using System.IO; (WPF CS0103/CS0246).'
}
# Avoid a possible null reference when WPF service controls are not yet initialized.
if ($mainLogic -notmatch 'if \(ServiceStopButton is \{ \} stopButton && _selectedService is') {
    throw 'MainWindow.xaml.cs: missing null guard for ServiceStopButton.'
}
if ($readLogic.Contains('ReadWriteCreate') -or $readLogic.Contains('DELETE FROM') -or $readLogic.Contains('INSERT INTO')) {
    throw 'Dashboard reader contains a writing operation.'
}
Write-Host 'PASS Guardian dashboard: valid XML, navigation, bindings, System.IO import, null guard and read-only SQLite access.' -ForegroundColor Green
