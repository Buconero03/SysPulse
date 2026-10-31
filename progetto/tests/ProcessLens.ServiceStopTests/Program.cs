using ProcessLens.Core;

void Check(string name, bool okay)
{
    if (!okay) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
}

ServiceSnapshot S(string name, string? path, uint state = 4, int pid = 5156, uint type = 0x10) =>
    new() { Name = name, DisplayName = name, StateCode = state, ProcessId = pid,
            ServiceType = type, BinaryPath = path };
const string win = @"C:\Windows";
Check("Third-party AnyDesk eligible", ServiceStopPolicy.Evaluate(S("AnyDesk", @"C:\Apps\AnyDesk.exe"), win).Allowed);
Check("Nonessential Microsoft service in Windows directory eligible", ServiceStopPolicy.Evaluate(S("WSearch", @"C:\Windows\System32\SearchIndexer.exe"), win).Allowed);
Check("Nonessential service hosted in svchost eligible", ServiceStopPolicy.Evaluate(S("ExampleService", @"C:\Windows\System32\svchost.exe -k LocalService"), win).Allowed);
Check("Missing image path does not falsely deny SCM check", ServiceStopPolicy.Evaluate(S("ExampleService", null), win).Allowed);
Check("RPC essential denied", !ServiceStopPolicy.Evaluate(S("RpcSs", @"C:\Windows\System32\svchost.exe"), win).Allowed);
Check("Firewall essential denied", !ServiceStopPolicy.Evaluate(S("mpssvc", @"C:\Windows\System32\svchost.exe"), win).Allowed);
Check("Defender essential denied", !ServiceStopPolicy.Evaluate(S("WinDefend", @"C:\Program Files\Windows Defender\MsMpEng.exe"), win).Allowed);
Check("Stopped service denied", !ServiceStopPolicy.Evaluate(S("AnyDesk", @"C:\Apps\AnyDesk.exe", state: 1), win).Allowed);
Check("Driver service denied", !ServiceStopPolicy.Evaluate(S("VendorDriver", @"C:\Apps\driver.exe", type: 0x01), win).Allowed);
Check("No PID denied", !ServiceStopPolicy.Evaluate(S("AnyDesk", @"C:\Apps\AnyDesk.exe", pid: 0), win).Allowed);
Check("Empty name denied", !ServiceStopPolicy.Evaluate(S("", null), win).Allowed);
Console.WriteLine("Service stop policy tests complete. No service was stopped.");
