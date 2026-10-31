using ProcessLens.Core;
using System.Windows.Media;
using System.ComponentModel;

namespace ProcessLens.App;

public sealed class ProcessRow(ProcessSnapshot process, ProcessInsight insight) : INotifyPropertyChanged
{
    public ProcessSnapshot Process { get; private set; } = process;
    public ProcessInsight Insight { get; private set; } = insight;
    internal void UpdateFrom(ProcessRow next)
    {
        Process = next.Process; Insight = next.Insight;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    private static readonly PropertyChangedEventArgs[] CpuChanges =
        new[] { nameof(Process), nameof(CpuPercent), nameof(CpuDisplay), nameof(RuntimeCpuState) }
            .Select(n => new PropertyChangedEventArgs(n)).ToArray();
    public event PropertyChangedEventHandler? PropertyChanged;
    public void UpdateCpu(LiveCpuSample? sample)
    {
        if (sample?.Matches(Process) != true && !Process.CpuSampleAvailable && !Process.CpuCumulativeAvailable) return;
        Process = sample?.Matches(Process) == true ? sample.Apply(Process) : LiveCpuSample.Unavailable(Process);
        foreach (var change in CpuChanges) PropertyChanged?.Invoke(this, change);
    }
    private static readonly PropertyChangedEventArgs[] MemoryChanges =
        new[] { nameof(Process), nameof(WorkingSetMb), nameof(MemoryDisplayMb), nameof(MemoryDisplayText),
            nameof(MemoryDisplayLabel), nameof(MemoryShortLabel), nameof(MemoryDescription), nameof(MemoryAvailable) }
            .Select(n => new PropertyChangedEventArgs(n)).ToArray();
    public bool MemoryAvailable => Process.PrivateWorkingSetAvailable || Process.WorkingSetAvailable;
    public void UpdateMemory(LiveMemorySample? sample)
    {
        var next = sample is null ? LiveMemorySample.Unavailable(Process) : sample.Apply(Process);
        if (ReferenceEquals(next, Process)) return;
        Process = next;
        foreach (var change in MemoryChanges) PropertyChanged?.Invoke(this, change);
    }
    private static readonly PropertyChangedEventArgs[] NetworkChanges =
        new[] { nameof(Process), nameof(NetworkBytesPerSecond), nameof(NetworkDisplayText),
            nameof(NetworkShortLabel), nameof(NetworkDescription), nameof(NetworkSortScore) }
            .Select(n => new PropertyChangedEventArgs(n)).ToArray();
    public void UpdateNetwork(LiveNetworkSample? sample)
    {
        var next = sample is null ? LiveNetworkSample.Unavailable(Process) : sample.Apply(Process);
        if (ReferenceEquals(next, Process)) return;
        Process = next;
        foreach (var change in NetworkChanges) PropertyChanged?.Invoke(this, change);
    }
    public double NetworkBytesPerSecond => Process.NetworkBytesPerSecond;
    public string NetworkDisplayText => Process.NetworkDisplayText;
    public string NetworkShortLabel => Process.NetworkShortLabel;
    public string NetworkDescription => Process.NetworkDescription;
    public double NetworkSortScore => Process.NetworkBytesPerSecond +
        (Process.PublicRemoteConnectionCount * 100d) +
        (Process.TcpEstablishedCount * 10d) +
        (Process.TcpListeningCount * 2d) +
        Process.UdpEndpointCount;
    public ProcessGroupRow? Group { get; internal set; }
    public string MemberLabel => (Process.ServiceDisplayNames.Count > 0 || Process.ServiceNames.Count > 0
        ? WindowsHostReference.Services(Process)
        : WindowsHostReference.Find(Process.Name) is { } host
            ? (SysPulseLocalization.IsEnglish ? $"By name: {SysPulseLocalization.Translate(host.Title)}" : $"Dal nome: {host.Title}")
            : SysPulseLocalization.Translate(Insight.Role)) + $" · PID {Pid}";
    public string Name => Process.Name;
    public ImageSource? IconSource => ProcessIconResolver.GetIcon(Process);
    public string IconDescription => ProcessIconResolver.IconDescription(Process);
    public string FriendlyName => Insight.FriendlyName;
    public string Category => Insight.Category; // Filtro interno: valore storico, invariato.
    public string CategoryLabel => ProcessTerminology.CategoryLabel(Insight.Category);
    public string OwnerApp => Insight.OwnerApp;
    public int Pid => Process.Pid;
    public double CpuPercent => Process.CpuPercent;
    public string CpuDisplay => Process.CpuSampleAvailable
        ? ProcessDisplayFormatter.Percent(Process.CpuPercent)
        : (SysPulseLocalization.IsEnglish ? "N/A" : "N/D");
    public double WorkingSetMb => Process.WorkingSetMb;
    public double MemoryDisplayMb => Process.MemoryDisplayMb;
    public string MemoryDisplayText => Process.MemoryDisplayText;
    // Labels for the narrow process list. The exact counter name stays in MemoryDisplayLabel.
    public string MemoryDisplayLabel => Process.MemoryDisplayLabel;
    public string MemoryShortLabel => CompactMemoryLabel(Process);
    public static string CompactMemoryLabel(ProcessSnapshot process)
    {
        if (SysPulseLocalization.IsEnglish)
        {
            return process.PrivateWorkingSetAvailable
                ? "PRIVATE WS"
                : process.WorkingSetAvailable ? "RESIDENT RAM" : "RAM N/A";
        }
        string label = process.PrivateWorkingSetAvailable
            ? "WS PRIVATO"
            : process.WorkingSetAvailable ? "RAM RESIDENTE" : "RAM N/D";
        return SysPulseLocalization.Translate(label);
    }
    public string MemoryDescription => Process.MemoryDescription;
    public string Parent => Process.ParentName ?? (SysPulseLocalization.IsEnglish ? "N/A" : "N/D");
    public string Activity => SysPulseLocalization.Translate(Insight.Summary);
    public string Role => SysPulseLocalization.Translate(Insight.Role);
    public string ParentContext => SysPulseLocalization.CurrentLanguage == AppLanguage.English
        ? $"{(Process.ParentName is null ? "Origin not available" : $"Started by: {Process.ParentName}")} · PID {Pid}"
        : $"{(Process.ParentName is null ? "Origine non disponibile" : $"Avviato da: {Process.ParentName}")} · PID {Pid}";
    public string StartTimeLabel => Process.StartTime.HasValue
        ? (SysPulseLocalization.CurrentLanguage == AppLanguage.English
            ? $"Started: {ProcessStartFormatter.FormatStart(Process.StartTime)}"
            : $"Avvio: {ProcessStartFormatter.FormatStart(Process.StartTime)}")
        : (SysPulseLocalization.CurrentLanguage == AppLanguage.English ? "Start time: not available" : "Avvio: non disponibile");
    public string UptimeLabel => Process.StartTime.HasValue
        ? (SysPulseLocalization.CurrentLanguage == AppLanguage.English
            ? $"Running for {ProcessStartFormatter.FormatElapsed(Process.StartTime)}"
            : $"In esecuzione da {ProcessStartFormatter.FormatElapsed(Process.StartTime)}")
        : (SysPulseLocalization.CurrentLanguage == AppLanguage.English ? "Uptime not available" : "Durata non disponibile");
    public string DetectionLabel => SysPulseLocalization.TerminologyFunctionLabel(Insight.RoleConfidencePercent);
    public int Confidence => Insight.ConfidencePercent;
    public string CloseRisk => SysPulseLocalization.Translate(Insight.CloseRisk);
    public string Framework => Insight.Framework;
    public string RoleConfidenceLabel => Insight.RoleConfidenceLabel;
    public string RuntimeCpuState => !Process.CpuSampleAvailable
        ? (SysPulseLocalization.IsEnglish ? "CPU N/A" : "CPU N/D")
        : CpuPercent switch
        {
            >= 10 => SysPulseLocalization.IsEnglish ? "High CPU usage" : "Uso CPU elevato",
            >= 2 => SysPulseLocalization.IsEnglish ? "Moderate CPU usage" : "Uso CPU moderato",
            >= 0.2 => SysPulseLocalization.IsEnglish ? "Light CPU usage" : "Uso CPU contenuto",
            _ => SysPulseLocalization.IsEnglish ? "Minimal CPU usage" : "Uso CPU minimo"
        };
}
