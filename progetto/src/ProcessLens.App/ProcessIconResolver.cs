using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using ProcessLens.Core;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProcessLens.App;

/// <summary>
/// Resolves the icon that Windows Shell associates with an executable.
/// Icons are cached by executable path so the process list does not repeatedly
/// hit Shell32 while it refreshes.
/// </summary>
public static class ProcessIconResolver
{
    private const uint FileAttributeNormal = 0x00000080;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;

    private sealed record ResolvedIcon(ImageSource? Image, bool Generic, string Source);
    private static readonly BoundedCache<string, ResolvedIcon> Cache =
        new(128, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> GenericLauncherNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "explorer.exe", "services", "services.exe", "svchost", "svchost.exe",
        "wininit", "wininit.exe", "winlogon", "winlogon.exe", "sihost", "sihost.exe",
        "RuntimeBroker", "RuntimeBroker.exe", "taskhostw", "taskhostw.exe", "userinit", "userinit.exe"
    };

    private static readonly Lazy<BitmapSource?> GenericIcon = new(() => TryGetShellIcon("application.exe", useFileAttributes: true));

    internal static ImageSource? GetIcon(ProcessSnapshot process)
    {
        var resolved = GetResolution(process.ExecutablePath, process.Name);
        if (!resolved.Generic && resolved.Image is not null)
            return resolved.Image;
        if (TryResolveContextualIcon(process) is { } contextual)
            return contextual;
        return resolved.Image;
    }

    internal static string IconDescription(ProcessSnapshot process)
    {
        var icon = GetResolution(process.ExecutablePath, process.Name);
        return icon.Source switch {
            "Shell" when !icon.Generic => "Icona associata all’eseguibile da Windows. Non verifica l’identità o la firma del processo.",
            "Risorsa eseguibile" when !icon.Generic => "Icona letta dalle risorse dell’eseguibile. Non verifica l’identità o la firma del processo.",
            _ when ProcessSymbolCatalog.Find(process.Name) is { } symbol => $"Simbolo ProcessLens: {symbol.Label}. Icona originale non disponibile. Firma digitale non verificata.",
            _ => "Distintivo grafico generato dal nome del file. Icona originale non disponibile; non identifica il produttore."
        };
    }
    internal static ImageSource? GetGroupIcon(string title, ProcessRow? first)
    {
        var symbol = ProcessSymbolCatalog.GroupOverride(title, first?.Name);
        return symbol is null ? first?.IconSource ?? ProcessSymbolDrawing.ForName(title) : ProcessSymbolDrawing.Known(symbol);
    }
    internal static string GetGroupIconDescription(string title, ProcessRow? first)
    {
        var symbol = ProcessSymbolCatalog.GroupOverride(title, first?.Name);
        return symbol is null ? first?.IconDescription ?? "Distintivo del gruppo." :
            $"Simbolo del gruppo {symbol.Label}, dal titolo mostrato. Non è l’icona estratta dal processo {first?.Name ?? "non disponibile"}.";
    }
    internal static bool IsGenericIcon(BitmapSource actual, BitmapSource? generic)
    {
        if (generic is null || actual.PixelWidth != generic.PixelWidth || actual.PixelHeight != generic.PixelHeight ||
            actual.PixelWidth is <= 0 or > 256 || actual.PixelHeight is <= 0 or > 256) return false;
        try
        {
            static byte[] Pixels(BitmapSource image)
            {
                BitmapSource normalized = image.Format == PixelFormats.Bgra32 ? image :
                    new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                int stride = image.PixelWidth * 4;
                var bytes = new byte[stride * image.PixelHeight];
                normalized.CopyPixels(bytes, stride, 0); return bytes;
            }
            return IconPreferencePolicy.SamePixels(Pixels(actual), Pixels(generic));
        }
        catch (Exception) { return false; } // If comparison is unavailable, preserve the executable's icon.
    }
    public static ImageSource? GetIcon(string? executablePath, string processName) =>
        GetResolution(executablePath, processName).Image;

    private static ResolvedIcon GetResolution(string? executablePath, string processName)
    {
        string key = !string.IsNullOrWhiteSpace(executablePath) ? executablePath : $"generic:{processName}";
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var choice = NativeIconPreference.ResolveExecutable(
            () => IsLocalExecutable(executablePath)
                ? (TryGetResourceIcon(executablePath!) ?? TryGetAppxManifestIcon(executablePath!) ?? TryGetSiblingExecutableIcon(executablePath!))
                : null,
            () => IsLocalExecutable(executablePath)
                ? (TryGetAppxManifestIcon(executablePath!) ?? TryGetSiblingExecutableIcon(executablePath!) ?? TryGetShellIcon(executablePath!, useFileAttributes: false))
                : null,
            bitmap => IsGenericIcon(bitmap, GenericIcon.Value));
        var icon = choice.Image ?? GenericIcon.Value;
        bool generic = choice.Image is null || IsGenericIcon(choice.Image, GenericIcon.Value);
        var result = new ResolvedIcon(generic ? Placeholder(processName) : icon, generic, choice.Source);
        Cache[key] = result; // Cache failures too: protected processes do not repeatedly hit the Shell.
        return result;
    }

