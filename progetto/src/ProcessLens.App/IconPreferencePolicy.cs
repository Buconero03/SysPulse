namespace ProcessLens.App;

internal static class IconPreferencePolicy
{
    internal const int MaximumPixelBytes = 256 * 256 * 4;
    internal static T? Select<T>(T? original, bool generic, bool windowsComponent, T windowsFallback) where T : class =>
        windowsComponent && (original is null || generic) ? windowsFallback : original;
    internal static bool SamePixels(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> generic) =>
        actual.Length > 0 && actual.Length <= MaximumPixelBytes && actual.SequenceEqual(generic);
}
