namespace ProcessLens.App;

internal sealed record NativeIconChoice<T>(T? Image, string Source) where T : class;
internal static class NativeIconPreference
{
    internal static NativeIconChoice<T> ResolveExecutable<T>(Func<T?> embedded, Func<T?> shell, Func<T, bool> generic) where T : class
    {
        T? resource;
        try { resource = embedded(); } catch { resource = null; }
        if (resource is not null && !generic(resource)) return new(resource, "Risorsa eseguibile");
        // No duplicate resource read; Shell is queried only if the file has no usable embedded icon.
        T? associated;
        try { associated = shell(); } catch { associated = null; }
        return Resolve(associated, generic, () => null);
    }
    // The second resolver is lazy: a real Shell icon never triggers another resource read.
    internal static NativeIconChoice<T> Resolve<T>(T? shell, Func<T, bool> generic, Func<T?> embedded) where T : class
    {
        if (shell is not null && !generic(shell)) return new(shell, "Shell");
        T? resource;
        try { resource = embedded(); } catch { resource = null; }
        if (resource is not null && !generic(resource)) return new(resource, "Risorsa eseguibile");
        return new(shell, shell is null ? "Non disponibile" : "Generica");
    }
}
