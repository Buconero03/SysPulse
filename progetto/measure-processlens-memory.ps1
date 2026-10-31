# Passive counter sampling: no profiler, no GC, no process starts/stops, no working-set trimming.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$TargetPid,
    [Parameter(Mandatory=$true)][ValidateSet('R7','R8')][string]$Label,
    [ValidateRange(10,1800)][int]$DurationSeconds = 180,
    [ValidateRange(1,30)][int]$IntervalSeconds = 5,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Scenario = 'processes-idle'
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'NON ESEGUITO: Windows richiesto.' }
if (-not ('ProcessLensPassiveMemoryR8' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ProcessLensPassiveMemoryR8 {
    [StructLayout(LayoutKind.Sequential)]
    public struct Counters {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        public UIntPtr PrivateUsage, PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }
    [DllImport("psapi.dll", SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessMemoryInfo(IntPtr process, ref Counters counters, uint size);
}
'@
}
$target = Get-Process -Id $TargetPid -ErrorAction Stop
$started = $target.StartTime.ToUniversalTime()
$version = 'N/D'
try { $version = $target.MainModule.FileVersionInfo.ProductVersion } catch { }
$directory = Join-Path $PSScriptRoot 'verification-r8'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$file = Join-Path $directory ($Label + '-' + $Scenario + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.csv')
$rows = New-Object System.Collections.Generic.List[object]
$watch = [Diagnostics.Stopwatch]::StartNew()
try {
    while ($watch.Elapsed.TotalSeconds -le $DurationSeconds) {
        $target.Refresh()
        if ($target.HasExited -or $target.StartTime.ToUniversalTime() -ne $started) { throw 'Istanza terminata/cambiata: raccolta interrotta.' }
        $privateWs = $null; $nativeError = $null
        $workingSet = $null; $commit = $null
        try { $workingSet = [math]::Round($target.WorkingSet64 / 1MB, 3) } catch { }
        try { $commit = [math]::Round($target.PrivateMemorySize64 / 1MB, 3) } catch { }
        $native = New-Object ProcessLensPassiveMemoryR8+Counters
        $native.cb = [Runtime.InteropServices.Marshal]::SizeOf($native)
        try {
            if ([ProcessLensPassiveMemoryR8]::GetProcessMemoryInfo($target.Handle, [ref]$native, $native.cb)) {
                $privateWs = [math]::Round($native.PrivateWorkingSetSize.ToUInt64() / 1MB, 3)
            } else { $nativeError = [Runtime.InteropServices.Marshal]::GetLastWin32Error() }
        } catch { $nativeError = $_.Exception.GetType().Name }
        $rows.Add([PSCustomObject]@{
            Label=$Label; Scenario=$Scenario; Version=$version; PID=$TargetPid;
            StartedUtc=$started.ToString('o'); SampleUtc=[DateTime]::UtcNow.ToString('o');
            ElapsedSeconds=[math]::Round($watch.Elapsed.TotalSeconds,2);
            PrivateWorkingSetMiB=$privateWs; WorkingSetMiB=$workingSet; PrivateCommitMiB=$commit;
            NativeCounterError=$nativeError
        })
        Start-Sleep -Seconds $IntervalSeconds
    }
}
finally {
    $rows | Export-Csv -LiteralPath $file -NoTypeInformation -Encoding UTF8
    $target.Dispose()
    Write-Host ('Campioni salvati: ' + $file)
}
Write-Host 'Confrontare stesso scenario e intervallo: PrivateWorkingSetMiB, WorkingSetMiB e commit sono metriche diverse. N/D/vuoto non equivale a zero.'
