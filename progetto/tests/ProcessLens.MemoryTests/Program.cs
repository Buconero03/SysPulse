using System.Globalization;
using ProcessLens.Core;

static void Test(
    string label,
    ProcessSnapshot process,
    string expectedLabel,
    double? expectedMb,
    string? explanationToken = null)
{
    // Formatting follows the user's Windows locale, as it should in the UI.
    // Build expected text using the same culture rather than hardcoding a dot.
    string expectedText = expectedMb.HasValue ? $"{expectedMb.Value:F1} MiB" : "N/D";
    bool numericMismatch = expectedMb.HasValue &&
        Math.Abs(process.MemoryDisplayMb - expectedMb.Value) > 0.000001d;

    if (process.MemoryDisplayLabel != expectedLabel ||
        process.MemoryDisplayText != expectedText ||
        numericMismatch ||
        (explanationToken is not null &&
         !process.MemoryDescription.Contains(explanationToken, StringComparison.Ordinal)))
    {
        throw new Exception(
            $"FAIL {label} [{CultureInfo.CurrentCulture.Name}]: " +
            $"label='{process.MemoryDisplayLabel}' expected='{expectedLabel}', " +
            $"text='{process.MemoryDisplayText}' expected='{expectedText}', " +
            $"numeric={process.MemoryDisplayMb}, expected={expectedMb}, " +
            $"description='{process.MemoryDescription}'");
    }
    Console.WriteLine($"PASS {label}");
}

const long MiB = 1024L * 1024L;
var baseline = new ProcessSnapshot { Pid = 100, ParentPid = 1, Name = "example.exe" };
var privateResident = baseline with
{
    WorkingSetBytes = 151 * MiB, WorkingSetAvailable = true,
    PrivateWorkingSetBytes = 26 * MiB, PrivateWorkingSetAvailable = true,
    PrivateBytes = 200 * MiB, PrivateCommitAvailable = true
};
var residentFallback = baseline with
{
    WorkingSetBytes = 151 * MiB, WorkingSetAvailable = true,
    PrivateBytes = 200 * MiB, PrivateCommitAvailable = true
};
var genuineZero = baseline with
{
    PrivateWorkingSetAvailable = true, WorkingSetAvailable = true
};

