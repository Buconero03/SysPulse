using ProcessLens.Core;

namespace ProcessLens.Semantics;

internal static class FrameworkDetector
{
    public static FrameworkAssessment Detect(ProcessSnapshot p)
    {
        string n = p.Name.ToLowerInvariant();
        string cmd = p.CommandLine ?? string.Empty;
        string meta = $"{p.ProductName} {p.FileDescription} {p.CompanyName} {p.ExecutablePath}";
        bool chromiumModules = HasAnyModule(p, "chrome_elf.dll", "libegl.dll", "libglesv2.dll", "vk_swiftshader.dll");
        bool dotnetModules = HasAnyModule(p, "coreclr.dll", "clr.dll", "hostfxr.dll", "hostpolicy.dll");
        bool pythonModules = p.ModuleNames.Any(x => x.StartsWith("python", StringComparison.OrdinalIgnoreCase) && x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        bool javaModules = HasAnyModule(p, "jvm.dll");
        bool qtModules = p.ModuleNames.Any(x => x.StartsWith("Qt5Core", StringComparison.OrdinalIgnoreCase) || x.StartsWith("Qt6Core", StringComparison.OrdinalIgnoreCase));

        if (p.IsCritical)
            return new("Windows/System", 100, "Windows segnala il processo come critico");
        if (p.ServiceNames.Count > 0)
            return new("Windows Service", 99, "il PID è associato dal Service Control Manager a uno o più servizi");

        // First consult the generic knowledge base. Deep module/DLL fingerprints can
        // identify runtimes even when the executable name is generic or renamed.
        var knowledge = KnowledgeSignatureEngine.MatchFramework(p);
        if (knowledge is not null)
            return new(knowledge.Label, knowledge.Score,
                $"knowledge:{knowledge.Id}; {string.Join("; ", knowledge.Evidence.Take(5))}");
        if (n == "msedgewebview2.exe" || meta.Contains("WebView2", StringComparison.OrdinalIgnoreCase))
            return new("Microsoft Edge WebView2", 99, "runtime WebView2 identificato da nome/metadati");
        if (p.IsDeepInspected && chromiumModules)
            return new("Chromium/Electron", 96, "moduli Chromium caricati nel processo selezionato");
        if (HasChromiumSwitch(cmd))
            return new("Chromium/Electron", 98, "la command line usa switch multiprocesso Chromium (--type/--utility-sub-type)");
        if (meta.Contains("Electron", StringComparison.OrdinalIgnoreCase))
            return new("Electron", 96, "metadati dell'eseguibile indicano Electron");
        if (n == "dotnet.exe" || meta.Contains(".NET", StringComparison.OrdinalIgnoreCase) || (p.IsDeepInspected && dotnetModules))
            return new(".NET", p.IsDeepInspected && dotnetModules ? 99 : 98, p.IsDeepInspected && dotnetModules ? "moduli CLR compatibili con runtime .NET" : "runtime/metadata .NET rilevati");
        if (n is "python.exe" or "pythonw.exe" || meta.Contains("Python", StringComparison.OrdinalIgnoreCase) || (p.IsDeepInspected && pythonModules))
            return new("Python", p.IsDeepInspected && pythonModules ? 99 : 98, p.IsDeepInspected && pythonModules ? "nomi di moduli compatibili con runtime Python" : "interprete Python rilevato");
        if (n == "node.exe" || meta.Contains("Node.js", StringComparison.OrdinalIgnoreCase))
            return new("Node.js", 98, "runtime Node.js rilevato");
        if (n is "java.exe" or "javaw.exe" || meta.Contains("Java", StringComparison.OrdinalIgnoreCase) || (p.IsDeepInspected && javaModules))
            return new("Java/JVM", p.IsDeepInspected && javaModules ? 99 : 97, p.IsDeepInspected && javaModules ? "modulo jvm.dll compatibile con JVM" : "Java Virtual Machine rilevata");
        if (p.IsDeepInspected && qtModules)
            return new("Qt", 96, "moduli Qt Core caricati nel processo selezionato");
        if (n is "powershell.exe" or "pwsh.exe")
            return new("PowerShell", 99, "shell PowerShell rilevata");
        if (n is "wsl.exe" or "wslhost.exe" or "vmmemwsl")
            return new("WSL", 99, "componente Windows Subsystem for Linux rilevato");
        if (n == "firefox.exe" && cmd.Contains("-contentproc", StringComparison.OrdinalIgnoreCase))
            return new("Firefox multiprocess", 99, "Firefox content process rilevato dalla command line");
        if (p.SameNameChildCount > 0 || string.Equals(p.ParentName, p.Name, StringComparison.OrdinalIgnoreCase))
            return new("Applicazione multiprocesso", 72, "nomi di processi collegati omonimi; identità del padre da verificare con PID e orario di avvio");
        if (!string.IsNullOrWhiteSpace(p.PackageFullName) || !string.IsNullOrWhiteSpace(p.ApplicationUserModelId))
            return new("Windows packaged app", 96, "processo associato a identità applicativa Windows (package/AUMID)");

        return new("Runtime non determinato", 45, "nessun runtime o framework riconoscibile con le evidenze disponibili");
    }

    private static bool HasAnyModule(ProcessSnapshot p, params string[] names) =>
        p.ModuleNames.Any(module => names.Any(name => module.Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static bool HasChromiumSwitch(string cmd) =>
        cmd.Contains("--type=", StringComparison.OrdinalIgnoreCase) ||
        cmd.Contains("--utility-sub-type=", StringComparison.OrdinalIgnoreCase) ||
        cmd.Contains("--field-trial-handle=", StringComparison.OrdinalIgnoreCase) ||
        cmd.Contains("--enable-features=", StringComparison.OrdinalIgnoreCase);
}
