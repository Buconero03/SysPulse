using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProcessLens.App;
using ProcessLens.Core;
using ProcessLens.Semantics;

internal static class Program
{
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception("FAIL R25 WPF " + label); Console.WriteLine("PASS R25 WPF " + label); }
    static void Set(MainWindow main,string name,object value)=>typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,value);
    static void Filter(MainWindow main)=>typeof(MainWindow).GetMethod("ApplyFilter",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,new object?[]{null,true,false});
    static void Drain()
    {
        var frame=new DispatcherFrame();bool expired=false;
        var deadline=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromSeconds(5)};
        EventHandler onDeadline=(_,_)=>{expired=true;frame.Continue=false;};deadline.Tick+=onDeadline;
        var sentinel=Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));
        try {deadline.Start();Dispatcher.PushFrame(frame);}
        finally {deadline.Stop();deadline.Tick-=onDeadline;sentinel.Abort();}
        if(expired)throw new TimeoutException("R28 fixture layout exceeded five seconds.");
    }
    [STAThread]
    public static void Main(string[] args)
    {
        _=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        if(args.Contains("--window-smoke")) { MainWindowSmoke();Application.Current.Shutdown();return; }
        if(args.Contains("--shell-smoke")) { ShellSmoke();Application.Current.Shutdown();return; }
        if(args.Contains("--scroll-smoke")) { ScrollSmoke();Application.Current.Shutdown();return; }
        var main=new MainWindow(false);var analyzer=new ProcessIntelligenceAnalyzer();var start=DateTime.UtcNow.AddMinutes(-2);
        ProcessSnapshot Chrome(int pid,int parent,int seconds)=>new(){Pid=pid,ParentPid=parent,Name="chrome.exe",FileDescription="Google Chrome",
            ExecutablePath=@"C:\Chrome\chrome.exe",StartTime=start.AddSeconds(seconds),IsDeepInspected=true};
        var root=Chrome(2147483000,1,0);var child=Chrome(2147483001,root.Pid,1);
        var cloud=new ProcessSnapshot{Pid=2147483002,ParentPid=root.Pid,Name="claude.exe",FileDescription="Claude",ExecutablePath=@"C:\Claude\claude.exe",
            StartTime=start.AddSeconds(1),CommandLine="--google",ParentName="Google Chrome",IsDeepInspected=true};
        ProcessRow Row(ProcessSnapshot p)=>new(p,analyzer.Analyze(p));
        var rows=new List<ProcessRow>{Row(root),Row(child),Row(cloud)};Set(main,"_allRows",rows);
        var search=(TextBox)main.FindName("SearchBox");var list=(ListView)main.FindName("ProcessList");
        search.Text="google";Filter(main);Drain();
        Check(list.Items.Count==2 && list.Items.Cast<ProcessRow>().All(x=>x.Name=="chrome.exe"),"identity filter excludes another app mentioning Google in parent and arguments");
        var view=CollectionViewSource.GetDefaultView(list.ItemsSource);
        Check(view.Groups?.Count==1 && ((CollectionViewGroup)view.Groups[0]!).ItemCount==2,"real collection view contains one expandable group with two PID rows");
        var group=(ProcessGroupRow)((CollectionViewGroup)view.Groups![0]!).Name;
        Check(group.Title.Contains("(2)") && group.Members.Select(x=>x.Pid).Distinct().Count()==2,"group count retains individual PID selection targets");
        var stableSource=list.ItemsSource;var stableRow=list.Items[0];
        group.IsExpanded=true;Filter(main);Drain();
        Check(ReferenceEquals(stableSource,list.ItemsSource) && ReferenceEquals(stableRow,list.Items[0]),"R26 metadata refresh retains collection and row identity");
        group=(ProcessGroupRow)((CollectionViewGroup)CollectionViewSource.GetDefaultView(list.ItemsSource).Groups![0]!).Name;
        Check(group.IsExpanded,"expansion survives snapshot/filter rebuild for the same process instance");
        group.IsExpanded=false;Filter(main);Drain();
        group=(ProcessGroupRow)((CollectionViewGroup)CollectionViewSource.GetDefaultView(list.ItemsSource).Groups![0]!).Name;
        Check(!group.IsExpanded,"R26 automatic refresh retains a collapsed group");
        rows[2]=Row(cloud with{WorkingSetBytes=1234});Filter(main);
        Check(search.Text=="google" && list.Items.Count==2 && list.Items.Cast<ProcessRow>().All(x=>x.Name=="chrome.exe"),"asynchronous row replacement cannot bypass current search");
        group=(ProcessGroupRow)((CollectionViewGroup)CollectionViewSource.GetDefaultView(list.ItemsSource).Groups![0]!).Name;
        Check(group.Members.All(x=>rows.Contains(x)),"group members reference the current row instances after a rebuild");
        double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        main.ApplyLiveMemorySamples(new Dictionary<int,LiveMemorySample>{
            [root.Pid]=new(root.Pid,root.StartTime,3*1048576,true,2*1048576,true,9*1048576,true,now,DateTime.UtcNow),
            [child.Pid]=new(child.Pid,child.StartTime,4*1048576,true,3*1048576,true,10*1048576,true,now,DateTime.UtcNow)});
        Check(group.MemoryDisplayMb==5 && group.MemoryLabel=="WS PRIVATO","live RAM updates group sum from existing PID rows");
        search.Text=child.Pid.ToString();Filter(main);
        Check(list.Items.Count==1 && ((ProcessRow)list.Items[0]).Pid==child.Pid,"PID search is exact and exposes only its matching member");
        ((CheckBox)main.FindName("GroupApplicationsCheckBox")).IsChecked=false;Filter(main);
        Check(CollectionViewSource.GetDefaultView(list.ItemsSource).Groups is null,"flat per-PID view remains available");
        int minimize=0,exit=0;main.MinimizeRequested+=()=>minimize++;main.ExitRequested+=()=>exit++;
        ((Button)main.FindName("MinimizeWindowButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((Button)main.FindName("ExitApplicationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(minimize==1 && exit==1,"real header buttons route separate minimize and exit commands");
        Check(((Button)main.FindName("RefreshProcessPortsButton")).Style is not null,"manual port refresh action uses native themed style");
        var systemIconRow=new ProcessRow(new ProcessSnapshot{Pid=4,ParentPid=0,Name="System"},
            analyzer.Analyze(new ProcessSnapshot{Pid=4,ParentPid=0,Name="System"}));
        var idleIconRow=new ProcessRow(new ProcessSnapshot{Pid=0,ParentPid=0,Name="Idle"},
            analyzer.Analyze(new ProcessSnapshot{Pid=0,ParentPid=0,Name="Idle"}));
        var iconGroup=new ProcessGroupRow("owned-icon-fixture","Windows",new[]{systemIconRow},1);
        Check(!ReferenceEquals(systemIconRow.IconSource,idleIconRow.IconSource) &&
            ReferenceEquals(iconGroup.IconSource,systemIconRow.IconSource),"R33 System and Idle differ while the group retains its member icon");
        Check(systemIconRow.IconSource is DrawingImage{IsFrozen:true},"R33 per-component vector icon is frozen and needs no Shell handle");
        Check(iconGroup.IconDescription==systemIconRow.IconDescription &&
            systemIconRow.IconDescription.Contains("Firma digitale non verificata"),"R33 icon tooltip declares generated symbol and unverified signature");
        var genericPixels=new byte[]{0,120,212,255};
        var customPixels=new byte[]{120,212,0,255};
        BitmapSource Pixel(byte[] bytes)=>BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,bytes,4);
        Check(ProcessIconResolver.IsGenericIcon(Pixel(genericPixels),Pixel(genericPixels)),"R28 generic icon comparison uses pixels rather than HICON identity");
        Check(!ProcessIconResolver.IsGenericIcon(Pixel(customPixels),Pixel(genericPixels)),"R28 a distinct original bitmap is retained");
        Check(((Button)main.FindName("WindowLogButton")).Style is not null,"R29 diagnostic action uses the current theme");
        string trace29=main.WindowDiagnostics.Report;
        Check(trace29.Contains("CLICK")&&trace29.Contains("controller collegato=True"),"R29 real header event is recorded in the modeless diagnostic report without disk IO");
        var logContent29=main.WindowDiagnostics.CreateLogContent();
        var box29=logContent29.Children.OfType<TextBox>().Single();
        Check(box29.IsReadOnly&&box29.Text==main.WindowDiagnostics.Report,"R29 log text is selectable and matches the complete report without showing a window");
        var reference31=new ProcessSnapshot{Pid=9131,ParentPid=0,Name="prevhost.exe",StartTime=start};
        Set(main,"_allRows",new List<ProcessRow>{new(reference31,analyzer.Analyze(reference31)),Row(cloud)});
        search.Text="Host delle anteprime";Filter(main);Drain();
        Check(list.Items.Count==1&&((ProcessRow)list.Items[0]).Pid==9131,"R31 search finds the visible dictionary title without unrelated processes");
        Check(((ProcessRow)list.Items[0]).Group!.Title.StartsWith("Host delle anteprime"),"R31 collapsed group uses the same readable host title as the search");
        search.Text="no-result-fixture-31";Filter(main);
        Check(list.Items.Count==0&&((SelectableText)main.FindName("CurrentFilterText")).Text.Contains("Nessun risultato"),"R31 missing search result explains the active query");
        Check(((TextBox)main.FindName("SearchBox")).BorderThickness.Left>0&&main.FindName("SearchSurface") is Border,"R31 search has a visible editor and clickable label surface");
        main.Close();Application.Current.Shutdown();
        var unknown32=new ProcessSnapshot { Pid=910001, ParentPid=0, Name="alpha_tool.exe" };
        var other32=unknown32 with { Pid=910002, Name="beta_worker.exe" };
        var placeholder32=ProcessIconResolver.GetIcon(unknown32);
        Check(placeholder32 is DrawingImage { IsFrozen:true } && !ReferenceEquals(placeholder32,ProcessIconResolver.GetIcon(other32)),"R32 missing originals get distinct frozen initials placeholders");
        Check(ReferenceEquals(placeholder32,ProcessIconResolver.GetIcon(unknown32))&&ProcessIconResolver.IconDescription(unknown32).Contains("Distintivo grafico"),"R32 placeholders are cached and explicitly described as placeholders");
        Check(main.Icon is BitmapSource { IsFrozen:true },"R32 R2 main window reads its optional icon from embedded bytes");
        Check(ProcessLensIcon.Decode(()=>null) is null && ProcessLensIcon.Decode(()=>new byte[7]) is null,"R32 R2 missing or malformed optional icon cannot abort markup construction");
        foreach(var symbol33 in ProcessSymbolCatalog.Entries) {
            foreach(var layer33 in symbol33.Layers) _=Geometry.Parse(layer33.Path); // Must fail the fixture for invalid source paths, rather than accepting the renderer fallback.
            var image33=ProcessSymbolDrawing.Known(symbol33);
            Check(image33.IsFrozen && image33.Drawing.Bounds.Width>0 && image33.Drawing.Bounds.Width<=34 && image33.Drawing.Bounds.Height<=34,"R33 catalogue vector renders and freezes: "+symbol33.Id);
        }
        var bash33=new ProcessRow(new ProcessSnapshot{Pid=910003,ParentPid=0,Name="bash.exe"},analyzer.Analyze(new ProcessSnapshot{Pid=910003,ParentPid=0,Name="bash.exe"}));
        var git33=new ProcessGroupRow("git-fixture","Git",new[]{bash33},1);
        Check(!ReferenceEquals(git33.IconSource,bash33.IconSource)&&git33.IconDescription.Contains("gruppo Git"),"R33 Git group does not inherit Bash icon and explains group illustration");
        Console.WriteLine("R25 WPF fixture complete: no shown window, native process/network reads, tray registration or user clipboard.");
    }
    static void Until(Func<bool> condition,int timeoutMilliseconds=5000)
    {
        var clock=Stopwatch.StartNew();
        while(!condition() && clock.Elapsed<TimeSpan.FromMilliseconds(timeoutMilliseconds))
        {
            var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>frame.Continue=false;timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
        }
        Check(condition(),"owned window reaches expected native shell state within the bounded fixture deadline");
    }
    static void MainWindowSmoke()
    {
        // Real controls/activity callback, 100 synthetic rows. No process or network acquisition.
        var main=new MainWindow(false){Title="ProcessLens — prova finestra R31"};
        var p=new ProcessSnapshot{Pid=2147400000,ParentPid=0,Name="fixture.exe",StartTime=DateTime.UtcNow.AddMinutes(-1),
            PrivateWorkingSetAvailable=true,PrivateWorkingSetBytes=1048576,WorkingSetAvailable=true,WorkingSetBytes=2097152};
        var analyzer=new ProcessIntelligenceAnalyzer();
        var rows=Enumerable.Range(0,100).Select(i=>
        {
            var sample=p with {Pid=p.Pid+i,StartTime=p.StartTime!.Value.AddMilliseconds(i)};
            return new ProcessRow(sample,analyzer.Analyze(sample));
        }).ToList();
        Set(main,"_allRows",rows);Filter(main);var row=rows[0];
        var activity=new List<bool>();var failures=new List<string>();
        using var controller=new TrayWindowController(main,()=>main.Close(),active=>{activity.Add(active);main.SetDashboardWindowVisibility(active);},x=>failures.Add(x),main.WindowDiagnostics);
        main.MinimizeRequested+=controller.RequestHideToTray;
        main.ExitRequested+=()=>{controller.AllowExit();main.Close();};
        main.Show();Drain();Set(main,"_currentDetailRow",row);Set(main,"_selectedPid",p.Pid);
        IntPtr Handle()=>new System.Windows.Interop.WindowInteropHelper(main).Handle;
        for(int pass=0;pass<3;pass++)
        {
            ((Button)main.FindName("MinimizeWindowButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(()=>!main.IsVisible); Check(!main.IsVisible,"R32 header action hides into notification area after the dispatcher turn");
            Drain();Check(activity.Last()==false,"R28 actual MainWindow polling pause completes after native transition");
            if(pass==0) {
                Until(()=>main.WindowDiagnostics.Report.Split(Environment.NewLine).Any(line=>line.Contains(" UI_CHECK:")&&line.Contains("visibile=False")));
                Check(main.WindowDiagnostics.Report.Contains("UI_CHECK_QUEUED"),"R29 delayed probe records queued and executed dispatcher checks for the minimized real window");
            }
            controller.RequestRestore();Until(()=>!IsIconic(Handle())&&main.IsVisible);Drain();
            Check(activity.Last()==true,"R28 real activity callback resumes after explicit restore");
        }
        // Inject documented Shell callback packets into the real owned HWND/hook.
        // This tests routing, not a physical Explorer click.
        long lastTray30=0;
        void PauseBetweenClicks30() {
            if(lastTray30!=0) Until(()=>Stopwatch.GetElapsedTime(lastTray30).TotalMilliseconds>GetDoubleClickTime()+80,checked((int)Math.Max(5000,GetDoubleClickTime()+1000)));
            lastTray30=Stopwatch.GetTimestamp();
        }
        void Tray30(int code,bool packed=true,int icon=1) => SendMessageW(Handle(),0x8000+69,
            new IntPtr(packed?0:icon),new IntPtr(packed?((long)icon<<16)|(uint)code:code));
        PauseBetweenClicks30();Tray30(0x202);Tray30(0x400);Until(()=>!main.IsVisible);Drain();
        Check(!main.IsVisible,"R32 actual HWND v4 mouse release and select produce one tray hide");
        PauseBetweenClicks30();Tray30(0x400);Tray30(0x202);Until(()=>main.IsVisible&&!IsIconic(Handle()));Drain();
        Check(main.IsVisible&&main.WindowState==WindowState.Normal,"R30 actual HWND later select/up restores once");
        PauseBetweenClicks30();Tray30(0x202,false);Until(()=>!main.IsVisible);Drain();
        Check(!main.IsVisible,"R32 actual HWND legacy-layout callback hides after v4 packets");
        PauseBetweenClicks30();Tray30(0x401);Until(()=>main.IsVisible&&!IsIconic(Handle()));Drain();
        Check(main.IsVisible,"R30 actual HWND keyboard activation restores the owned window");
        Tray30(0x202,true,2);Tray30(0x406);Drain();
        Check(!IsIconic(Handle()),"R30 actual HWND wrong icon and hover leave the window unchanged");
        Check(main.WindowDiagnostics.Report.Contains("TRAY_CALLBACK")&&main.WindowDiagnostics.Report.Contains("TRAY_DECISION")&&main.WindowDiagnostics.Report.Contains("duplicato ignorato"),"R30 actual callback hook records packets before filtering and duplicate decisions");
        Check(main.FocusSearch(),"R31 shown search field receives keyboard focus");
        var search31=(TextBox)main.FindName("SearchBox");
        System.Windows.Input.ApplicationCommands.Find.Execute(null,main);
        Check(search31.IsKeyboardFocusWithin,"R31 Find command routes to the actual search editor");
        ((Button)main.FindName("WindowLogButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var log29=main.OwnedWindows.Cast<Window>().Single(x=>x.Title.Contains("Log finestra"));
        Check(main.IsEnabled&&log29.Content is DockPanel,"R29 log view is modeless and leaves the Minimize button usable");
        controller.RequestMinimize();Until(()=>IsIconic(Handle())&&!log29.IsVisible);Drain();
        Check(!log29.IsVisible,"R31 minimizing also hides the owned diagnostic window");
        Tray30(0x205);Tray30(0x7B);Drain();
        var menu31=(ContextMenu)typeof(TrayWindowController).GetField("_menu",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!;
        Until(()=>menu31.IsOpen);
        Check(IsIconic(Handle())&&((MenuItem)menu31.Items[1]).IsEnabled,"R32 right-click menu preserves taskbar minimize and offers notification-area hide");
        ((MenuItem)menu31.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Until(()=>!IsIconic(Handle())&&log29.IsVisible);Drain();
        Check(log29.IsVisible&&log29.WindowState!=WindowState.Minimized,"R31 Show from tray restores the hidden diagnostic window with its owner");
        SendMessageW(Handle(),0x112,new IntPtr(0xF020),IntPtr.Zero);Until(()=>IsIconic(Handle())&&!log29.IsVisible);Drain();
        controller.RequestRestore();Until(()=>!IsIconic(Handle())&&log29.IsVisible&&log29.WindowState!=WindowState.Minimized);Drain();
        Check(log29.WindowState==WindowState.Normal,"R31 native caption minimize also restores the diagnostic child to its saved normal state");
        var child32=new Window { Owner=main, Title="R32 owned child", Width=180, Height=100 };
        child32.Show();Drain();
        controller.RequestHideToTray();Until(()=>!main.IsVisible&&!log29.IsVisible&&!child32.IsVisible);Drain();
        Check(!main.IsVisible&&!log29.IsVisible&&!child32.IsVisible,"R32 tray hide removes main and all visible owned windows");
        Check(main.WindowDiagnostics.Report.Contains("HIDDEN_TO_TRAY"),"R32 report distinguishes notification-area hide from taskbar minimize");
        controller.RequestRestore();Until(()=>main.IsVisible&&log29.IsVisible&&child32.IsVisible);Drain();
        Check(main.IsVisible&&child32.IsVisible,"R32 restore reopens exactly the previously visible owned child");
        child32.Close();
        log29.Close();
        Check(main.WindowDiagnostics.Report.Contains("MINIMIZE_RETURN")&&main.WindowDiagnostics.Report.Contains("STATE"),"R29 actual minimize records controller and WPF transitions");
        SendMessageW(Handle(),0x112,new IntPtr(0xF020),IntPtr.Zero);Until(()=>IsIconic(Handle()));Drain();
        Check(main.WindowState==WindowState.Minimized,"R28 unhandled native caption command reaches Windows and WPF");
        Check(main.WindowDiagnostics.Report.Contains("SC_MINIMIZE"),"R29 native title-bar minimize is observed without intercepting the command");
        controller.RequestRestore();Until(()=>!IsIconic(Handle()));Drain();
        main.WindowState=WindowState.Maximized;
        ((Button)main.FindName("MinimizeWindowButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(()=>!main.IsVisible);Drain();
        controller.RequestRestore();Until(()=>main.IsVisible&&!IsIconic(Handle())&&main.WindowState==WindowState.Maximized);Drain();
        main.Close();Until(()=>IsIconic(Handle()));Drain();
        Check(main.IsVisible&&main.ShowInTaskbar,"R28 X still preserves a taskbar route");
        controller.RequestRestore();Until(()=>!IsIconic(Handle()));Drain();
        Check(failures.Count==0,"R28 Windows confirms every owned fixture minimize");
        ((Button)main.FindName("ExitApplicationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!main.IsVisible,"R28 real Exit button closes the fixture window");
        Console.WriteLine("PASS R28 MainWindow shell gate complete; real owned window only, no process or network collection.");
    }
    static void ShellSmoke()
    {
        // Explicit manual option: only this fixture's own window and tray entry are operated.
        var window=new Window{Title="ProcessLens R25 shell fixture",Width=600,Height=320,Content=new TextBlock{Text="Verifica riduzione/riapertura R25 — finestra fixture",Margin=new Thickness(20)}};
        using var controller=new TrayWindowController(window,()=>window.Close(),_=>{});
        var hwnd=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        window.Show();controller.RequestMinimize();Until(()=>IsIconic(hwnd));
        Check(window.WindowState==WindowState.Minimized,"R26 native and WPF minimized states agree");
        controller.RequestRestore();Until(()=>!IsIconic(hwnd)&&window.IsVisible);
        for(int pass=0;pass<5;pass++)
        {
            SendMessageW(hwnd,0x112,new IntPtr(0xF020),IntPtr.Zero);Until(()=>IsIconic(hwnd));
            controller.RequestRestore();Until(()=>!IsIconic(hwnd)&&window.IsVisible);
        }
        window.WindowState=WindowState.Maximized;controller.RequestMinimize();Until(()=>IsIconic(hwnd));
        controller.RequestRestore();Until(()=>!IsIconic(hwnd)&&window.WindowState==WindowState.Maximized);
        window.Close();Until(()=>IsIconic(hwnd));Check(window.IsVisible,"X retains taskbar restore route");
        controller.RequestRestore();Until(()=>!IsIconic(hwnd));controller.AllowExit();window.Close();
        Check(!window.IsVisible,"explicit exit closes the owned fixture window");
        Console.WriteLine("PASS R25 manual shell fixture; human click on tray and Explorer restart still require manual verification.");
    }
    static T? Find<T>(DependencyObject parent) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);
            if(child is T match)return match;
            if(Find<T>(child) is { } nested)return nested;
        }
        return null;
    }
    static void ScrollSmoke()
    {
        // Explicit visual fixture: real MainWindow XAML, automatic acquisition disabled.
        var main=new MainWindow(false);var analyzer=new ProcessIntelligenceAnalyzer();
        var start=DateTime.UtcNow.AddMinutes(-2);
        ProcessRow Make(int i)
        {
            var p=new ProcessSnapshot{Pid=2147400000+i,ParentPid=0,Name="fixture.exe",StartTime=start.AddSeconds(i),
                ExecutablePath=@"C:\ProcessLens-owned-fixture\fixture.exe",IsDeepInspected=true,
                WorkingSetAvailable=true,WorkingSetBytes=(i+1)*1048576L};
            return new(p,analyzer.Analyze(p));
        }
        var rows=Enumerable.Range(0,150).Select(Make).ToList();Set(main,"_allRows",rows);
        ((CheckBox)main.FindName("GroupApplicationsCheckBox")).IsChecked=false;
        Set(main,"_sortMode","Memoria");Filter(main);main.Show();Drain();main.UpdateLayout();
        var list=(ListView)main.FindName("ProcessList");var viewer=Find<ScrollViewer>(list)!;
        viewer.ScrollToVerticalOffset(600);Drain();double listOffset=viewer.VerticalOffset;
        Check(listOffset>100,"R26 visual fixture reaches middle of real virtualized list");
        var stableSource=list.ItemsSource;var stableOrder=list.Items.Cast<ProcessRow>().ToArray();
        foreach(var row in rows)row.UpdateCpu(null);
        main.RefreshLiveProcessOrder();Drain();
        Check(ReferenceEquals(list.ItemsSource,stableSource)&&list.Items.Cast<ProcessRow>().SequenceEqual(stableOrder),"R26 live rank tick preserves middle-page order");
        var detail=(ScrollViewer)main.FindName("ProcessDetailsScroll");
        detail.ScrollToVerticalOffset(500);Drain();double detailOffset=detail.VerticalOffset;
        Check(detailOffset>100,"R26 visual fixture reaches middle of actual details panel");
        Filter(main);Drain();
        Check(Math.Abs(viewer.VerticalOffset-listOffset)<1 && Math.Abs(detail.VerticalOffset-detailOffset)<1,"R26 metadata refresh preserves both viewports");
        var guard=new AutomaticViewport(detail,()=>detail);
        using(guard.Begin()) { ((SelectableText)main.FindName("DetailOriginOperationText")).Text="Fixture updated"; }
        guard.Cancel();detail.ScrollToVerticalOffset(700);Drain();
        Check(Math.Abs(detail.VerticalOffset-700)<1,"R26 new scroll cancels older queued restoration");
        main.Close();
        Console.WriteLine("PASS R26 visual scrolling fixture; no process inventory, network, clipboard or tray registration.");
    }
    [DllImport("user32.dll")]static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")]static extern IntPtr SendMessageW(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool IsIconic(IntPtr hwnd);
}
