using System.Runtime.InteropServices;
using System.Windows;

namespace ProcessLens.App;

internal static class ClipboardText
{
    public static void Copy(DependencyObject source, string text)
    {
        if (text.Length == 0) return;
        try { Clipboard.SetText(text); }
        catch (ExternalException)
        {
            const string message = "Gli appunti sono temporaneamente occupati da un'altra applicazione. Riprova a copiare il testo.";
            var owner = Window.GetWindow(source);
            if (owner is null) MessageBox.Show(message, "ProcessLens — Copia", MessageBoxButton.OK, MessageBoxImage.Information);
            else MessageBox.Show(owner, message, "ProcessLens — Copia", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
