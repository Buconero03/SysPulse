using ProcessLens.Core;

static void Check(bool value, string reason)
{
    if (!value) throw new Exception("FAIL " + reason);
    Console.WriteLine("PASS " + reason);
}

var ts = new DateTime(2026, 10, 8, 17, 30, 0, DateTimeKind.Local);
var service = new ServiceSnapshot
{
    Name = "WSearch", DisplayName = "Windows Search",
    StateCode = 4, ServiceType = 0x20, ProcessId = 1234
};
string?[] running = ["Windows Search", "running"];
string?[] start = ["Windows Search", "start"];
var transition = ServiceHistoryInterpretation.Decode("WSearch", "Windows Search", 7036, ts, running, 15);
Check(transition is { EventId: 7036, RecordId: 15 }, "7036 transition parsed");
Check(transition!.Detail.Contains("running", StringComparison.Ordinal), "raw state preserved");
Check(transition.WhenLabel == "08/10/2026 17:30:00", "timestamp formatted");
Check(ServiceHistoryInterpretation.Decode("OtherService", "Other Service", 7036, ts, running, 16) is null,
    "do not confuse unrelated service");
Check(ServiceHistoryInterpretation.Decode("WSearch", "Windows Search", 7035, ts, start, 17) is { EventId: 7035 },
    "7035 start control recorded");
Check(ServiceHistoryInterpretation.Decode("WSearch", "Windows Search", 7040, ts,
    ["Windows Search", "manual", "auto"], 18) is { EventId: 7040 }, "7040 config change");
Check(ServiceHistoryInterpretation.Decode("WSearch", "Windows Search", 7045, ts,
    ["WSearch", "C:\\Windows\\System32\\svchost.exe"], 19) is { EventId: 7045 }, "7045 install by service name");
Check(ServiceHistoryInterpretation.Decode("WSearch", "Windows Search", 7023, ts, running, 20) is null,
    "unrecognized event ignored");
Check(ServiceHistoryInterpretation.Decode("WSearch", "Windows Search", 7036, ts, [], 21) is null,
    "missing payload ignored");
var explanation = ServiceHistoryInterpretation.Explain(service, [transition!], available: true);
Check(explanation.Contains("Richiedente sconosciuto", StringComparison.OrdinalIgnoreCase),
    "historical event must not attribute caller");
Check(ServiceHistoryInterpretation.Explain(service, [], available: false)
    .Contains("Cronologia SCM: N/D", StringComparison.Ordinal), "error does not fabricate history");
Check(ServiceHistoryInterpretation.Explain(service, [], available: true)
    .Contains("Nessun evento", StringComparison.Ordinal), "empty history not asserted as no starts");
Console.WriteLine("Service history interpretation tests completed.");
