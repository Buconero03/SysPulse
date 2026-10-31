$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
foreach ($suite in @('NetworkUiTests','SystemProcessUiTests')) {
    $path = Join-Path $root ('tests\ProcessLens.' + $suite + '\Program.cs')
    $code = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $main = $code.Substring($code.IndexOf('public static void Main()'))
    $setup = 'SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));'
    $firstAwait = 'Task continuation = VerifyDispatcherContinuationAsync(completion.Task, dispatcher, ownerThread);'
    if ($main.IndexOf($setup) -lt 0 -or $main.IndexOf($setup) -gt $main.IndexOf($firstAwait)) {
        throw "FAIL R14 R3: dispatcher context must precede asynchronous operations in $suite"
    }
    foreach ($token in @('SynchronizationContext.SetSynchronizationContext(previousContext);',
        'TaskCreationOptions.RunContinuationsAsynchronously', 'dispatcher.CheckAccess()',
        'workerThread != ownerThread', 'Complete(continuation);')) {
        if (-not $code.Contains($token)) { throw "FAIL R14 R3 fixture context: $suite / $token" }
    }
    if ($main.IndexOf($firstAwait) -gt $main.IndexOf('ThreadPool.QueueUserWorkItem(')) {
        throw "FAIL R14 R3: fixture must await a pending task before worker completion in $suite"
    }
}
Write-Host 'PASS R14 R3: headless WPF context setup and asynchronous ownership preflight supplied (source check).'
