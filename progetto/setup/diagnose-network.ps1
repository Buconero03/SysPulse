param([switch] $NoPrompt)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Diagnostica disponibile su Windows.' }
$lines = New-Object 'System.Collections.Generic.List[string]'
function Add-Result([string] $Text) { $lines.Add($Text); Write-Host $Text }
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Add-Result ('ProcessLens - prerequisiti rete - ' + [DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))
Add-Result ('Windows: ' + [Environment]::OSVersion.Version.ToString() + ' | processo 64 bit: ' + [Environment]::Is64BitProcess)
Add-Result ('Amministratore: ' + $admin)
Add-Result 'Controllo locale: nessuna cattura, scansione Wi-Fi, sonda, modifica a servizi, audit o firewall.'
foreach ($name in @('WlanSvc', 'EventLog')) {
    try { $service = Get-Service -Name $name -ErrorAction Stop; Add-Result ('Servizio ' + $name + ': ' + $service.Status) }
    catch { Add-Result ('Servizio ' + $name + ': N/D - ' + $_.Exception.Message) }
}
try {
    $adapters = @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces())
    $wifi = @($adapters | Where-Object { $_.NetworkInterfaceType -eq [Net.NetworkInformation.NetworkInterfaceType]::Wireless80211 })
    Add-Result ('Interfacce classificate Wi-Fi da Windows: ' + $wifi.Count)
    $ipv4 = 0
    foreach ($adapter in $adapters) {
        if ($adapter.OperationalStatus -eq [Net.NetworkInformation.OperationalStatus]::Up -and
            $adapter.NetworkInterfaceType -ne [Net.NetworkInformation.NetworkInterfaceType]::Loopback) {
            $ipv4 += @($adapter.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork }).Count
        }
    }
    Add-Result ('Indirizzi IPv4 locali su interfacce attive: ' + $ipv4 + ' (indirizzi non salvati nel rapporto)')
} catch { Add-Result ('Inventario interfacce: N/D - ' + $_.Exception.Message) }
try {
    if (-not ('ProcessLensSetup.NativeCheck' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace ProcessLensSetup {
 public static class NativeCheck {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern IntPtr LoadLibraryEx(string name, IntPtr file, uint flags);
  [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true)]
  static extern IntPtr GetProcAddress(IntPtr module, string name);
  [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
  [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr client);
  [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr client, IntPtr reserved, out IntPtr list);
  [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);
  [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr client, IntPtr reserved);
  public static string Exports(string path) {
   IntPtr module=LoadLibraryEx(path,IntPtr.Zero,0x800);
   if(module==IntPtr.Zero) return "PktMon API: N/D - caricamento DLL di sistema, Win32 " + Marshal.GetLastWin32Error();
   try {
    string[] names={"PacketMonitorInitialize","PacketMonitorUninitialize","PacketMonitorCreateLiveSession","PacketMonitorEnumDataSources","PacketMonitorCreateRealtimeStream","PacketMonitorAddSingleDataSourceToSession","PacketMonitorAttachOutputToSession","PacketMonitorSetSessionActive","PacketMonitorCloseSessionHandle","PacketMonitorCloseRealtimeStream"};
    var missing=new System.Collections.Generic.List<string>();
    foreach(string name in names) if(GetProcAddress(module,name)==IntPtr.Zero) missing.Add(name);
    return missing.Count==0 ? "PktMon API: tutti i 10 export richiesti presenti; motore non avviato." : "PktMon API: export mancanti: " + string.Join(", ",missing.ToArray());
   } finally {FreeLibrary(module);}
  }
  public static string Wlan() {
   IntPtr client=IntPtr.Zero, list=IntPtr.Zero; uint version;
   try {
    uint error=WlanOpenHandle(2,IntPtr.Zero,out version,out client);
    if(error!=0) return "WLAN OpenHandle: Win32 " + error + " - " + new Win32Exception((int)error).Message;
    error=WlanEnumInterfaces(client,IntPtr.Zero,out list);
    if(error!=0) return "WLAN EnumInterfaces: Win32 " + error + " - " + new Win32Exception((int)error).Message;
    if(list==IntPtr.Zero) return "WLAN: N/D - tabella mancante.";
    int count=Marshal.ReadInt32(list);
    if(count<0 || count>16) return "WLAN: N/D - conteggio fuori limite.";
    return "WLAN API: " + count + " interfacce restituite; cache BSS e permesso Posizione non verificati.";
   } finally {if(list!=IntPtr.Zero) WlanFreeMemory(list); if(client!=IntPtr.Zero) WlanCloseHandle(client,IntPtr.Zero);}
  }
 }
}
'@
    }
    Add-Result ([ProcessLensSetup.NativeCheck]::Wlan())
    Add-Result ([ProcessLensSetup.NativeCheck]::Exports((Join-Path $env:SystemRoot 'System32\pktmonapi.dll')))
} catch { Add-Result ('Verifica API native: N/D - ' + $_.Exception.Message) }
$reader = $null; $record = $null
try {
    $query = New-Object System.Diagnostics.Eventing.Reader.EventLogQuery('Security', [System.Diagnostics.Eventing.Reader.PathType]::LogName, '*')
    $query.ReverseDirection = $true
    $reader = New-Object System.Diagnostics.Eventing.Reader.EventLogReader($query)
    $record = $reader.ReadEvent([TimeSpan]::FromSeconds(1))
    Add-Result 'Registro Security: lettura consentita. Contenuto degli eventi non salvato.'
} catch { Add-Result ('Registro Security: N/D - ' + $_.Exception.Message) }
finally { if ($record) { $record.Dispose() }; if ($reader) { $reader.Dispose() } }
$auditpol = Join-Path $env:SystemRoot 'System32\auditpol.exe'
foreach ($guid in @('{0CCE9226-69AE-11D9-BED3-505054503030}', '{0CCE9215-69AE-11D9-BED3-505054503030}')) {
    try {
        Add-Result ('Audit in sola lettura: ' + $guid)
        $text = @(& $auditpol /get "/subcategory:$guid" 2>&1)
        $exitCode = $LASTEXITCODE
        foreach ($line in ($text | Select-Object -First 16)) { Add-Result ([string]$line) }
        Add-Result ('Auditpol exit code: ' + $exitCode)
    } catch { Add-Result ('Audit: N/D - ' + $_.Exception.Message) }
}
Add-Result ''
Add-Result 'PROSSIMI CONTROLLI'
Add-Result 'Wi-Fi assente: verificare scheda, driver del produttore e WLAN AutoConfig. Un installer non aggiunge una scheda radio.'
Add-Result 'Wi-Fi accesso negato: Impostazioni > Privacy e sicurezza > Posizione. Amministratore non sostituisce il consenso.'
Add-Result 'PktMon export assenti: dipendono dalla versione di Windows; non copiare DLL da altri PC. La cattura IPv4 locale e separata.'
Add-Result 'Segnali Windows: audit disabilitato o registro non accessibile non significa nessun tentativo di accesso.'
Add-Result 'In ProcessLens: Elenca sorgenti Windows > seleziona sorgente > Cattura per 20 s; per Wi-Fi usa Leggi reti Wi-Fi e Cache radio Wi-Fi.'
Add-Result 'Nessuno di questi controlli conta tutti i client del router o dimostra che qualcuno stia spiando un IP.'
$logRoot = Join-Path $env:LOCALAPPDATA 'ProcessLens\logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$report = Join-Path $logRoot 'rete-prerequisiti.txt'
[IO.File]::WriteAllLines($report, $lines, (New-Object Text.UTF8Encoding($true)))
Write-Host ('Rapporto: ' + $report) -ForegroundColor Cyan
if (-not $NoPrompt) {
    $answer = Read-Host 'P: apri Posizione | S: apri Servizi | A: ripeti come amministratore | Invio: termina'
    switch ($answer.ToUpperInvariant()) {
        'P' { Start-Process 'ms-settings:privacy-location' }
        'S' { Start-Process (Join-Path $env:SystemRoot 'System32\services.msc') }
        'A' {
            if (-not $admin) {
                $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
                Start-Process -FilePath $ps -Verb RunAs -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"')
            }
        }
    }
}
