using System.ComponentModel;
using System.Text.Json;
using ProcessLens.Core;

static void Require(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL diagnostic test: " + label);
    Console.WriteLine("PASS diagnostic test: " + label);
}
var entry = new DiagnosticEvent
{
    EventId = DiagnosticEventIds.RamMeasurement,
    Component = "ProcessLens.MemoryAgent", Operation = "PhysicalMemory.Read",
    Api = "GlobalMemoryStatusEx", OperationId = Guid.NewGuid().ToString("N"),
    Severity = "Warning", Status = "Unavailable", ReasonCode = "RAM_READ_FAILED",
    Complete = false, Measurements = new Dictionary<string,double> { ["duration_ms"] = 12 }
};
using var data = JsonDocument.Parse(StructuredDiagnostics.Serialize(entry));
var root = data.RootElement;
Require(root.GetProperty("schema_version").GetInt32() == 1, "schema version");
Require(root.GetProperty("event_id").GetString() == "PL-MEM-2001", "stable event code");
Require(root.GetProperty("measurements").GetProperty("duration_ms").GetDouble() == 12, "typed measurements and units");
Require(root.GetProperty("complete").GetBoolean() == false, "incomplete not marked successful");
Require(!root.TryGetProperty("target_pid", out _) && !root.TryGetProperty("native_exit_code", out _), "absent evidence omitted, not zero");
var error = StructuredDiagnostics.ErrorDetails(new Win32Exception(5, "password=topsecret"));
Require(error.Win32ErrorCode == 5 && error.HResultHex.StartsWith("0x"), "native error details");
var safe = entry with { ExceptionType = error.ExceptionType, HResultHex = error.HResultHex, Win32ErrorCode = error.Win32ErrorCode };
Require(!StructuredDiagnostics.Serialize(safe).Contains("topsecret"), "exception message excluded");
Require(!StructuredDiagnostics.ValidEventId("PL-RANDOM-9000"), "event namespace controlled");
bool rejected = false;
try { StructuredDiagnostics.Serialize(entry with { Operation = "Authorization: Bearer secret" }); }
catch (ArgumentException) { rejected = true; }
Require(rejected, "free-form sensitive text rejected");
Require(StructuredDiagnostics.MaximumLogBytes <= 1024 * 1024, "bounded log size");
Console.WriteLine("ProcessLens structured diagnostic tests passed.");
