namespace ProcessLens.Core;

/// <summary>
/// Parole usate nella GUI per distinguere indizi, inferenze e misurazioni.
/// I valori 0..100 sono punteggi di regole deterministiche, NON probabilita'
/// calibrate, e non verificano l’attività istantanea del processo.
/// </summary>
public static class ProcessTerminology
{
    public static string EvidenceStrength(int ruleScore) => ruleScore switch
    {
        >= 95 => "Corrispondenza molto alta",
        >= 80 => "Corrispondenza alta",
        >= 60 => "Corrispondenza media",
        >= 40 => "Corrispondenza bassa",
        _ => "Corrispondenza insufficiente"
    };

    public static string FunctionLabel(int ruleScore) => ruleScore switch
    {
        >= 95 => "Ruolo identificato",
        >= 80 => "Ruolo probabile",
        >= 60 => "Ruolo stimato",
        _ => "Funzione sconosciuta"
    };

    public static string EvidenceLevel(int ruleScore) => ruleScore switch
    {
        >= 95 => "molto alta",
        >= 80 => "alta",
        >= 60 => "intermedia",
        >= 40 => "limitata",
        _ => "insufficiente"
    };

    public static string CategoryLabel(string category) =>
        string.Equals(category, "Background", StringComparison.OrdinalIgnoreCase)
            ? "Secondario"
            : category;

    public const string ScoreExplanation =
        "Punteggio delle regole, non probabilità. Non misura l’attività attuale.";
}
