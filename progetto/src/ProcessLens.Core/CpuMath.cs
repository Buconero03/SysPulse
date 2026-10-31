namespace ProcessLens.Core;

/// <summary>Pure CPU accounting formulas used by the Windows sampler and unit tests.</summary>
public static class CpuMath
{
    // GetSystemTimes reports aggregate 100ns intervals across all logical CPUs.
    public static double ProcessPercentOfMachine(long processCpuDeltaTicks, ulong systemCpuDeltaTicks)
    {
        if (processCpuDeltaTicks < 0 || systemCpuDeltaTicks == 0) return 0;
        return Math.Clamp(100d * processCpuDeltaTicks / systemCpuDeltaTicks, 0, 100);
    }

    public static double MachineBusyPercent(ulong totalDelta, ulong idleDelta)
    {
        if (totalDelta == 0 || idleDelta > totalDelta) return 0;
        return Math.Clamp(100d * (totalDelta - idleDelta) / totalDelta, 0, 100);
    }
}