    private static ImageSource? TryResolveContextualIcon(ProcessSnapshot process)
    {
        // Differentiate svchost.exe instances by the specific Windows service category they host
        if ((process.Name.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase) ||
             process.Name.Equals("svchost", StringComparison.OrdinalIgnoreCase)) &&
            (process.ServiceNames.Count > 0 || process.ServiceDisplayNames.Count > 0))
        {
            string? serviceSymbolName = SelectServiceSymbolName(process.ServiceNames, process.ServiceDisplayNames);
            if (serviceSymbolName is not null && ProcessSymbolCatalog.Find(serviceSymbolName) is { } svcSymbol)
                return ProcessSymbolDrawing.Known(svcSymbol);
        }

        // If this is an unknown helper binary launched by a parent application that has a real icon, inherit the parent app's icon
        if (ProcessSymbolCatalog.Find(process.Name) is null && process.AncestorDetails.Count > 0)
        {
            foreach (var ancestor in process.AncestorDetails)
            {
                if (GenericLauncherNames.Contains(ancestor.Name))
                    continue;
                if (!string.IsNullOrWhiteSpace(ancestor.ExecutablePath))
                {
                    var parentRes = GetResolution(ancestor.ExecutablePath, ancestor.Name);
                    if (!parentRes.Generic && parentRes.Image is not null)
                        return parentRes.Image;
                }
                if (ProcessSymbolCatalog.Find(ancestor.Name) is { } ancestorSymbol)
                    return ProcessSymbolDrawing.Known(ancestorSymbol);
            }
        }

