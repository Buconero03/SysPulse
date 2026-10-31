namespace ProcessLens.Core;

/// <summary>
/// Formats OS-provided process start times without inferring missing timestamps.
/// The elapsed time is measured against UTC to avoid daylight-saving jumps.
/// </summary>
public static class ProcessStartFormatter
{
    public static string FormatStart(DateTime? startTime)
    {
        if (startTime is not { } start)
            return SysPulseLocalization.IsEnglish ? "Unavailable" : "Non disponibile";

        // Process.StartTime is local. Some system/test sources report an
        // unspecified Kind; do not reinterpret their clock reading as UTC.
        DateTime local = start.Kind == DateTimeKind.Utc ? start.ToLocalTime() : start;
        return local.ToString("dd/MM/yyyy HH:mm:ss");
    }

    public static string FormatElapsed(DateTime? startTime, DateTime? nowUtc = null)
    {
        if (startTime is not { } start)
            return SysPulseLocalization.IsEnglish ? "Unavailable" : "Non disponibile";

        var elapsed = (nowUtc ?? DateTime.UtcNow).ToUniversalTime() - start.ToUniversalTime();
        if (elapsed < TimeSpan.Zero)
            return SysPulseLocalization.IsEnglish ? "Unavailable" : "Non disponibile";

        int days = (int)elapsed.TotalDays;
        string dayUnit = SysPulseLocalization.IsEnglish ? "d" : "g";
        if (days > 0)
            return $"{days} {dayUnit} {elapsed.Hours} h {elapsed.Minutes} min";
        if (elapsed.Hours > 0)
            return $"{elapsed.Hours} h {elapsed.Minutes} min";
        if (elapsed.Minutes > 0)
            return $"{elapsed.Minutes} min {elapsed.Seconds} s";
        return $"{elapsed.Seconds} s";
    }
}