// Regression test for Italian Windows (comma) and English Windows (period).
// A localized numeric string must not make deterministic memory tests fail.
CultureInfo originalCulture = CultureInfo.CurrentCulture;
try
{
    foreach (string cultureName in new[] { "it-IT", "en-US" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        Console.WriteLine($"Memory tests: {CultureInfo.CurrentCulture.Name} " +
            $"(sample: {26.0:F1} MiB)");

        Test("private resident takes precedence over total resident",
            privateResident, "WORKING SET PRIVATO", 26.0, "Pagine private presenti in RAM");
        Test("fallback clearly labelled total resident",
            residentFallback, "WORKING SET TOTALE", 151.0, "NON confrontare");
        Test("protected or unavailable process does not become zero",
            baseline, "RAM N/D", null);
        Test("zero genuinely sampled",
            genuineZero, "WORKING SET PRIVATO", 0.0);
    }
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
}

Console.WriteLine("Memory semantics tests complete (it-IT + en-US).");
// The explanatory guide uses the same existing counters; no missing metric becomes zero.
foreach (string cultureName in new[] { "it-IT", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
    try
    {
        string text = ProcessMemoryGuide.DescribeCurrentValues(privateResident);
        if (!text.Contains("PID 100", StringComparison.Ordinal) ||
            !text.Contains($"{26.0:F1} MiB", StringComparison.Ordinal) ||
            !text.Contains($"{151.0:F1} MiB", StringComparison.Ordinal) ||
            !text.Contains($"{200.0:F1} MiB", StringComparison.Ordinal) ||
            !text.Contains("non sommarli", StringComparison.Ordinal) ||
            !text.Contains("contatori diversi", StringComparison.Ordinal))
            throw new Exception("FAIL memory guide counters and semantics [" + cultureName + "]");

        string noData = ProcessMemoryGuide.DescribeCurrentValues(baseline);
        if (!noData.Contains("Working Set privato: N/D", StringComparison.Ordinal) ||
            !noData.Contains("totale: N/D", StringComparison.Ordinal) ||
            !noData.Contains("privato: N/D", StringComparison.Ordinal) ||
            !noData.Contains("non equivale a zero", StringComparison.Ordinal))
            throw new Exception("FAIL memory guide missing metric not zero [" + cultureName + "]");
        Console.WriteLine("PASS memory guide existing counters and missing values [" + cultureName + "]");
    }
    finally { CultureInfo.CurrentCulture = originalCulture; }
}


// Deterministic trend rules: samples of the SAME metric and verified lifetime.
DateTime t = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
var sequence = new MemoryObservation[]
{
    new(t, 100 * MiB),
    new(t.AddSeconds(10), 110 * MiB),
    new(t.AddSeconds(25), 125 * MiB)
};
var trend = MemoryTrendAnalyzer.Analyze("Working Set privato", sequence)
    ?? throw new Exception("Trend unexpectedly null");
if (!trend.HasTrend || trend.Samples != 3 || trend.Seconds != 25 ||
    Math.Abs(trend.DeltaMb - 25) > .00001 || trend.PeakMb != 125 || trend.MinimumMb != 100)
    throw new Exception("FAIL memory trend: delta, interval, peak or count");
Console.WriteLine("PASS memory trend 25s, +25 MiB");
var one = MemoryTrendAnalyzer.Analyze("RAM", sequence.Take(1).ToArray());
if (one is null || one.HasTrend) throw new Exception("FAIL one sample cannot indicate growth");
Console.WriteLine("PASS one-sample trend insufficient");
if (MemoryTrendAnalyzer.Analyze("RAM", Array.Empty<MemoryObservation>()) is not null)
    throw new Exception("FAIL empty samples");
Console.WriteLine("PASS missing sample is not zero");
// VM allocation types are not physical RAM, so don't add their values to resident memory.
var map = new MemoryMapSnapshot(true, "", 300 * MiB, 200 * MiB, 50 * MiB, 0, 500, false);
if (map.PrivateMb != 300 || map.ImageMb != 200 || map.MappedMb != 50 ||
    privateResident.MemoryDisplayMb != 26)
    throw new Exception("FAIL virtual region counters contaminated physical RAM reading");
Console.WriteLine("PASS virtual region totals distinct from physical RAM");

// User query regression: memory intent must not be triggered by "programma",
// and the diagnosis must separate physical resident RAM from virtual commit.
var insight = new ProcessInsight
{
    FriendlyName = "Language server", Category = "Sviluppo", OwnerApp = "Editor",
    Summary = "Analizza codice per un editor o IDE", Role = "Language server",
    WhatIsIt = "Analizza codice", WhyRunning = "Componente editor",
    CloseImpact = "Servizio editor interrotto", CloseRisk = "BASSO / MEDIO",
    DetectionLabel = "CERTO", DetectionBasis = "Process role", ConfidencePercent = 90
};
var queryEngine = new ProcessLens.Semantics.ProcessQueryEngine();
var answer = queryEngine.Ask(privateResident with { Name = "language_server.exe", CpuSampleAvailable = true },
    insight, "Perché usa così tanta RAM?", null, null, trend, map);
if (!answer.Answer.Contains("Working Set privato", StringComparison.Ordinal) ||
    !answer.Answer.Contains("Non devono essere sommati ai MiB residenti", StringComparison.OrdinalIgnoreCase) ||
    !answer.Answer.Contains("language server", StringComparison.OrdinalIgnoreCase) ||
    !answer.Answer.Contains("Un solo intervallo non basta per diagnosticare un memory leak", StringComparison.OrdinalIgnoreCase))
    throw new Exception("FAIL memory explanation evidence/limitations");
Console.WriteLine("PASS memory question: measured vs inferred, no invented internal objects");
var other = queryEngine.Ask(privateResident, insight, "Che cosa fa questo programma?", null);
if (other.Answer.Contains("REGIONI VIRTUALI IMPEGNATE", StringComparison.Ordinal))
    throw new Exception("FAIL false memory-intent on programma");
Console.WriteLine("PASS RAM term boundary does not match programma");

// Working-set flags are deterministic and must never be confused with committed bytes.
if (WorkingSetPageFlags.IsValid(0) || WorkingSetPageFlags.IsShared(1) ||
    !WorkingSetPageFlags.IsValid(1) ||
    !WorkingSetPageFlags.IsShared(1UL | (1UL << 15)))
    throw new Exception("FAIL QueryWorkingSetEx flag decoding");
Console.WriteLine("PASS QueryWorkingSetEx valid/shared page-flag interpretation");
var allocationReport = new WindowsAllocationReport(true, "", 12, 3, 128 * MiB,
    4, 2, 1, true, false,
    new [] { new WindowsAllocationGroup(0x1000, "MEM_PRIVATE", 128 * MiB, 4, 4, 2, 1) });
// Verify the actual evidence boundaries, not one fragile sentence fragment.
// The report must separate committed VIRTUAL bytes, sampled page residency,
// and the absence of runtime object attribution. Test both decimal locales.
foreach (string cultureName in new[] { "it-IT", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
    try
    {
        string evidence = allocationReport.ToEvidenceText();
        string committed = (128.0d).ToString("F1", CultureInfo.CurrentCulture);
        if (!evidence.Contains("pagine campionate", StringComparison.Ordinal) ||
            !evidence.Contains("Residenti:", StringComparison.OrdinalIgnoreCase) ||
            !evidence.Contains("MiB virtuali impegnati", StringComparison.OrdinalIgnoreCase) ||
            !evidence.Contains("Oggetti e variabili non identificati", StringComparison.OrdinalIgnoreCase) ||
            !evidence.Contains("non equivalgono alla RAM fisica", StringComparison.OrdinalIgnoreCase) ||
            !evidence.Contains(committed + " MiB", StringComparison.Ordinal))
        {
            throw new Exception("FAIL allocation report evidence [" + cultureName + "]: " + evidence);
        }
    }
    finally { CultureInfo.CurrentCulture = originalCulture; }
}
var unavailableReport = WindowsAllocationReport.Unavailable("Accesso negato.").ToEvidenceText();
if (!unavailableReport.Contains("Accesso negato.", StringComparison.Ordinal) ||
    !unavailableReport.Contains("Cause e dimensioni degli oggetti: N/D", StringComparison.Ordinal))
    throw new Exception("FAIL unavailable allocation evidence must not invent measurements");
Console.WriteLine("PASS allocation report labels sampled working set versus committed space (it-IT/en-US)");


// v0.19.5: UI classification is visual feedback only, never a leak/paging diagnosis.
foreach (var item in new (double Percent, string Label, string Color)[]
{
    (0.0, "Utilizzo RAM sotto soglia di attenzione", "#047857"),
    (74.9, "Utilizzo RAM sotto soglia di attenzione", "#047857"),
    (75.0, "Utilizzo RAM da osservare", "#A16207"),
    (84.9, "Utilizzo RAM da osservare", "#A16207"),
    (85.0, "Utilizzo RAM elevato", "#B45309"),
    (89.9, "Utilizzo RAM elevato", "#B45309"),
    (90.0, "RAM quasi satura", "#B91C1C"),
    (93.3, "RAM quasi satura", "#B91C1C"),
    (100.0, "RAM quasi satura", "#B91C1C"),
    (double.NaN, "Stato RAM non disponibile", "#475569")
})
{
    var ui = MemoryDashboardPresentation.ForUsage(item.Percent);
    if (ui.Label != item.Label || ui.AccentHex != item.Color)
        throw new Exception($"FAIL RAM dashboard status {item.Percent}: {ui.Label} / {ui.AccentHex}");
}
if (!MemoryDashboardPresentation.ForUsage(93.3).Explanation.Contains("non dimostra", StringComparison.Ordinal))
    throw new Exception("FAIL RAM dashboard suggests paging is proven");
Console.WriteLine("PASS RAM dashboard levels, colors and evidence limits");

if (!MemoryDashboardPresentation.ForUsage(90).Explanation.StartsWith("Almeno il 90%") ||
    !MemoryDashboardPresentation.ForUsage(75).Explanation.StartsWith("Almeno il 75%"))
    throw new Exception("FAIL R7 threshold equality must match branch predicate");
if (!privateResident.MemoryDescription.Contains("stessa istanza") ||
    !privateResident.MemoryDescription.Contains("aggregazioni diverse"))
    throw new Exception("FAIL R7 Task Manager comparison must specify same counter and instance");
Console.WriteLine("PASS R7 memory threshold boundaries and counter comparison limits");
