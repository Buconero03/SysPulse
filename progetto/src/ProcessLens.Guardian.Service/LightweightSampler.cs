using System.Diagnostics;
using ProcessLens.Guardian.Core;

namespace ProcessLens.Guardian.Service;

public sealed class LightweightSampler
{
    private sealed record Cpu(TimeSpan Value, DateTime Taken, long StartTicks);
    private readonly Dictionary<int, Cpu> _cpu = new();
    public IReadOnlyList<ProcessObserved> Capture(DateTime utc)
    {
        var rows = new List<ProcessObserved>();
        var parents = ProcessTreeSampler.Parents();
        var present = new HashSet<int>();
        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                int pid;
                try { pid = proc.Id; } catch { continue; }
                present.Add(pid);
                string name = "?";
                long started = 0;
                TimeSpan? cpu = null;
                long? residentWorkingSet = null;
                long? privateCommit = null;
                try { name = proc.ProcessName; } catch { }
                try { started = proc.StartTime.ToUniversalTime().Ticks; } catch { }
                try { cpu = proc.TotalProcessorTime; } catch { }
                try { residentWorkingSet = proc.WorkingSet64; } catch { }
                try { privateCommit = proc.PrivateMemorySize64; } catch { }
                double? percent = null;
                if (cpu is { } current)
                {
                    if (_cpu.TryGetValue(pid, out var old) && (started == 0 || old.StartTicks == 0 || old.StartTicks == started))
                        percent = GuardianMath.ProcessCpuPercent(current, old.Value, (utc - old.Taken).TotalSeconds, Environment.ProcessorCount);
                    _cpu[pid] = new(current, utc, started);
                }
                // WorkingSet64 includes shared physical pages; never label it 'private resident'.
                rows.Add(new(pid, started, parents.GetValueOrDefault(pid), name, percent, residentWorkingSet, privateCommit));
            }
        }
        foreach (int pid in _cpu.Keys.Where(id => !present.Contains(id)).ToArray()) _cpu.Remove(pid);
        return rows;
    }
}
