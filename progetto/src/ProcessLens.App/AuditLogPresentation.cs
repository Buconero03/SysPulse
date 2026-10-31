using System.Globalization;
using System.Text.RegularExpressions;

namespace ProcessLens.App;

// Display adapter only. Original log text, files and diagnostic event IDs are untouched.
internal static class AuditLogPresentation
{
    internal sealed record Entry(string Time, string Stage, string Message, string Source)
    {
        public string Component => Source.Contains("] [RAM] ", StringComparison.Ordinal) ||
            Source.Contains("] [VERIFICA RAM] ", StringComparison.Ordinal) ? "RAM" :
            Time.Length > 0 || Stage is "AVVIO" or "SICUREZZA" or "ATTESA" ? "Profiler" : "Registro";
    }
    internal sealed record Document(IReadOnlyList<Entry> Entries, string Location);
    private static readonly Regex Ram = new(
        @"^\[(?<time>[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3})\] \[(?:RAM|VERIFICA RAM)\] (?<stage>.+?)(?: — |: )(?<message>.*)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ProfilerStart = new(
        @"^\[(?<stage>AVVIO|SICUREZZA|ATTESA)\] (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex ProfilerEnd = new(
        @"^\[(?<time>[0-9]{2}:[0-9]{2}:[0-9]{2})\] (?<stage>ESITO): (?<message>.*)$", RegexOptions.CultureInvariant);

    public static string FormatRamEntry(DateTime timestamp, string stage, string message) =>
        $"[{timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)}] [RAM] {stage}: {message}";

    public static Document Parse(string source)
    {
        var entries = new List<Entry>();
        string location = "";
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int last = Array.FindLastIndex(lines, s => s.Length > 0);
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (index == last && line.StartsWith("Registro locale: ", StringComparison.Ordinal))
            { location = line; continue; }
            var match = Ram.Match(line);
            if (match.Success && TimeSpan.TryParseExact(match.Groups["time"].Value, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out _))
            {
                entries.Add(new Entry(match.Groups["time"].Value, match.Groups["stage"].Value, match.Groups["message"].Value, line));
                continue;
            }
            match = ProfilerStart.Match(line);
            if (!match.Success)
            {
                match = ProfilerEnd.Match(line);
                if (match.Success && !TimeSpan.TryParseExact(match.Groups["time"].Value, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out _))
                    match = Match.Empty;
            }
            if (match.Success)
                entries.Add(new Entry(match.Groups["time"].Value, match.Groups["stage"].Value, match.Groups["message"].Value, line));
            else if (entries.Count > 0)
            {
                var previous = entries[^1];
                entries[^1] = previous with { Message = previous.Message + "\n" + line, Source = previous.Source + "\n" + line };
            }
            else if (line.Length > 0) entries.Add(new Entry("", "Registro", line, line));
        }
        return new Document(entries.ToArray(), location);
    }

    public static string Overview(Document document)
    {
        if (document.Entries.Count == 0) return "Registro vuoto.";
        var latest = document.Entries[^1];
        string time = latest.Time.Length > 0 ? " · " + latest.Time : "";
        string unit = document.Entries.Count == 1 ? "voce" : "voci";
        return $"{document.Entries.Count} {unit} · ultima: {latest.Stage}{time}";
    }
}
