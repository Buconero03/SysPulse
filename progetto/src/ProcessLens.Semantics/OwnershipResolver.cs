using ProcessLens.Core;

namespace ProcessLens.Semantics;

internal static class OwnershipResolver
{
    private static readonly HashSet<string> GenericHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet.exe", "python.exe", "pythonw.exe", "node.exe", "java.exe", "javaw.exe",
        "powershell.exe", "pwsh.exe", "msedgewebview2.exe", "conhost.exe"
    };

    public static OwnershipAssessment Resolve(ProcessSnapshot p, IdentityAssessment identity, FrameworkAssessment framework)
    {
        if (p.IsCritical || IsWindowsPath(p.ExecutablePath))
            return new("Windows", 96, "percorso nella directory Windows o flag critico; non prova autenticità o produttore");

        if (framework.Name == "Microsoft Edge WebView2")
        {
            string? host = p.AncestorNames.FirstOrDefault(x => !x.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(host))
                return new(PrettyExe(host), 88, $"nome del primo antenato esterno a WebView2 = {host}; associazione proposta");
        }

        if (GenericHosts.Contains(p.Name) && !string.IsNullOrWhiteSpace(p.ParentName) && !GenericHosts.Contains(p.ParentName))
            return new(PrettyExe(p.ParentName), 82, $"runtime generico; nome del padre riportato = {p.ParentName}; relazione temporale non verificata qui");

        if (string.Equals(p.ParentName, p.Name, StringComparison.OrdinalIgnoreCase))
            return new(identity.FriendlyName, Math.Max(80, identity.Confidence - 5), "nome del padre omonimo; associazione proposta, istanza da verificare");

        if (!string.IsNullOrWhiteSpace(p.ProductName) && !p.ProductName.Contains("Operating System", StringComparison.OrdinalIgnoreCase))
            return new(p.ProductName.Trim(), 94, "ProductName dell'eseguibile");

        if (!string.IsNullOrWhiteSpace(p.PackageFullName) || !string.IsNullOrWhiteSpace(p.ApplicationUserModelId))
            return new(identity.FriendlyName, 94, "identità applicativa Windows (package/AUMID)");

        return new(identity.FriendlyName, Math.Min(identity.Confidence, 75), "identità dell'eseguibile; applicazione associata non distinguibile con i dati disponibili");
    }

    private static bool IsWindowsPath(string? path) =>
        path?.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase) == true;

    private static string PrettyExe(string value) => Path.GetFileNameWithoutExtension(value).Replace('_', ' ').Replace('-', ' ');
}
