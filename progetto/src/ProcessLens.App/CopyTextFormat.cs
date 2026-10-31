namespace ProcessLens.App;

// Plain text only. No hidden process metadata or command line is added to a copy.
internal static class CopyTextFormat
{
    public static string Report(string title, string body, string confidence, string evidence, string audit = "")
    {
        var parts = new[] { title, body, confidence, evidence,
            string.IsNullOrEmpty(audit) ? "" : "REGISTRO VERIFICHE\n" + audit };
        return string.Join("\n\n", parts.Where(x => !string.IsNullOrEmpty(x)));
    }

    public static string Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        static string Cell(string value) => value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) < 0
            ? value : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var lines = new List<string> { string.Join("\t", headers.Select(Cell)) };
        lines.AddRange(rows.Select(row => string.Join("\t", row.Select(Cell))));
        return string.Join("\r\n", lines);
    }
}
