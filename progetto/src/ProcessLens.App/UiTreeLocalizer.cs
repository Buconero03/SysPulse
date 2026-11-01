using System.Windows;
using ProcessLens.Core;
namespace ProcessLens.App;
// Compatibility with existing activation calls. Re-coercion uses each current source value;
// it never caches the first report and replays it over later diagnostic updates.
internal static class UiTreeLocalizer {
    public static void Apply(DependencyObject? root,AppLanguage language)=>LocalizationView.Refresh(root);
}
