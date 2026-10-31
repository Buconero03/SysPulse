using ProcessLens.Core;

static ServiceSnapshot Make(uint state, int? start, int pid) => new()
{
    Name = "example", DisplayName = "Esempio", StateCode = state,
    StartupCode = start, ProcessId = pid, ServiceType = 0x20
};

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL " + label);
    Console.WriteLine("PASS " + label);
}

var auto = Make(4, 2, 1234);
Check(auto.IsRunning && auto.StartupLabel == "Automatico", "state and start");
Check(ServiceExplanation.WhyRunning(auto).Contains("Richiedente non disponibile in SCM", StringComparison.OrdinalIgnoreCase),
    "no invented process initiator");
var manual = Make(4, 3, 1234);
Check(ServiceExplanation.WhyRunning(manual).Contains("trigger", StringComparison.OrdinalIgnoreCase),
    "manual start not assumed user initiated");
var stopped = Make(1, 4, 0);
Check(!stopped.IsRunning && stopped.ProcessIdLabel == "—", "stopped no PID");
Check(ServiceExplanation.Impact(stopped).Contains("arrestato"), "stopped impact");
Check(ServiceExplanation.Association(auto, 3, "svchost.exe").Contains("3 servizi"), "shared host count");
Check(ServiceExplanation.Association(stopped, 0, null).Contains("PID ospitante: N/D"), "no fabricated host");
Console.WriteLine("Service explanation tests completed.");

Check(ServiceExplanation.Impact(auto).Contains("solo su conferma") &&
    !ServiceExplanation.Impact(auto).Contains("non modifica i servizi"), "R7 SCM stop capability described accurately");
Check(ServiceExplanation.Impact(stopped).Contains("altri servizi"), "R7 stopped service does not describe entire host activity");
Check((auto with { ServiceType = 1 }).HostType == "Driver o altro tipo di servizio", "R7 driver type not labelled Win32");
