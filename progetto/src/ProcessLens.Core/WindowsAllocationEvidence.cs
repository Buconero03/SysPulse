namespace ProcessLens.Core;

/// <summary>
/// QueryWorkingSetEx probes pages from selected Windows allocation groups only.
/// A sample is NOT an exact resident byte count and not an object / heap profiler.
/// </summary>
public static class WorkingSetPageFlags
{
    // PSAPI_WORKING_SET_EX_BLOCK: Valid at bit 0, Shared at bit 15
    // for valid pages; ShareCount is not a byte count.
    public static bool IsValid(ulong flags) => (flags & 1UL) != 0;
    public static bool IsShared(ulong flags) => IsValid(flags) && (flags & (1UL << 15)) != 0;
}

public sealed record WindowsAllocationGroup(
    ulong AllocationBase, string Kind, long CommittedBytes, int RegionCount,
    int SampledPages, int ResidentSampledPages, int SharedResidentSampledPages,
    string? ModuleOrPurpose = null)
{
    public double CommittedMiB => CommittedBytes / 1048576d;
    public int PrivateResidentSampledPages => Math.Max(0, ResidentSampledPages - SharedResidentSampledPages);
}

public sealed record WindowsAllocationReport(
    bool Available, string Note, int EnumeratedRegions, int CommittedGroups,
    long CommittedBytes, int SampledPages, int ResidentSampledPages,
    int SharedResidentSampledPages, bool SamplesAvailable, bool Truncated,
    IReadOnlyList<WindowsAllocationGroup> LargestGroups,
    long PeakWorkingSetBytes = 0, long PeakCommitBytes = 0,
    uint PageFaultCount = 0, long PagedPoolBytes = 0, long NonPagedPoolBytes = 0)
{
    public static WindowsAllocationReport Unavailable(string note) =>
        new(false, note, 0, 0, 0, 0, 0, 0, false, false,
            Array.Empty<WindowsAllocationGroup>());

    public string ToEvidenceText()
    {
        if (!Available) return "■ ANALISI DELLE ALLOCAZIONI WINDOWS\n" + Note +
            " Cause e dimensioni degli oggetti: N/D.";
        string limit = Truncated ? "Scansione parziale: limite di tempo o regioni." :
            "Scansione completata.";
        var result = new System.Text.StringBuilder();
        result.AppendLine("■ ALLOCAZIONI WINDOWS · REGIONI E PAGINE");
        result.AppendLine($"VirtualQueryEx: {EnumeratedRegions} regioni interrogate; " +
            $"{CommittedGroups} gruppi di allocazione impegnati; " +
            $"{CommittedBytes / 1048576d:F1} MiB virtuali impegnati. {limit}");

        if (PeakWorkingSetBytes > 0 || PeakCommitBytes > 0 || PagedPoolBytes > 0 || NonPagedPoolBytes > 0)
        {
            result.AppendLine($"PSAPI EX2: " +
                $"Picco Working Set totale {PeakWorkingSetBytes / 1048576d:F1} MiB · " +
                $"Picco commit {PeakCommitBytes / 1048576d:F1} MiB · " +
                $"Page fault {PageFaultCount:N0} · " +
                $"Pool kernel paginabile {PagedPoolBytes / 1048576d:F2} MiB · " +
                $"Pool kernel non paginabile {NonPagedPoolBytes / 1048576d:F2} MiB.");
        }

        if (SamplesAvailable && SampledPages > 0)
        {
            int privatePages = Math.Max(0, ResidentSampledPages - SharedResidentSampledPages);
            double residentPct = 100.0 * ResidentSampledPages / SampledPages;
            result.AppendLine($"QueryWorkingSetEx: {SampledPages} pagine campionate nei blocchi principali.\n" +
                $"• Residenti: {ResidentSampledPages} · {residentPct:F0} % del campione.\n" +
                $"• Private: {privatePages} · condivisibili con flag Shared: {SharedResidentSampledPages}.\n" +
                "Campione parziale: non misura la RAM totale del processo.");
        }
        else
            result.AppendLine("QueryWorkingSetEx: campione N/D; residenza non verificata.");

        result.AppendLine("Blocchi principali · ordinati per commit:");
        foreach (var group in LargestGroups.Take(8))
        {
            string purpose = !string.IsNullOrWhiteSpace(group.ModuleOrPurpose)
                ? $" [{group.ModuleOrPurpose}]"
                : string.Empty;
            string residency = group.SampledPages > 0 && SamplesAvailable
                ? $"\n  Campione: {group.ResidentSampledPages}/{group.SampledPages} pagine residenti; private={group.PrivateResidentSampledPages}, condivisibili={group.SharedResidentSampledPages}"
                : string.Empty;
            result.AppendLine($"• {group.Kind}{purpose}, base 0x{group.AllocationBase:X}, " +
                $"{group.CommittedMiB:F1} MiB di commit · {group.RegionCount} regioni.{residency}");
        }
        result.Append("Guida tecnica: MEM_PRIVATE = memoria privata; MEM_IMAGE = eseguibili e DLL; MEM_MAPPED = file mappati o sezioni condivise. " +
            "Oggetti e variabili non identificati: per .NET usa Profilazione heap .NET. " +
            "Le pagine campionate non equivalgono alla RAM fisica totale del processo.");
        return result.ToString();
    }
}
