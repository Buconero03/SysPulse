namespace ProcessLens.App;

// Presentation only: never adds measurements or changes the original report used for copying.
internal static class ReportLayout
{
    internal sealed record Section(string Heading, string Body);
    internal sealed record Block(string Label, string Text);

    public static IReadOnlyList<Section> Sections(string text)
    {
        var result = new List<Section>();
        string heading = "Quadro generale";
        var lines = new List<string>();
        bool hasHeading = false;
        foreach (string line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("■ ", StringComparison.Ordinal) && line.Length > 2)
            {
                if (hasHeading || lines.Any(s => !string.IsNullOrWhiteSpace(s)))
                    result.Add(new Section(heading, string.Join("\n", lines)));
                heading = line[2..];
                lines.Clear();
                hasHeading = true;
            }
            else lines.Add(line);
        }
        if (hasHeading || lines.Count > 0)
            result.Add(new Section(heading, string.Join("\n", lines)));
        return result;
    }

    public static IReadOnlyList<Block> Blocks(string text)
    {
        var result = new List<Block>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string line in paragraph.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string label = "";
                string value = line;
                foreach (string prefix in new[] {
                    "VirtualQueryEx:", "QueryWorkingSetEx:",
                    "Contatori storici e Kernel Pool (PSAPI EX2):", "PSAPI EX2:", "Guida tecnica:",
                    "Moduli caricati (campione):", "Campione DLL osservate:",
                    "Classi finestre:", "Deep inspection:", "Origine:", "Catena padre:" })
                {
                    if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    label = prefix[..^1];
                    value = line[prefix.Length..].TrimStart(' ');
                    // Existing PSAPI fields get separate visual lines; numbers/units stay untouched.
                    if (prefix is "Contatori storici e Kernel Pool (PSAPI EX2):" or "PSAPI EX2:")
                        value = value.Replace(" · ", "\n", StringComparison.Ordinal);
                    break;
                }
                result.Add(new Block(label, value));
            }
        }
        return result;
    }

    public static string EvidenceGroup(string line, string fallback)
    {
        if (line.StartsWith("Moduli caricati", StringComparison.Ordinal) ||
            line.StartsWith("Campione DLL osservate", StringComparison.Ordinal)) return "Moduli e librerie";
        if (line.StartsWith("Classi finestre:", StringComparison.Ordinal)) return "Finestre osservate";
        if (line.StartsWith("Deep inspection:", StringComparison.Ordinal)) return "Esito delle letture";
        return fallback;
    }
}
