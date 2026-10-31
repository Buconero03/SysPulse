using System.Globalization;
using System.Text.RegularExpressions;

namespace ProcessLens.Core;

/// <summary>A bounded, evidence-only IPC result. Never contains object contents or raw commands.</summary>
public sealed record HeapTypeRow(string TypeName, long Bytes, long Count);
public sealed record ProfilerResult
{
    public string Status { get; init; } = "error";
    public int Pid { get; init; }
    public string Runtime { get; init; } = "non identificato";
    public string Adapter { get; init; } = "";
    public string Detail { get; init; } = "";
    public DateTimeOffset CapturedUtc { get; init; } = DateTimeOffset.UtcNow;
    public long ManagedHeapBytes { get; init; }
    public long ManagedObjects { get; init; }
    public IReadOnlyList<HeapTypeRow> TopTypes { get; init; } = Array.Empty<HeapTypeRow>();
    public bool Complete { get; init; }
    public string ReasonCode { get; init; } = "";
    public string OperationId { get; init; } = "";
    public int? ToolExitCode { get; init; }
    public long? CaptureDurationMs { get; init; }
    public long? ReportDurationMs { get; init; }
    public bool? TemporaryFilesRemoved { get; init; }
    public string Evidence { get; init; } = "";
}

/// <summary>Parse only dotnet-gcdump heapstat columns; never parse arbitrary target object contents.</summary>
public static class GcDumpHeapStatParser
{
    private static readonly Regex Row = new(@"^\s*([\d,]+)\s+([\d,]+)\s+(.+?)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TotalBytes = new(@"^\s*([\d,]+)\s+GC Heap bytes\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex TotalObjects = new(@"^\s*([\d,]+)\s+GC Heap objects\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    // Older gcdump: 'Object Bytes  Count  Type'; version 10 documentation: 'Size (Bytes) Count Type'.
    private static readonly Regex TableHeader = new(@"^\s*(?:Object Bytes|Size \(Bytes\))\s+Count\s+Type\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static (long HeapBytes, long HeapObjects, IReadOnlyList<HeapTypeRow> TopTypes) Parse(string text, int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        long totalBytes = 0, totalObjects = 0;
        long rowBytes = 0, rowObjects = 0;
        bool hasByteTotal = false, hasObjectTotal = false;
        var types = new List<HeapTypeRow>();
        bool headerSeen = false;
        foreach (string line in text.Split('\n'))
        {
            if (TotalBytes.Match(line) is { Success: true } bm && TryNumber(bm.Groups[1].Value, out long b))
            { totalBytes = b; hasByteTotal = true; }
            if (TotalObjects.Match(line) is { Success: true } om && TryNumber(om.Groups[1].Value, out long o))
            { totalObjects = o; hasObjectTotal = true; }
            if (TableHeader.IsMatch(line)) { headerSeen = true; continue; }
            if (!headerSeen) continue;
            var match = Row.Match(line);
            if (!match.Success || !TryNumber(match.Groups[1].Value, out long bytes) ||
                !TryNumber(match.Groups[2].Value, out long count) || bytes < 0 || count < 0) continue;
            // Strip assembly suffix, not part of CLR type identity.
            string type = Regex.Replace(match.Groups[3].Value, @"\s+\[[^\]]+\]\s*$", "").Trim();
            if (type.Length == 0) continue;
            try { checked { rowBytes += bytes; rowObjects += count; } }
            catch (OverflowException) { return (0, 0, Array.Empty<HeapTypeRow>()); }
            types.Add(new HeapTypeRow(type.Length > 180 ? type[..180] : type, bytes, count));
        }
        // gcdump v10's documented report has no explicit heap totals. Use ALL parsed rows,
        // never only the top-N selection. The worker rejects truncated stdout separately.
        if (!hasByteTotal) totalBytes = rowBytes;
        if (!hasObjectTotal) totalObjects = rowObjects;
        return (totalBytes, totalObjects, types.OrderByDescending(x => x.Bytes).Take(limit).ToArray());
    }
    private static bool TryNumber(string s, out long number) => long.TryParse(s.Replace(",", ""),
        NumberStyles.None, CultureInfo.InvariantCulture, out number);
}

public static class ProfilerPolicy
{
    public const int MaxSeconds = 45;
    public const int MaxHelperWorkingSetMiB = 256;
    public const int MaxGcDumpFileMiB = 128;
    public static bool SafeToCollect(double totalRamUsedPercent) => totalRamUsedPercent >= 0 && totalRamUsedPercent < 85.0;
}
