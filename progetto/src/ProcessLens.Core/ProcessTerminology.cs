namespace ProcessLens.Core;

/// <summary>
/// Parole usate nella GUI per distinguere indizi, inferenze e misurazioni.
/// I valori 0..100 sono punteggi di regole deterministiche, NON probabilita'
/// calibrate, e non verificano l’attività istantanea del processo.
/// </summary>
public static class ProcessTerminology
{
    public static string EvidenceStrength(int ruleScore) =>
        SysPulseLocalization.CurrentLanguage == AppLanguage.English
            ? SysPulseLocalization.TerminologyEvidenceStrength(ruleScore, AppLanguage.English)
            : ruleScore switch
            {
                >= 95 => "Corrispondenza molto alta",
                >= 80 => "Corrispondenza alta",
                >= 60 => "Corrispondenza media",
                >= 40 => "Corrispondenza bassa",
                _ => "Corrispondenza insufficiente"
            };

    public static string FunctionLabel(int ruleScore) =>
        SysPulseLocalization.CurrentLanguage == AppLanguage.English
            ? SysPulseLocalization.TerminologyFunctionLabel(ruleScore, AppLanguage.English)
            : ruleScore switch
            {
                >= 95 => "Ruolo identificato",
                >= 80 => "Ruolo probabile",
                >= 60 => "Ruolo stimato",
                _ => "Funzione sconosciuta"
            };

    public static string EvidenceLevel(int ruleScore) =>
        SysPulseLocalization.CurrentLanguage == AppLanguage.English
            ? SysPulseLocalization.TerminologyEvidenceLevel(ruleScore, AppLanguage.English)
            : ruleScore switch
            {
                >= 95 => "molto alta",
                >= 80 => "alta",
                >= 60 => "intermedia",
                >= 40 => "limitata",
                _ => "insufficiente"
            };

    public static string CategoryLabel(string category) =>
        SysPulseLocalization.CurrentLanguage == AppLanguage.English
            ? SysPulseLocalization.TerminologyCategoryLabel(category, AppLanguage.English)
            : string.Equals(category, "Background", StringComparison.OrdinalIgnoreCase)
                ? "Secondario"
                : category;

    public static string EvidenceStrength(int ruleScore, AppLanguage language) =>
        SysPulseLocalization.TerminologyEvidenceStrength(ruleScore, language);

    public static string FunctionLabel(int ruleScore, AppLanguage language) =>
        SysPulseLocalization.TerminologyFunctionLabel(ruleScore, language);

    public static string EvidenceLevel(int ruleScore, AppLanguage language) =>
        SysPulseLocalization.TerminologyEvidenceLevel(ruleScore, language);

    public static string CategoryLabel(string category, AppLanguage language) =>
        SysPulseLocalization.TerminologyCategoryLabel(category, language);

    public const string ScoreExplanation =
        "Punteggio delle regole, non probabilità. Non misura l’attività attuale.";
}
