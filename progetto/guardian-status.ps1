$name = 'ProcessLensGuardian'
$s = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($null -eq $s) { Write-Host 'Guardian non installato.' } else { $s | Format-Table Name,Status,StartType }
$data = Join-Path $env:ProgramData 'ProcessLens\Guardian'
$db = Join-Path $data 'guardian.sqlite3'
if (Test-Path $db) { Get-Item $db | Select-Object FullName,Length,LastWriteTime | Format-List } else { Write-Host 'Database non trovato (o accesso negato).' }
$reports = Join-Path $data 'reports'
if (Test-Path $reports) { Get-ChildItem $reports -Filter '*.md' | Sort-Object Name -Descending | Select-Object -First 5 Name,Length,LastWriteTime | Format-Table }
Write-Host "Report: $reports"
