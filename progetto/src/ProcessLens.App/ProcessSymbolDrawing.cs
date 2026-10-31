using System.Windows;
using System.Windows.Media;
using ProcessLens.Core;

namespace ProcessLens.App;

internal static class ProcessSymbolDrawing
{
    private static readonly BoundedCache<string, DrawingImage> Cache = new(128, StringComparer.OrdinalIgnoreCase);
    private static readonly string[] Colors = { "#175CD3", "#6941C6", "#027A48", "#B54708", "#B42318", "#026AA2", "#C11574", "#475467" };

    internal static DrawingImage ForName(string name)
    {
        var symbol = ProcessSymbolCatalog.Find(name);
        return symbol is null ? Distinctive(name) : Known(symbol);
    }

    internal static DrawingImage Known(ProcessSymbol symbol)
    {
        string key = "known:" + symbol.Id;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        try {
            var group = Background();
            foreach (var layer in symbol.Layers) {
                var geometry = Geometry.Parse(layer.Path);
                var pen = layer.Stroke is null ? null : new Pen(Paint(layer.Stroke, symbol.Color), layer.Width) {
                    StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                group.Children.Add(new GeometryDrawing(layer.Fill is null ? null : Paint(layer.Fill, symbol.Color), pen, geometry));
            }
            return Save(key, group);
        } catch (Exception) { return Distinctive(symbol.Id); } // Optional visuals must not break the process list.
    }

    private static readonly string[] FallbackGlyphPaths =
    {
        // App window with header bar and controls
        "M4,6 H28 V26 H4 Z M4,11 H28 M8,8.5 H10 M12,8.5 H14",
        // Gear / service component
        "M14,4 H18 L19,7 L22,6 L25,9 L24,12 L27,14 V18 L24,20 L25,23 L22,26 L19,25 L18,28 H14 L13,25 L10,26 L7,23 L8,20 L5,18 V14 L8,12 L7,9 L10,6 L13,7 Z M12,16 A4,4 0 1 0 20,16 A4,4 0 1 0 12,16 Z",
        // Terminal / CLI prompt
        "M4,6 H28 V26 H4 Z M9,12 L14,16 L9,20 M16,20 H23",
        // Microchip / system runtime
        "M9,9 H23 V23 H9 Z M12,4 V9 M16,4 V9 M20,4 V9 M12,23 V28 M16,23 V28 M20,23 V28 M4,12 H9 M4,16 H9 M4,20 H9 M23,12 H28 M23,16 H28 M23,20 H28 M13,13 H19 V19 H13 Z",
        // Network nodes
        "M16,5 A3.5,3.5 0 1 0 16,12 A3.5,3.5 0 1 0 16,5 Z M6,20 A3.5,3.5 0 1 0 6,27 A3.5,3.5 0 1 0 6,20 Z M26,20 A3.5,3.5 0 1 0 26,27 A3.5,3.5 0 1 0 26,20 Z M16,12 V17 L8,21 M16,17 L24,21",
        // Shield / security / guardian
        "M16,4 L27,8 V16 Q26,24 16,28 Q6,24 5,16 V8 Z M11,16 L14.5,19.5 L21,12.5",
        // Cube / module / package
        "M16,4 L28,10 V22 L16,28 L4,22 V10 Z M4,10 L16,16 L28,10 M16,16 V28",
        // Database / storage
        "M6,8 A10,4 0 1 0 26,8 A10,4 0 1 0 6,8 M6,8 V24 A10,4 0 0 0 26,24 V8 M6,16 A10,4 0 0 0 26,16"
    };

    private static DrawingImage Distinctive(string name)
    {
        string key = "file:" + ProcessIconPlaceholder.Key(name);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var group = Background();
        string hexColor = Colors[ProcessIconPlaceholder.ColorIndex(name)];
        var brush = Paint(hexColor, "#175CD3");
        int pattern = ProcessIconPlaceholder.Pattern(name);

        // Soft tinted inner badge background
        var softBg = (Color)ColorConverter.ConvertFromString(hexColor);
        var tintBrush = new SolidColorBrush(Color.FromArgb(26, softBg.R, softBg.G, softBg.B));
        group.Children.Add(new GeometryDrawing(tintBrush, null, new RectangleGeometry(new Rect(2, 2, 28, 28), 6, 6)));

        // Select semantic vector shape based on name keywords or pattern
        int glyphIndex = SelectSemanticGlyphIndex(name, pattern);
        var pen = new Pen(brush, 2.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        group.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse(FallbackGlyphPaths[glyphIndex])));

        // Subtle deterministic accent dots along the bottom bar so every process name stays uniquely identifiable
        for (int bit = 0; bit < 4; bit++)
        {
            if ((pattern & (1 << bit)) == 0) continue;
            double cx = 9 + bit * 4.5;
            group.Children.Add(new GeometryDrawing(brush, null, new EllipseGeometry(new Point(cx, 29.2), 1.1, 1.1)));
        }
        return Save(key, group);
    }

    private static int SelectSemanticGlyphIndex(string? name, int pattern)
    {
        if (string.IsNullOrWhiteSpace(name)) return pattern & 7;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("svc") || lower.Contains("serv") || lower.Contains("host") || lower.Contains("daemon") || lower.Contains("agent"))
            return 1; // Gear
        if (lower.Contains("cli") || lower.Contains("cmd") || lower.Contains("sh") || lower.Contains("term") || lower.Contains("con") || lower.Contains("tool") || lower.Contains(" build"))
            return 2; // Terminal
        if (lower.Contains("sys") || lower.Contains("drv") || lower.Contains("kern") || lower.Contains("mem") || lower.Contains("cpu") || lower.Contains(" hal"))
            return 3; // Chip
        if (lower.Contains("net") || lower.Contains("http") || lower.Contains("web") || lower.Contains("dns") || lower.Contains("vpn") || lower.Contains("sync") || lower.Contains("cloud"))
            return 4; // Network
        if (lower.Contains("sec") || lower.Contains("def") || lower.Contains("guard") || lower.Contains("auth") || lower.Contains("lock") || lower.Contains("crypt") || lower.Contains("cert"))
            return 5; // Shield
        if (lower.Contains("pkg") || lower.Contains("inst") || lower.Contains("setup") || lower.Contains("upd") || lower.Contains("work") || lower.Contains("helper") || lower.Contains("broker"))
            return 6; // Cube / module
        if (lower.Contains("db") || lower.Contains("sql") || lower.Contains("data") || lower.Contains("index") || lower.Contains("cache") || lower.Contains("store"))
            return 7; // Database
        return pattern & 7;
    }

    private static DrawingGroup Background()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 32, 32), 5, 5)));
        return group;
    }
    private static Brush Paint(string value, string accent) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(value == "accent" ? accent : value));
    private static DrawingImage Save(string key, DrawingGroup group)
    {
        var image = new DrawingImage(group); image.Freeze(); Cache[key] = image; return image;
    }
}
