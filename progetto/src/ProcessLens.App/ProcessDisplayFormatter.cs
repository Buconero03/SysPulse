using System.Globalization;

namespace ProcessLens.App;

// Presentation only: the original counters and their availability flags are unchanged.
internal static class ProcessDisplayFormatter
{
    private const string UnitSpace = "\u00a0";

    public static string Percent(double value) => double.IsFinite(value) && value >= 0
        ? value.ToString("F1", CultureInfo.CurrentCulture) + UnitSpace + "%"
        : "N/D";

    public static string Seconds(double value) => double.IsFinite(value) && value >= 0
        ? value.ToString("F1", CultureInfo.CurrentCulture) + UnitSpace + "s"
        : "N/D";

    // Physical memory only. Invalid or missing readings are not measured zero.
    public static string SystemRam(ulong? totalBytes, ulong? availableBytes)
    {
        if (totalBytes is not > 0 || availableBytes is null || availableBytes > totalBytes)
            return "N/D";
        ulong used = totalBytes.Value - availableBytes.Value;
        const double gib = 1073741824d;
        return $"{(used / gib).ToString("F1", CultureInfo.CurrentCulture)} / " +
            $"{(totalBytes.Value / gib).ToString("F1", CultureInfo.CurrentCulture)}{UnitSpace}GiB · " +
            Percent(used * 100.0 / totalBytes.Value);
    }

    // Round the display to a millisecond and carry into minutes/hours/days.
    // A positive sub-millisecond reading must not appear as a measured zero.
    public static string CpuDuration(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 ||
            seconds >= TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond - 1)
            return "N/D";
        if (seconds == 0) return "0" + UnitSpace + "ms";
        if (seconds < 0.001) return "< 1" + UnitSpace + "ms";

        var duration = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000, MidpointRounding.AwayFromZero));
        if (duration.TotalSeconds < 1)
            return duration.Milliseconds.ToString(CultureInfo.CurrentCulture) + UnitSpace + "ms";

        var parts = new List<string>();
        if (duration.Days > 0) parts.Add(duration.Days + UnitSpace + "g");
        if (duration.Hours > 0) parts.Add(duration.Hours + UnitSpace + "h");
        if (duration.Minutes > 0) parts.Add(duration.Minutes + UnitSpace + "min");
        parts.Add((duration.Seconds + duration.Milliseconds / 1000d)
            .ToString("0.###", CultureInfo.CurrentCulture) + UnitSpace + "s");
        return string.Join(" ", parts);
    }
}
