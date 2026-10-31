using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProcessLens.App;

internal static class ProcessLensIcon
{
    private static readonly Lazy<BitmapFrame?> Image = new(() => Decode(
        () => IconFile.ReadEmbedded(typeof(ProcessLensIcon).Assembly)));

    internal static BitmapFrame? Decode(Func<byte[]?> read)
    {
        try {
            byte[]? bytes = read();
            if (!IconFile.TrySelectImage(bytes, 32, out _)) return null;
            using var stream = new MemoryStream(bytes!, writable: false);
            var image = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            image.Freeze();
            return image;
        } catch (Exception ex) {
            AppDiagnostics.Record("Icona facoltativa non disponibile; avvio senza icona personalizzata", ex);
            return null;
        }
    }

    internal static void Apply(Window window)
    {
        try { if (Image.Value is { } icon) window.Icon = icon; }
        catch (Exception ex) { AppDiagnostics.Record("Icona finestra non applicata; avvio prosegue", ex); }
    }
}
