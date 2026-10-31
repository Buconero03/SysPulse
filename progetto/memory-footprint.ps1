$ErrorActionPreference = 'Stop'
$os = Get-CimInstance Win32_OperatingSystem
$totalMiB = [double]$os.TotalVisibleMemorySize / 1024
$freeMiB = [double]$os.FreePhysicalMemory / 1024
[PSCustomObject]@{
    TotalRAMGiB = [Math]::Round($totalMiB / 1024, 2)
    UsedRAMGiB = [Math]::Round(($totalMiB - $freeMiB) / 1024, 2)
    PercentUsed = if ($totalMiB -gt 0) { [Math]::Round(100 * ($totalMiB - $freeMiB) / $totalMiB, 1) } else { $null }
} | Format-List
$service = Get-CimInstance Win32_Service -Filter "Name='ProcessLensGuardian'" -ErrorAction SilentlyContinue
$ids = @()
if ($service -and $service.ProcessId -gt 0) { $ids += [int]$service.ProcessId }
$rows = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -in @('ProcessLens', 'ProcessLens.MemoryAgent') -or $_.Id -in $ids
} | ForEach-Object {
    [PSCustomObject]@{
        Name = $_.ProcessName
        PID = $_.Id
        WorkingSetMiB = [Math]::Round($_.WorkingSet64 / 1MB, 1)
        PrivateMiB = [Math]::Round($_.PrivateMemorySize64 / 1MB, 1)
        Handles = $_.HandleCount
    }
})
$rows | Format-Table -AutoSize
Write-Host 'WorkingSet comprende pagine condivise: NON sommare i processi per stimare la RAM totale.'
Write-Host 'Confrontare campioni a riposo e sotto carico; non rappresenta un picco o una prova di memoria liberata.'
