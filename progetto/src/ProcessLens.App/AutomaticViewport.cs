using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProcessLens.App;

// Protect automatic updates only. User input and explicit navigation cancel pending restoration.
internal sealed class AutomaticViewport
{
    private readonly FrameworkElement _root;
    private readonly Func<ScrollViewer?> _viewer;
    private readonly ReadingPositionPolicy _policy = new();
    private DispatcherOperation? _pending;
    private bool _automatic;
    private UIElement? _bringBoundary;
    private readonly RequestBringIntoViewEventHandler _bringHandler;
    public AutomaticViewport(FrameworkElement root, Func<ScrollViewer?> viewer)
    {
        _root = root; _viewer = viewer;
        _bringHandler = (_, e) => { if (_automatic) e.Handled = true; };
        root.PreviewMouseWheel += (_, _) => Cancel();
        root.PreviewMouseDown += (_, _) => Cancel();
        root.PreviewMouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) Cancel(); };
        root.PreviewKeyDown += (_, _) => Cancel();
        root.PreviewTouchDown += (_, _) => Cancel();
        root.Unloaded += (_, _) => Cancel();
        root.AddHandler(FrameworkElement.RequestBringIntoViewEvent, _bringHandler, true);
    }
    internal bool HoldsOrder => ReadingPositionPolicy.HoldOrder(_viewer()?.VerticalOffset ?? 0,
        _root.IsMouseOver, _root.IsKeyboardFocusWithin);
    internal void Cancel()
    {
        _policy.UserInput(); _automatic = false;
        _pending?.Abort(); _pending = null;
    }
    internal IDisposable Begin()
    {
        // Nested updates share one outer capture.
        if (_automatic) return new Completion(() => { });
        var viewer = _viewer();
        if (viewer is null || !viewer.IsLoaded || _root.IsMouseCaptureWithin) return new Completion(() => { });
        // Intercept BELOW ScrollViewer, before its class handler or the outer ListView handles the event.
        var boundary = viewer.Content as UIElement;
        if (!ReferenceEquals(boundary, _bringBoundary))
        {
            _bringBoundary?.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, _bringHandler);
            _bringBoundary = boundary;
            _bringBoundary?.AddHandler(FrameworkElement.RequestBringIntoViewEvent, _bringHandler, true);
        }
        long ticket = _policy.Capture();
        double offset = viewer.VerticalOffset;
        var anchor = FindAnchor(viewer);
        object? anchorData = _root is ListView ? anchor?.DataContext : null;
        double y = anchor?.TranslatePoint(new Point(), viewer).Y ?? 0;
        _automatic = true;
        return new Completion(() =>
        {
            // TextBoxView can finish wrapping at Background; settle after that work, never before it.
            _pending = _root.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                _pending = null;
                if (!_policy.IsCurrent(ticket) || !viewer.IsLoaded) { _automatic = false; return; }
                try
                {
                    var current = anchor is not null && anchor.IsVisible && viewer.IsAncestorOf(anchor) &&
                        (anchorData is null || ReferenceEquals(anchor.DataContext, anchorData)) ? anchor : null;
                    if (current is null && anchorData is not null) current = FindDataAnchor(viewer, anchorData);
                    double target = offset <= 0.5 ? 0 : current is null ? offset : ReadingPositionPolicy.AnchoredOffset(
                        viewer.VerticalOffset, y, current.TranslatePoint(new Point(), viewer).Y);
                    viewer.ScrollToVerticalOffset(target);
                }
                finally { _automatic = false; }
            }));
        });
    }
    private static FrameworkElement? FindDataAnchor(DependencyObject root, object data)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element && (element is ListViewItem || element is Expander) &&
                ReferenceEquals(element.DataContext, data)) return element;
            if (FindDataAnchor(child, data) is { } found) return found;
        }
        return null;
    }
    private FrameworkElement? FindAnchor(ScrollViewer viewer)
    {
        // Realized visuals only: never create offscreen containers or force layout.
        FrameworkElement? best = null; double bestY = double.MaxValue;
        void Visit(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is FrameworkElement element && element.IsVisible && element.ActualHeight > 0 &&
                    (_root is ListView ? element is ListViewItem || element is Expander : element is SelectableText && element.Name.Length > 0))
                {
                    double top = element.TranslatePoint(new Point(), viewer).Y;
                    if (top + element.ActualHeight > 0 && top < viewer.ActualHeight && Math.Abs(top) < bestY)
                    { best = element; bestY = Math.Abs(top); }
                }
                Visit(child);
            }
        }
        Visit(viewer); return best;
    }
    private sealed class Completion(Action complete) : IDisposable
    {
        private Action? _complete = complete;
        public void Dispose() { var action = _complete; _complete = null; action?.Invoke(); }
    }
}
