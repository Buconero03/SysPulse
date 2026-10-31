namespace ProcessLens.App;

// A visual placeholder derived from the observed filename, never a claimed product logo.
internal static class ProcessIconPlaceholder
{
    internal static string Key(string? name) => string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().ToUpperInvariant();
    internal static int Pattern(string? name)
    {
        ulong hash = 14695981039346656037;
        foreach (char c in Key(name)) hash = unchecked((hash ^ c) * 1099511628211);
        int pattern = (int)((hash ^ (hash >> 32)) & 0x7FFF);
        return pattern == 0 ? 1 << 7 : pattern;
    }
    internal static int ColorIndex(string? name)
    {
        uint hash = 2166136261;
        foreach (char c in Key(name)) hash = unchecked((hash ^ c) * 16777619);
        return (int)(hash % 8);
    }
}
