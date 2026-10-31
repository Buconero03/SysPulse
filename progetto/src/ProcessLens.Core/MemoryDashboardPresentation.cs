namespace ProcessLens.Core;

/// <summary>
/// Etichette UI basate esclusivamente sulla percentuale di RAM fisica utilizzata.
/// Non prova paging, blocchi del sistema, cause o memory leak.
/// La soglia di attenzione (75%) e quella critica (90%) sono scelte visuali,
/// distinte dalla policy di notifica di MemoryAgent (85%/80%).
/// </summary>
public static class MemoryDashboardPresentation
{
    public sealed record State(string Label, string Explanation, string AccentHex, string BackgroundHex);

    public static State ForUsage(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent) || percent < 0 || percent > 100)
            return new("Stato RAM non disponibile", "La percentuale ricevuta non è valida.", "#475569", "#E2E8F0");
        if (percent >= 90)
            return new("RAM quasi satura", "Almeno il 90% della memoria fisica è in uso. Potrebbero verificarsi rallentamenti; questo dato da solo non dimostra attività di paging.", "#B91C1C", "#FEE2E2");
        if (percent >= 85)
            return new("Utilizzo RAM elevato", "L'uso della memoria fisica ha raggiunto la soglia degli avvisi (85%). Non è una diagnosi della causa.", "#B45309", "#FEF3C7");
        if (percent >= 75)
            return new("Utilizzo RAM da osservare", "Almeno il 75% della memoria fisica è utilizzato. Controlla l'andamento se il sistema rallenta.", "#A16207", "#FEF9C3");
        return new("Utilizzo RAM sotto soglia di attenzione", "Il livello di utilizzo è inferiore alla soglia visiva di attenzione (75%); non è una baseline del sistema.", "#047857", "#D1FAE5");
    }
}
