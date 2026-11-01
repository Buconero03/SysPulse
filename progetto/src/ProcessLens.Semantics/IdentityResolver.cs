using ProcessLens.Core;

namespace ProcessLens.Semantics;

internal static class IdentityResolver
{
    public static IdentityAssessment Resolve(ProcessSnapshot p)
    {
        string exe = Path.GetFileNameWithoutExtension(p.Name);
        string? product = SysPulseLocalization.Raw(Clean(p.ProductName));
        string? description = SysPulseLocalization.Raw(Clean(p.FileDescription));
        string cmd = p.CommandLine ?? string.Empty;

        string friendly;
        if (exe.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (cmd.Contains("VBCSCompiler.dll", StringComparison.OrdinalIgnoreCase))
                friendly = "Compilatore .NET Roslyn (VBCSCompiler)";
            else if (cmd.Contains("MSBuild.dll", StringComparison.OrdinalIgnoreCase))
                friendly = "Motore di build .NET (MSBuild)";
            else
                friendly = UsefulProduct(product) ? product! : ".NET Host";
        }
        else if (exe.Equals("ProcessLens.MemoryAgent", StringComparison.OrdinalIgnoreCase))
        {
            friendly = "ProcessLens — Monitor Avvisi RAM";
        }
        else if (exe.Equals("ProcessLens.Guardian.Service", StringComparison.OrdinalIgnoreCase))
        {
            friendly = "ProcessLens — Servizio Guardian ETW";
        }
        else if (exe.Equals("ProcessLensProfiler", StringComparison.OrdinalIgnoreCase))
        {
            friendly = "ProcessLens — Profiler Heap .NET";
        }
        else
        {
            friendly = UsefulProduct(product) ? product! :
                       UsefulDescription(description, exe) ? description! :
                       Humanize(exe);
        }

        int confidence = 55;
        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(p.ExecutablePath))
        {
            confidence = 75;
            reasons.Add("percorso eseguibile disponibile");
        }
        if (!string.IsNullOrWhiteSpace(description))
        {
            confidence = Math.Max(confidence, 88);
            reasons.Add($"descrizione file = {description}");
        }
        if (!string.IsNullOrWhiteSpace(product))
        {
            confidence = Math.Max(confidence, 94);
            reasons.Add($"prodotto = {product}");
        }
        if (!string.IsNullOrWhiteSpace(p.PackageFullName))
        {
            confidence = Math.Max(confidence, 99);
            reasons.Add($"identità package Windows = {p.PackageFullName}");
        }
        if (!string.IsNullOrWhiteSpace(p.ApplicationUserModelId))
        {
            confidence = Math.Max(confidence, 99);
            reasons.Add($"AUMID Windows = {p.ApplicationUserModelId}");
        }
        if (!string.IsNullOrWhiteSpace(p.CompanyName))
            reasons.Add($"publisher metadata = {p.CompanyName}");

        return new IdentityAssessment(friendly, confidence,
            reasons.Count == 0 ? $"nome eseguibile = {p.Name}" : string.Join("; ", reasons));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool UsefulProduct(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return !value.Contains("Operating System", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("Sistema operativo Microsoft", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("Microsoft Windows", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsefulDescription(string? value, string exe)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return !value.Equals(exe, StringComparison.OrdinalIgnoreCase) &&
               !value.Equals(exe + ".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Processo sconosciuto";
        return SysPulseLocalization.Raw(value.Replace('_', ' ').Replace('-', ' '))!;
    }
}
