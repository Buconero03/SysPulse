using System.IO;
using System.Text.Json;

namespace ProcessLens.App;

internal sealed record SymbolLayer(string Path, string? Fill, string? Stroke, double Width);
internal sealed record ProcessSymbol(string Id, string Label, string Color, IReadOnlyList<SymbolLayer> Layers);

// Display symbols from exact names, not process classification or signature verification.
internal static class ProcessSymbolCatalog
{
    internal const string ResourceName = "ProcessLens.ProcessSymbols";
    private sealed record Catalog(IReadOnlyList<ProcessSymbol> Entries,
        Dictionary<string, ProcessSymbol> Names, Dictionary<string, ProcessSymbol> Groups, string? Error);
    private static readonly Lazy<Catalog> Data = new(Load);
    internal static IReadOnlyList<ProcessSymbol> Entries => Data.Value.Entries;
    internal static string? LoadError => Data.Value.Error;

    internal static ProcessSymbol? Find(string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName) || executableName.Length > 260 ||
            executableName.Contains('\\') || executableName.Contains('/') || executableName.Contains(':') || executableName.Any(char.IsControl)) return null;
        string key = executableName.Trim();
        if (key.Equals("System Idle Process", StringComparison.OrdinalIgnoreCase)) key = "Idle";
        if (key.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) key = key[..^4];
        return Data.Value.Names.GetValueOrDefault(key);
    }

    // A group labelled Git must not inherit Bash's image merely because Bash is the first row.
    // This affects only the group illustration; no membership or process identity changes.
    internal static ProcessSymbol? GroupOverride(string? title, string? executableName)
    {
        if (title is null || title.Length > 128 || !Data.Value.Groups.TryGetValue(title.Trim(), out var group)) return null;
        return Find(executableName)?.Id == group.Id ? null : group;
    }

    private static Catalog Load()
    {
        var names = new Dictionary<string, ProcessSymbol>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<string, ProcessSymbol>(StringComparer.OrdinalIgnoreCase);
        try {
            using var stream = typeof(ProcessSymbolCatalog).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null || stream.Length is <= 0 or > 128 * 1024) throw new InvalidDataException("Catalogo icone non disponibile.");
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            if (root.GetProperty("schema").GetInt32() != 1) throw new InvalidDataException("Schema icone non supportato.");
            var glyphs = new Dictionary<string, SymbolLayer[]>();
            foreach (var glyph in root.GetProperty("glyphs").EnumerateObject()) {
                var layers = glyph.Value.EnumerateArray().Select(x => new SymbolLayer(
                    x.GetProperty("path").GetString()!, x.GetProperty("fill").GetString(),
                    x.GetProperty("stroke").GetString(), x.GetProperty("width").GetDouble())).ToArray();
                if (layers.Length is < 1 or > 12 || layers.Any(x => x.Path.Length is < 1 or > 1024 || !double.IsFinite(x.Width) || x.Width is < 0 or > 5))
                    throw new InvalidDataException("Geometria icona non valida.");
                glyphs.Add(glyph.Name, layers);
                if (glyphs.Count > 128) throw new InvalidDataException("Troppe geometrie.");
            }
            var entries = new List<ProcessSymbol>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in root.GetProperty("entries").EnumerateArray()) {
                var symbol = new ProcessSymbol(entry.GetProperty("id").GetString()!, entry.GetProperty("label").GetString()!,
                    entry.GetProperty("color").GetString()!, glyphs[entry.GetProperty("glyph").GetString()!]);
                if (!ids.Add(symbol.Id) || symbol.Id.Length is < 1 or > 64 || symbol.Label.Length is < 1 or > 80 ||
                    symbol.Color.Length != 7 || symbol.Color[0] != '#' || !symbol.Color.AsSpan(1).ToString().All(Uri.IsHexDigit))
                    throw new InvalidDataException("Descrittore icona non valido.");
                entries.Add(symbol);
                foreach (var name in entry.GetProperty("names").EnumerateArray()) names.Add(name.GetString()!, symbol);
                foreach (var group in entry.GetProperty("groups").EnumerateArray()) groups.Add(group.GetString()!, symbol);
                if (entries.Count > 256 || names.Count > 512 || groups.Count > 256) throw new InvalidDataException("Catalogo troppo grande.");
            }
            return new(entries.ToArray(), names, groups, null);
        } catch (Exception ex) {
            return new(Array.Empty<ProcessSymbol>(), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), ex.GetType().Name);
        }
    }
}
