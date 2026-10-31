using ProcessLens.Core;

namespace ProcessLens.App;

// A display classification from the existing snapshot, never a signature/security verdict.
internal static class WindowsProcessIconPolicy
{
    internal const string WindowsDescription = "Componente Windows · classificazione da PID riservato, nome/percorso o metadati del file. Firma digitale non verificata.";
    internal const string ExecutableDescription = "Icona dell’eseguibile o segnaposto.";
    internal static bool UseWindowsIcon(ProcessSnapshot p, string windowsDirectory)
    {
        if (p.Pid == 0) return p.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("System Idle Process", StringComparison.OrdinalIgnoreCase);
        if (p.Pid == 4) return p.Name.Equals("System", StringComparison.OrdinalIgnoreCase);
        if (!SafeWindowsPath(p.ExecutablePath, windowsDirectory)) return false;
        string name = p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p.Name : p.Name + ".exe";
        string actual = p.ExecutablePath!.Replace('/', '\\');
        if (!actual[(actual.LastIndexOf('\\') + 1)..].Equals(name, StringComparison.OrdinalIgnoreCase)) return false;
        var entry = new SystemProcessEntry(p.Pid, p.ParentPid, name, null, null, null, null, p.ExecutablePath);
        if (WindowsComponentCatalog.Match(entry, windowsDirectory) is not null) return true;
        // Remaining OS components (e.g. ShellHost/SystemApps) may carry the OS product metadata.
        // Restrict this fallback to OS locations: a Microsoft app or a Windows\Temp binary is not enough.
        string relative = actual[(windowsDirectory.Replace('/', '\\').TrimEnd('\\').Length + 1)..];
        if (!(relative.StartsWith("System32\\", StringComparison.OrdinalIgnoreCase) ||
              relative.StartsWith("SysWOW64\\", StringComparison.OrdinalIgnoreCase) ||
              relative.StartsWith("SystemApps\\", StringComparison.OrdinalIgnoreCase))) return false;
        string company = (p.CompanyName ?? "").Trim();
        if (!company.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase)) return false;
        string product = string.Join(' ', (p.ProductName ?? "").Replace("®", "").Replace("™", "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return product.Equals("Microsoft Windows Operating System", StringComparison.OrdinalIgnoreCase) ||
               product.Equals("Sistema operativo Microsoft Windows", StringComparison.OrdinalIgnoreCase);
    }
    private static readonly HashSet<string> SharedSymbolNames = new(StringComparer.OrdinalIgnoreCase) {
        "svchost.exe", "services.exe", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "lsass.exe" };
    internal static bool UseSharedWindowsSymbol(ProcessSnapshot p, string windowsDirectory) =>
        (p.Pid is 0 or 4 || SharedSymbolNames.Contains(p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p.Name : p.Name + ".exe")) &&
        UseWindowsIcon(p, windowsDirectory);

    private static bool SafeWindowsPath(string? path, string directory)
    {
        if (directory.Any(char.IsControl) || (directory.Length > 2 && directory[2..].Contains(':')) || directory.Split('\\', '/').Any(x => x is "." or "..")) return false;
        if (path is null || !SystemProcessPresentation.UnderWindows(path, directory)) return false;
        return !path.Any(char.IsControl) && !path[2..].Contains(':');
    }
}
