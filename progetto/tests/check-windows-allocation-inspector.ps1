$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$native = Get-Content (Join-Path $root 'src\ProcessLens.Windows\WindowsAllocationInspector.cs') -Raw -Encoding UTF8
$model = Get-Content (Join-Path $root 'src\ProcessLens.Core\WindowsAllocationEvidence.cs') -Raw -Encoding UTF8
$ui = Get-Content (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$checks = @(
    [PSCustomObject]@{ Name='QueryWorkingSetEx'; Passed=$native.Contains('QueryWorkingSetEx(handle, buffer, bytes)') }
    [PSCustomObject]@{ Name='VirtualQueryEx'; Passed=$native.Contains('VirtualQueryEx(handle, new IntPtr') }
    [PSCustomObject]@{ Name='PID lifetime checked'; Passed=$native.Contains('finalCreation != creation') }
    [PSCustomObject]@{ Name='read only handle'; Passed=$native.Contains('NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ') }
    [PSCustomObject]@{ Name='bounded scan'; Passed=$native.Contains('MaxSampledPages = 2500') -and $native.Contains('MaxDuration') }
    [PSCustomObject]@{ Name='sample transparency'; Passed=$model.Contains('pagine campionate') }
    [PSCustomObject]@{ Name='on demand UI integration'; Passed=$ui.Contains('_allocationInspector.Capture(row.Pid, row.Process.StartTime)') }
    [PSCustomObject]@{ Name='evidence in answer'; Passed=$ui.Contains('allocationReport.ToEvidenceText()') }
    [PSCustomObject]@{ Name='audit trail'; Passed=$ui.Contains('AuditStep("API ALLOCAZIONI WINDOWS"') }
)
$bad = @($checks | Where-Object { -not $_.Passed })
if ($bad.Count -gt 0) { throw ('FAIL Windows allocation inspector: ' + (($bad | ForEach-Object {$_.Name}) -join ', ')) }
Write-Host 'PASS native Windows allocation API, PID identity, bounded sampling, UI evidence and logs.' -ForegroundColor Green
