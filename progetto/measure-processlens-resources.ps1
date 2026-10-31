# Passive read-only measurement of one existing process. No launches, stops, profiler, GC or trimming.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$TargetPid,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label,
    [ValidateRange(10,1800)][int]$DurationSeconds = 180,
    [ValidateRange(1,30)][int]$IntervalSeconds = 2,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Scenario = 'processes-idle'
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'NON ESEGUITO: Windows richiesto.' }
if (-not ('ProcessLensPassiveResourcesR7' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ProcessLensPassiveResourcesR7 {
    [StructLayout(LayoutKind.Sequential)] public struct Counters {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        public UIntPtr PrivateUsage, PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }
    [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern uint GetActiveProcessorCount(ushort group);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool GetProcessTimes(IntPtr process, out ulong creation, out ulong exit, out ulong kernel, out ulong user);
    [DllImport("psapi.dll", SetLastError=true)] public static extern bool GetProcessMemoryInfo(IntPtr process, ref Counters counters, uint size);
}
'@
}
$target = Get-Process -Id $TargetPid -ErrorAction Stop
$started = $target.StartTime.ToUniversalTime()
$version = 'N/D'
try { $version = $target.MainModule.FileVersionInfo.ProductVersion } catch { }
$directory = Join-Path $PSScriptRoot 'verification-r10r7'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$file = Join-Path $directory ($Label + '-' + $Scenario + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.csv')
$rows = New-Object System.Collections.Generic.List[object]
$watch = [Diagnostics.Stopwatch]::StartNew()
$previousCpu = $null; $previousAt = $null; $previousCpus = $null
$handle = [IntPtr]::Zero
try {
    $handle = [ProcessLensPassiveResourcesR7]::OpenProcess(0x1000, $false, [uint32]$TargetPid)
    if ($handle -eq [IntPtr]::Zero) { throw ('OpenProcess non riuscita, errore ' + [Runtime.InteropServices.Marshal]::GetLastWin32Error()) }
    while ($watch.Elapsed.TotalSeconds -le $DurationSeconds) {
        $target.Refresh()
        if ($target.HasExited -or $target.StartTime.ToUniversalTime() -ne $started) { throw 'Istanza terminata o cambiata: raccolta interrotta.' }
        [uint64]$creation=0; [uint64]$exitTime=0; [uint64]$kernel=0; [uint64]$userTime=0
        $cpu=$null; $cpuTicks=$null; $cpuError=$null; $sampleSeconds=$null
        $cpus = [ProcessLensPassiveResourcesR7]::GetActiveProcessorCount(0xffff)
        if ([ProcessLensPassiveResourcesR7]::GetProcessTimes($handle,[ref]$creation,[ref]$exitTime,[ref]$kernel,[ref]$userTime)) {
            if ($creation -ne [uint64]($started.ToFileTimeUtc())) { throw 'Creazione non corrispondente: nessun campione attribuito al PID.' }
            $at = [Diagnostics.Stopwatch]::GetTimestamp() / [double][Diagnostics.Stopwatch]::Frequency
            $cpuTicks = [double]$kernel + [double]$userTime
            if ($null -ne $previousCpu -and $cpus -gt 0 -and $previousCpus -eq $cpus) {
                $sampleSeconds = $at - $previousAt
                if ($sampleSeconds -gt 0 -and $cpuTicks -ge $previousCpu) {
                    $cpu = [math]::Min(100,[math]::Max(0,100 * ($cpuTicks - $previousCpu) / (10000000 * $sampleSeconds * $cpus)))
                }
            }
            $previousCpu=$cpuTicks; $previousAt=$at; $previousCpus=$cpus
        } else {
            $cpuError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            $previousCpu=$null; $previousAt=$null; $previousCpus=$null
        }
        $privateWs=$null; $workingSet=$null; $commit=$null; $nativeError=$null
        $native = New-Object ProcessLensPassiveResourcesR7+Counters
        $native.cb = [Runtime.InteropServices.Marshal]::SizeOf($native)
        $sentinel = if ([IntPtr]::Size -eq 8) { [uint64]::MaxValue } else { [uint64][uint32]::MaxValue }
        $native.PrivateWorkingSetSize = [UIntPtr]::new($sentinel)
        if ([ProcessLensPassiveResourcesR7]::GetProcessMemoryInfo($handle,[ref]$native,$native.cb)) {
            $workingSet = $native.WorkingSetSize.ToUInt64() / 1MB
            $commit = $native.PrivateUsage.ToUInt64() / 1MB
            $privateBytes = $native.PrivateWorkingSetSize.ToUInt64()
            if ($privateBytes -ne $sentinel -and $privateBytes -le $native.WorkingSetSize.ToUInt64()) { $privateWs = $privateBytes / 1MB }
        } else { $nativeError = [Runtime.InteropServices.Marshal]::GetLastWin32Error() }
        $rows.Add([PSCustomObject]@{
            Label=$Label; Scenario=$Scenario; Version=$version; PID=$TargetPid; Culture=$PSCulture;
            StartedUtc=$started.ToString('o'); SampleUtc=[DateTime]::UtcNow.ToString('o');
            ElapsedSeconds=$watch.Elapsed.TotalSeconds; CpuPercentTime=$cpu; CpuIntervalSeconds=$sampleSeconds;
            CpuCumulativeTicks=$cpuTicks; LogicalProcessors=$cpus; CpuError=$cpuError;
            PrivateWorkingSetMiB=$privateWs; WorkingSetMiB=$workingSet; PrivateCommitMiB=$commit; NativeCounterError=$nativeError
        })
        Start-Sleep -Seconds $IntervalSeconds
    }
}
finally {
    $rows | Export-Csv -LiteralPath $file -NoTypeInformation -Encoding UTF8
    if ($handle -ne [IntPtr]::Zero) { [ProcessLensPassiveResourcesR7]::CloseHandle($handle) | Out-Null }
    $target.Dispose()
    Write-Host ('Campioni salvati: ' + $file)
}
$summary = [ordered]@{ Label=$Label; Scenario=$Scenario; PID=$TargetPid; Version=$version; Samples=$rows.Count; Metrics=[ordered]@{} }
foreach ($metric in @('CpuPercentTime','PrivateWorkingSetMiB','WorkingSetMiB','PrivateCommitMiB')) {
    $values = @($rows | Where-Object { $null -ne $_.$metric } | ForEach-Object { [double]$_.$metric } | Sort-Object)
    $entry = [ordered]@{ AvailableSamples=$values.Count; Median=$null; SampledPeak=$null }
    if ($values.Count -gt 0) {
        $middle = [int][math]::Floor($values.Count / 2)
        $entry.Median = if ($values.Count % 2 -eq 0) { ($values[$middle-1] + $values[$middle]) / 2 } else { $values[$middle] }
        $entry.SampledPeak = $values[-1]
    }
    $summary.Metrics[$metric] = $entry
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ([IO.Path]::ChangeExtension($file,'.json')) -Encoding UTF8
Write-Host 'CSV e riepilogo: solo PID indicato, picchi dei campioni e contatori distinti. Vuoto/N/D non equivale a zero. Confrontare stessa durata, scenario e colonna.'
