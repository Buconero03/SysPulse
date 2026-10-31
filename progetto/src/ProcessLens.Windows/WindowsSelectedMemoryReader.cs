using System.Diagnostics;
using ProcessLens.Core;

namespace ProcessLens.Windows;

/// <summary>
/// On-demand measurement of the selected PID, with identity validation before/after.
/// Uses Windows process counters only; it cannot identify heap objects or cache owners.
/// </summary>
public static class WindowsSelectedMemoryReader
{
    public sealed record Result(bool Verified, ProcessSnapshot Snapshot, string Note);

    public static Result Refresh(ProcessSnapshot original)
    {
        if (original.Pid <= 4 || !original.StartTime.HasValue)
            return new(false, original, "PID o orario di creazione non verificabile: lettura annullata.");
        try
        {
            using var process = Process.GetProcessById(original.Pid);
            process.Refresh();
            if (process.StartTime != original.StartTime.Value)
                return new(false, original, "PID riutilizzato o istanza cambiata: lettura annullata.");

            // Existing native PSAPI collector; no injection, no memory-content reads.
            var sample = WindowsMemorySampler.Capture(original.Pid);
            long total = 0, privateCommit = 0;
            bool totalAvailable = false, commitAvailable = false;
            try { total = process.WorkingSet64; totalAvailable = true; } catch { }
            try { privateCommit = process.PrivateMemorySize64; commitAvailable = true; } catch { }
            process.Refresh();
            if (process.HasExited || process.StartTime != original.StartTime.Value)
                return new(false, original, "Processo terminato o identita' cambiata durante la lettura.");

            var updated = original with
            {
                WorkingSetBytes = sample.WorkingSetAvailable ? sample.WorkingSetBytes : total,
                WorkingSetAvailable = sample.WorkingSetAvailable || totalAvailable,
                PrivateWorkingSetBytes = sample.PrivateWorkingSetBytes,
                PrivateWorkingSetAvailable = sample.PrivateWorkingSetAvailable,
                PrivateBytes = sample.PrivateCommitAvailable ? sample.PrivateCommitBytes : privateCommit,
                PrivateCommitAvailable = sample.PrivateCommitAvailable || commitAvailable
            };
            var hasResident = updated.PrivateWorkingSetAvailable || updated.WorkingSetAvailable;
            var hasCommit = updated.PrivateCommitAvailable;
            return new(true, updated,
                hasResident || hasCommit
                    ? $"Campione Windows del PID {original.Pid} acquisito; " +
                      $"RAM privata={(updated.PrivateWorkingSetAvailable ? "OK" : "N/D")}, " +
                      $"Working Set={(updated.WorkingSetAvailable ? "OK" : "N/D")}, " +
                      $"commit={(hasCommit ? "OK" : "N/D")}."
                    : "Identita' confermata; Windows non ha fornito contatori RAM accessibili.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return new(false, original, "Contatori non acquisiti: " + ex.GetType().Name + ".");
        }
    }
}