        return null;
    }

    private static string? SelectServiceSymbolName(IReadOnlyList<string> names, IReadOnlyList<string> displayNames)
    {
        string joined = string.Join(" ", names) + " " + string.Join(" ", displayNames);
        if (ContainsAny(joined, "Audio", "Audiosrv", "AudioEndpointBuilder", "MMCSS", "RtkAudio"))
            return "audiodg";
        if (ContainsAny(joined, "Defend", "MpsSvc", "Firewall", "BFE", "wscsvc", "Security", "Sicurezza", "WdNis"))
            return "MsMpEng";
        if (ContainsAny(joined, "wuauserv", "UsoSvc", "DoSvc", "BITS", "Update", "Aggiornament", "WaaSMedic", "Install", "AppXSvc", "ClipSVC"))
            return "TiWorker";
        if (ContainsAny(joined, "Spooler", "Print", "Stampa", "StiSvc"))
            return "spoolsv";
        if (ContainsAny(joined, "Dnscache", "Dhcp", "WlanSvc", "NlaSvc", "WinHttp", "netprofm", "nsi", "Lanman", "Tcpip", "SSDPSRV", "upnphost", "RmSvc", "icssvc", "SharedAccess", "Rete", "Network"))
            return "WmiPrvSE";
        if (ContainsAny(joined, "CryptSvc", "KeyIso", "VaultSvc", "BDESVC", "BitLocker", "SamSs", "TokenBroker", "Cred"))
            return "LsaIso";
        if (ContainsAny(joined, "WSearch", "Index", "Indicizz"))
            return "SearchIndexer";
        if (ContainsAny(joined, "Power", "Alimentazione", "Energia", "Battery"))
            return "wininit";
        if (ContainsAny(joined, "Themes", "DispBroker", "FontCache", "Display", "Schermo", "Grafica"))
            return "dwm";
        if (ContainsAny(joined, "Schedule", "TimeBroker", "W32Time", "Orario", "Pianific"))
            return "backgroundTaskHost";
        if (ContainsAny(joined, "ProfSvc", "UserManager", "TermService", "SessionEnv", "SENS", "Utente", "Session"))
            return "smss";
        if (ContainsAny(joined, "bthserv", "BTAGService", "Bluetooth", "PlugPlay", "HidServ", "Device", "Dispositiv"))
            return "dllhost";
        if (ContainsAny(joined, "StorSvc", "defragsvc", "vds", "Archiviazione", "Disco"))
            return "SearchIndexer";
        return null;
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (string kw in keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static DrawingImage Placeholder(string processName) => ProcessSymbolDrawing.ForName(processName);

    private static bool IsLocalExecutable(string? path) => path is { Length: > 3 and <= 32767 } &&
        char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' &&
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static BitmapSource? TryGetAppxManifestIcon(string executablePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(executablePath);
            for (int depth = 0; depth < 3 && !string.IsNullOrWhiteSpace(dir); depth++, dir = Path.GetDirectoryName(dir))
            {
                string manifestPath = Path.Combine(dir, "AppxManifest.xml");
                if (!File.Exists(manifestPath)) continue;

                var doc = XDocument.Load(manifestPath);
                var logoCandidates = new List<string>();
                foreach (var el in doc.Descendants())
                {
                    string local = el.Name.LocalName;
                    if (local is "VisualElements" or "DefaultTile")
                    {
                        foreach (string attrName in new[] { "Square44x44Logo", "SmallLogo", "Square150x150Logo", "Logo" })
                        {
                            string? val = el.Attribute(attrName)?.Value;
                            if (!string.IsNullOrWhiteSpace(val)) logoCandidates.Add(val);
                        }
                    }
                    else if (local == "Logo" && !string.IsNullOrWhiteSpace(el.Value))
                    {
                        logoCandidates.Add(el.Value.Trim());
                    }
                }

                foreach (string relativeLogo in logoCandidates)
                {
                    if (TryResolveAppxLogoFile(dir, relativeLogo) is { } pngPath &&
                        TryLoadPngBitmap(pngPath) is { } bitmap)
                    {
                        return bitmap;
                    }
                }
                break;
            }
        }
        catch (Exception) { }
        return null;
    }

    private static string? TryResolveAppxLogoFile(string packageRoot, string relativeLogo)
    {
        string full = Path.Combine(packageRoot, relativeLogo.Replace('/', '\\'));
        if (File.Exists(full)) return full;

        string? logoDir = Path.GetDirectoryName(full);
        string stem = Path.GetFileNameWithoutExtension(full);
        string ext = Path.GetExtension(full);
        if (string.IsNullOrWhiteSpace(logoDir) || !Directory.Exists(logoDir)) return null;

        var matches = Directory.GetFiles(logoDir, $"{stem}.*{ext}");
        if (matches.Length == 0) return null;

        return matches
            .OrderByDescending(f => f.Contains("targetsize-32", StringComparison.OrdinalIgnoreCase) || f.Contains("targetsize-48", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.Contains("altform-unplated", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.Contains("scale-200", StringComparison.OrdinalIgnoreCase) || f.Contains("scale-100", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static BitmapSource? TryLoadPngBitmap(string pngPath)
    {
        try
        {
            using var stream = new FileStream(pngPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 32;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception) { return null; }
    }

    private static BitmapSource? TryGetSiblingExecutableIcon(string executablePath)
    {
        try
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(winDir) &&
                executablePath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase) &&
                !executablePath.Contains("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) &&
                !executablePath.Contains("\\SystemApps\\", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? dir = Path.GetDirectoryName(executablePath);
            for (int depth = 0; depth < 2 && !string.IsNullOrWhiteSpace(dir); depth++, dir = Path.GetDirectoryName(dir))
            {
                if (!Directory.Exists(dir)) continue;
                string? parentRoot = Path.GetPathRoot(dir);
                if (string.Equals(dir, parentRoot, StringComparison.OrdinalIgnoreCase)) break;

                foreach (string candidate in Directory.EnumerateFiles(dir, "*.exe").Take(12))
                {
                    if (string.Equals(candidate, executablePath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (TryGetResourceIcon(candidate) is { } icon && !IsGenericIcon(icon, GenericIcon.Value))
                        return icon;
                }
            }
        }
        catch (Exception) { }
        return null;
    }

    private static BitmapSource? TryGetResourceIcon(string path)
    {
        nint icon = 0;
        try
        {
            uint count = ExtractIconExW(path, 0, out icon, IntPtr.Zero, 1);
            if (count is 0 or uint.MaxValue || icon == 0) return null;
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze(); return bitmap;
        }
        catch (Exception) { return null; }
        finally { if (icon != 0) DestroyIcon(icon); }
    }

    private static BitmapSource? TryGetShellIcon(string path, bool useFileAttributes)
    {
        uint flags = ShgfiIcon | ShgfiLargeIcon;
        if (useFileAttributes)
            flags |= ShgfiUseFileAttributes;

        var info = new ShellFileInfo();
        nint result = SHGetFileInfo(
            path,
            useFileAttributes ? FileAttributeNormal : 0,
            ref info,
            (uint)Marshal.SizeOf<ShellFileInfo>(),
            flags);

        try
        {
            if (result == 0 || info.IconHandle == 0) return null;
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                info.IconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            if (info.IconHandle != 0) DestroyIcon(info.IconHandle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShellFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern uint ExtractIconExW(string file, int index, out nint large, nint small, uint count);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);
}
