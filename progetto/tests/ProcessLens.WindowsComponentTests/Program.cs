using System.Globalization;
using ProcessLens.Core;

static void Check(bool ok, string label)
{ if (!ok) throw new Exception("FAIL Windows components: " + label); Console.WriteLine("PASS Windows components: " + label); }
static SystemProcessEntry Row(int pid, string name, string? path = null, long? ram = null) =>
    new(pid, 4, name, 2, 10, 0, ram, path, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc));
const string win = @"D:\Windows";
Check(WindowsComponentCatalog.Definitions.Count == 31 && WindowsComponentCatalog.Definitions.Select(d => d.Id).Distinct().Count() == 31,
    "expanded finite catalog has 31 distinct component families");
foreach (var d in WindowsComponentCatalog.Definitions)
{
    var row = Row(120, d.Name, d.RelativePath is null ? null : win + "\\" + d.RelativePath);
    Check(WindowsComponentCatalog.Match(row, win)?.Id == d.Id, "expected component path or internal name: " + d.Name);
    if (d.RelativePath is not null)
        Check(WindowsComponentCatalog.Match(row with { Path = @"D:\Users\fake\" + d.Name }, win) is null,
            "same name outside expected Windows location excluded: " + d.Name);
}
foreach (string name in new[] { "chrome.exe", "notepad.exe", "powershell.exe", "ProcessLens.exe", "service-vendor.exe" })
    Check(WindowsComponentCatalog.Match(Row(120, name, win + @"\System32\" + name), win) is null,
        "Windows folder alone cannot include an ordinary program: " + name);
foreach (string path in new[] { @"D:\Windows-old\System32\svchost.exe", @"D:\Windows\Temp\svchost.exe",
    @"D:\Windows\System32\..\System32\svchost.exe", @"D:\Windows\System32\fake.exe", @"E:\Windows\System32\svchost.exe" })
    Check(WindowsComponentCatalog.Match(Row(120, "svchost.exe", path), win) is null, "wrong path or lookalike excluded: " + path);
Check(WindowsComponentCatalog.Match(Row(12, "SVCHOST.EXE", @"d:/windows/system32/SVCHOST.EXE"), win)?.Id == "svchost",
    "case and separator normalization keep the same expected path");
Check(WindowsComponentCatalog.Match(Row(12, "explorer.exe", win + @"\explorer.exe"), win)?.Id == "explorer" &&
    WindowsComponentCatalog.Match(Row(12, "explorer.exe", win + @"\System32\explorer.exe"), win) is null,
    "Explorer requires Windows root instead of a generic System32 match");
Check(WindowsComponentCatalog.Match(Row(12, "Memory Compression.exe"), win) is null &&
    WindowsComponentCatalog.Match(Row(12, "svchost.exe.extra"), win) is null &&
    WindowsComponentCatalog.Match(Row(12, "unknown.exe"), win) is null,
    "suffixes and unknown names never enter the system component catalog");
Check(WindowsComponentCatalog.Match(Row(0, "System Idle Process"), win)?.Id == "idle" &&
    WindowsComponentCatalog.Match(Row(4, "System"), win)?.Id == "system",
    "native reserved entries retained without executable metadata");
var a = Row(12, "svchost.exe", win + @"\System32\svchost.exe", 10 * 1048576);
var b = Row(112, "svchost.exe", null, 20 * 1048576);
var c = Row(512, "svchost.exe", win + @"\System32\svchost.exe", null);
var memory = new SystemMemorySnapshot(null, null, null, null, null, null, "original source error");
var source = new SystemProcessCatalog(DateTime.UtcNow, true, true, "synthetic source", "original partial reason",
    new[] { a, b, c, Row(9, "Memory Compression"), Row(10, "chrome.exe", win + @"\System32\chrome.exe") }, memory);
var retained = WindowsComponentCatalog.Retain(source, win);
Check(retained.Processes.Count == 4 && source.Processes.Count == 5 && retained.Processes.All(p => p.Name != "chrome.exe") &&
    retained.CapturedUtc == source.CapturedUtc && retained.Memory == memory && retained.Partial && retained.Note == source.Note,
    "retain discards application rows and preserves source counters errors and original snapshot");
var groups = WindowsComponentCatalog.Groups(retained.Processes, win);
var host = groups.Single(g => g.Definition.Id == "svchost");
Check(groups.Count == 2 && host.Count == 3 && host.Instances.Select(p => p.Pid).SequenceEqual(new[] { 12, 112, 512 }),
    "many instances grouped once without losing PID identities");
Check(host.MaximumWorkingSet == 20 * 1048576 && host.MemoryCount == 2 && host.Identity.Contains("N/D"),
    "maximum available resident value is not the sum and unavailable identity remains explicit");
Check(WindowsComponentCatalog.Groups(retained.Processes, win, search: "12").Single().Instances.Single().Pid == 12,
    "numeric search matches exact PID rather than 112 or 512");
Check(WindowsComponentCatalog.Groups(retained.Processes, win, search: "compression").Single().Definition.Id == "compression" &&
    WindowsComponentCatalog.Groups(retained.Processes, win, search: "Host condiviso").Single().Definition.Id == "svchost",
    "name and role search both remain within the curated component set");
Check(WindowsComponentCatalog.Groups(retained.Processes, win, "Kernel e memoria").Single().Definition.Id == "compression" &&
    WindowsComponentCatalog.Groups(retained.Processes, win, "Servizi").Single().Definition.Id == "svchost" &&
    WindowsComponentCatalog.Groups(retained.Processes, win, "Sicurezza").Count == 0,
    "categories filter by component function without an all-applications escape");
foreach (string culture in new[] { "it-IT", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    Check(host.Resident == (culture == "it-IT" ? "20,0 MiB" : "20.0 MiB"), "rendered maximum uses current decimal culture " + culture);
    var missing = new WindowsComponentGroup(host.Definition, new[] { c with { WorkingSetBytes = -1 } });
    var zero = new WindowsComponentGroup(host.Definition, new[] { c with { WorkingSetBytes = 0 } });
    Check(missing.Resident == "N/D" && zero.MaximumWorkingSet == 0 && zero.MemoryCount == 1,
        "missing or invalid memory cannot become measured zero " + culture);
    string detail = WindowsComponentCatalog.Details(host, b with { StartUtc = null }, win);
    Check(detail.Contains("origine non verificata") && detail.Contains("non verifica") && detail.Contains("non la somma") &&
        detail.Contains("non registra eventi") && detail.Contains("Guardian") && detail.Contains("manca l'ora di avvio") &&
        detail.Contains("PID 112") && detail.Contains(b.Access), "detail separates catalog role from observed identity memory and events " + culture);
}
var bulk = Enumerable.Range(1000, 5000).Select(i => Row(i, "app.exe", win + @"\app.exe"))
    .Concat(Enumerable.Range(7000, 200).Select(i => a with { Pid = i })).ToArray();
var compact = WindowsComponentCatalog.Retain(source with { Processes = bulk }, win);
Check(compact.Processes.Count == 200 && WindowsComponentCatalog.Groups(compact.Processes, win).Count == 1,
    "large application inventory is not retained and 200 service hosts yield one UI row");
Check(WindowsComponentCatalog.Groups(Array.Empty<SystemProcessEntry>(), win).Count == 0 &&
    !WindowsComponentCatalog.Retain(source with { Available = false }, win).Available,
    "empty or failed samples cannot fabricate observed components");
Console.WriteLine("Windows component tests complete; synthetic data only, no Windows query or live RAM measurement.");

var coverageSource = source with { Partial = false, Processes = new[] {
    Row(0, "System Idle Process") with { StartUtc = null }, a, b with { StartUtc = null, WorkingSetBytes = null },
    Row(20, "audiodg.exe", win + @"\System32\audiodg.exe"),
    Row(21, "WmiPrvSE.exe", win + @"\SysWOW64\wbem\WmiPrvSE.exe"),
    Row(22, "chrome.exe", @"D:\Apps\chrome.exe"),
    Row(23, "new-windows-component.exe", win + @"\System32\new-windows-component.exe"),
    Row(24, "unknown.exe"), Row(25, "svchost.exe", @"D:\Users\fake\svchost.exe")
} };
var expanded = WindowsComponentCatalog.Retain(coverageSource, win);
var coverage = expanded.ComponentCoverage!;
Check(coverage.Returned == 9 && coverage.Included == 5 && coverage.Groups == 4 && coverage.Excluded == 4 &&
    coverage.OtherWindowsPath == 1 && coverage.OtherPath == 1 && coverage.UnknownPath == 1 && coverage.UnexpectedPath == 1,
    "R22 coverage partitions original enumeration without calling excluded entries applications or threats");
Check(coverage.Included + coverage.Excluded == coverage.Returned && expanded.Processes.Count == coverage.Included &&
    coverageSource.Processes.Count == 9 && coverageSource.ComponentCoverage is null,
    "R22 source total survives compact retention without mutating or retaining excluded rows");
Check(coverage.IncludedWithoutPath == 1 && coverage.IncludedWithoutStart == 2 && coverage.IncludedWithoutMemory == 4,
    "R22 unreadable details stay visible and reserved PID does not inflate missing executable paths");
Check(ReferenceEquals(WindowsComponentCatalog.Retain(expanded, win), expanded),
    "R22 repeated retention does not silently replace enumeration total with catalog total");
foreach (string id in new[] { "ctfmon", "dllhost", "wmiprvse", "searchprotocolhost", "searchfilterhost" })
{
    var d = WindowsComponentCatalog.Definitions.Single(x => x.Id == id);
    string path32 = win + @"\SysWOW64\" + d.RelativePath![9..];
    Check(WindowsComponentCatalog.Match(Row(30, d.Name, path32), win)?.Id == id,
        "R22 explicit 32-bit host path: " + id);
    Check(WindowsComponentCatalog.Match(Row(30, d.Name, win + @"\Temp\" + d.Name), win) is null,
        "R22 added host cannot match a temporary directory: " + id);
}
foreach (string culture in new[] { "it-IT", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    var all = WindowsComponentCatalog.Groups(expanded.Processes, win);
    var selected = WindowsComponentCatalog.Groups(expanded.Processes, win, search: "12");
    string full = WindowsComponentCatalog.SampleStatus(expanded, all, false);
    string filtered = WindowsComponentCatalog.SampleStatus(expanded, selected, true);
    Check(full.Contains("gruppi mostrati: 4 · istanze mostrate: 5") && full.Contains("senza filtri") &&
        filtered.Contains("gruppi mostrati: 1 · istanze mostrate: 1") && filtered.Contains("filtri attivi") &&
        WindowsComponentCatalog.CoverageReport(expanded).Contains("fonte: 9"),
        "R22 visible counts change with exact PID filter while source total stays unchanged " + culture);
    Check(WindowsComponentCatalog.SampleStatus(expanded with { Partial = true }, all, false).Contains("LETTURA PARZIALE") &&
        !WindowsComponentCatalog.SampleStatus(expanded with { Partial = true }, all, false).Contains("completata"),
        "R22 alternative or partial enumeration cannot claim completion " + culture);
    Check(WindowsComponentCatalog.CoverageReport(expanded with { Available = false }).Contains("N/D") &&
        !WindowsComponentCatalog.CoverageReport(expanded with { Available = false }).Contains("fonte: 0") &&
        WindowsComponentCatalog.SampleStatus(expanded with { Available = false }, all, false).Contains("N/D"),
        "R22 unavailable enumeration never becomes a valid zero count " + culture);
}
Check(compact.ComponentCoverage is { Returned: 5200, Included: 200, OtherWindowsPath: 5000, Groups: 1 },
    "R22 large sample retains scalar exclusion counts and only Windows host rows");
Console.WriteLine("R22 coverage and expanded Windows catalog regressions complete.");
