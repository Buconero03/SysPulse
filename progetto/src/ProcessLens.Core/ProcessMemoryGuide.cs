namespace ProcessLens.Core;

/// <summary>
/// Formats existing Windows counters without sampling, estimating or equating
/// private committed memory with resident physical memory.
/// </summary>
public static class ProcessMemoryGuide
{
    public static string DescribeCurrentValues(ProcessSnapshot process)
    {
        string privateResident = process.PrivateWorkingSetAvailable
            ? $"{process.PrivateWorkingSetMb:F1} MiB" : "N/D";
        string residentTotal = process.WorkingSetAvailable
            ? $"{process.WorkingSetMb:F1} MiB" : "N/D";
        string privateCommit = process.PrivateCommitAvailable
            ? $"{process.PrivateMb:F1} MiB" : "N/D";

        return $"PID {process.Pid}\n• Working Set privato: {privateResident}\n" +
               $"• Working Set totale: {residentTotal}\n" +
               $"• Commit privato: {privateCommit}\n" +
               "Il totale include il privato: non sommarli. " +
               "Commit e RAM fisica sono contatori diversi. " +
               "N/D: dato non disponibile; non equivale a zero.";
    }
}
