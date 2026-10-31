namespace ProcessLens.Core;

/// <summary>
/// Small, deterministic hysteresis for local physical RAM alerts.
/// The agent samples memory periodically; there is one alert at >= High,
/// and it cannot repeat until a subsequent reading is strictly below Reset.
/// </summary>
public sealed class MemoryAlertPolicy
{
    public double HighPercent { get; }
    public double ResetPercent { get; }
    public bool Armed { get; private set; } = true;

    public MemoryAlertPolicy(double highPercent = 85, double resetPercent = 80)
    {
        if (!double.IsFinite(highPercent) || !double.IsFinite(resetPercent) ||
            resetPercent < 0 || resetPercent >= highPercent || highPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(highPercent),
                "Require 0 <= reset < high <= 100.");
        HighPercent = highPercent;
        ResetPercent = resetPercent;
    }

    /// <returns>True for a single alert-worthy crossing, including start above threshold.</returns>
    public bool Observe(double usedPercent)
    {
        if (!double.IsFinite(usedPercent) || usedPercent < 0 || usedPercent > 100)
            return false;
        if (usedPercent < ResetPercent)
            Armed = true;
        if (Armed && usedPercent >= HighPercent)
        {
            Armed = false;
            return true;
        }
        return false;
    }
}
