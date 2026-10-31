using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ProcessLens.Core;
using ProcessLens.Windows;

namespace ProcessLens.App;

public partial class MemoryDashboardView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool _refreshing;
    private bool _active;
    private DateTime? _lastSnapshotStartedUtc;
    private bool _groupByProgram = true;
    private string _categoryFilter = "ALL";
    private WindowsPhysicalMemoryReader.FullMemoryAnalysisSnapshot? _lastSnapshot;

    public event Action<int>? InspectProcessRequested;
    public event Action<MemoryDetailsTarget>? DetailsRequested;

    private sealed record MemoryRow(
        string Name,
        string CategoryLabel,
        string InstancesOrPid,
        string PrivateWorkingSetMiB,
        string SharedWorkingSetMiB,
        string WorkingSetMiB,
        string CommitMiB,
        string SystemShare,
        int TargetPid, string ExecutableName, bool Grouped);

    public MemoryDashboardView()
    {
        InitializeComponent();
        UpdateButtonStyles();
        _timer.Tick += async (_, _) => await RefreshAsync();
        Unloaded += (_, _) => { _timer.Stop(); _active = false; };
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active)
        {
            _timer.Start();
            _ = RefreshAsync();
        }
        else _timer.Stop();
    }

    private async void RefreshMemory_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void ModeGroupedButton_Click(object sender, RoutedEventArgs e)
    {
        _groupByProgram = true;
        UpdateButtonStyles();
        PopulateTable();
    }

    private void ModeSinglePidButton_Click(object sender, RoutedEventArgs e)
    {
        _groupByProgram = false;
        UpdateButtonStyles();
        PopulateTable();
    }

    private void CategoryFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _categoryFilter = tag;
            UpdateButtonStyles();
            PopulateTable();
            TopMemoryProcesses.BringIntoView();
            TopMemoryProcesses.Focus();
        }
    }

    private void InspectSelectedMemory_Click(object sender, RoutedEventArgs e)
    {
        if (TopMemoryProcesses.SelectedItem is MemoryRow row && row.TargetPid > 0)
            InspectProcessRequested?.Invoke(row.TargetPid);
        else if (TopMemoryProcesses.Items.Count > 0 && TopMemoryProcesses.Items[0] is MemoryRow first && first.TargetPid > 0)
            InspectProcessRequested?.Invoke(first.TargetPid);
    }

    private void TopMemoryProcesses_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is DependencyObject origin && ItemsControl.ContainerFromElement(TopMemoryProcesses, origin) is DataGridRow { Item: MemoryRow row })
        {
            DetailsRequested?.Invoke(new(row.TargetPid, row.ExecutableName, row.Grouped, _lastSnapshotStartedUtc));
            e.Handled = true;
        }
    }

    private void ResidualCategory_Click(object sender, RoutedEventArgs e)
    {
        ResidualCategoryDetails.IsExpanded = !ResidualCategoryDetails.IsExpanded;
        if (ResidualCategoryDetails.IsExpanded) ResidualCategoryDetails.BringIntoView();
    }

    private void UpdateButtonStyles()
    {
        var activeBg = (SolidColorBrush)new BrushConverter().ConvertFromString("#DBEAFE")!;
        var activeBorder = (SolidColorBrush)new BrushConverter().ConvertFromString("#3B82F6")!;
        var activeFg = (SolidColorBrush)new BrushConverter().ConvertFromString("#1E3A8A")!;
        var normalBg = Brushes.White;
        var normalBorder = (SolidColorBrush)new BrushConverter().ConvertFromString("#CBD5E1")!;
        var normalFg = (SolidColorBrush)new BrushConverter().ConvertFromString("#334155")!;

        void ApplyStyle(Button btn, bool isSelected)
        {
            btn.Background = isSelected ? activeBg : normalBg;
            btn.BorderBrush = isSelected ? activeBorder : normalBorder;
            btn.Foreground = isSelected ? activeFg : normalFg;
        }

        ApplyStyle(ModeGroupedButton, _groupByProgram);
        ApplyStyle(ModeSinglePidButton, !_groupByProgram);
        ApplyStyle(CatFilterAllButton, _categoryFilter == "ALL");
        ApplyStyle(CatFilterAppsButton, _categoryFilter == "Applicazioni");
        ApplyStyle(CatFilterDevButton, _categoryFilter == "Sviluppo");
        ApplyStyle(CatFilterWinButton, _categoryFilter == "Windows");
        ApplyStyle(CatFilterBgButton, _categoryFilter == "Secondario");
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || !_active) return;
        _refreshing = true;
        try
        {
            var captured = await Task.Run(() =>
            {
                var begin = DateTime.UtcNow;
                return (Begin: begin, Snapshot: WindowsPhysicalMemoryReader.ReadFullAnalysis(int.MaxValue));
            });
            var snapshot = captured.Snapshot;
            _lastSnapshotStartedUtc = captured.Begin;
            if (!_active) return;
            _lastSnapshot = snapshot;

            double percent = snapshot.Memory.UsedPercent;
            var presentation = MemoryDashboardPresentation.ForUsage(percent);
            var accent = (SolidColorBrush)new BrushConverter().ConvertFromString(presentation.AccentHex)!;
            var pale = (SolidColorBrush)new BrushConverter().ConvertFromString(presentation.BackgroundHex)!;

            UsageText.Text = $"{percent:F1}%";
            UsageText.Foreground = accent;
            UsedText.Text = $"{snapshot.Memory.UsedBytes / 1073741824d:F2} GiB in uso ({snapshot.TotalProcessPrivateWorkingSetBytes / 1073741824d:F2} GiB privata processi)";
            AvailableText.Text = $"{snapshot.Memory.AvailableBytes / 1073741824d:F2} GiB";
            TotalText.Text = $"{snapshot.Memory.TotalBytes / 1073741824d:F2} GiB";
            CommitSummaryText.Text = $"Commit privato processi: {snapshot.TotalProcessPrivateCommitBytes / 1073741824d:F2} GiB";
            PressureBadge.Background = pale;
            PressureText.Foreground = accent;
            PressureText.Text = presentation.Label;
            PressureExplanation.Text = presentation.Explanation;
            MemoryBar.Foreground = accent;
            MemoryBar.Value = percent;

            UpdateCategoryCards(snapshot);
            PopulateTable();

            RefreshStatus.Text = $"Ultimo aggiornamento: {DateTime.Now:HH:mm:ss} · Censiti {snapshot.TotalAccessibleProcesses} processi attivi " +
                (snapshot.UnreadableProcessesCount > 0 ? $"({snapshot.UnreadableProcessesCount} processi protetti di sistema non espongono contatori utente)." : "(tutti i processi hanno risposto).");

            // La chiave Run attesta una configurazione di avvio, NON che il programma sia in esecuzione.
            try
            {
                var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
                    "ProcessLensMemoryAlerts", null);
                if (value is string command && !string.IsNullOrWhiteSpace(command))
                {
                    AgentStatus.Text = "Avvio dell'agente configurato per questo utente";
                    AgentExplanation.Text = "Primo avviso a 85%, riarmo sotto 80%. La configurazione non conferma che MemoryAgent sia ora in esecuzione.";
                }
                else
                {
                    AgentStatus.Text = "Avvio automatico non configurato";
                    AgentExplanation.Text = "Avvisi facoltativi: per attivarli usa install-memory-alerts.ps1 con consenso. Nessuna chiusura automatica.";
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                AgentStatus.Text = "Configurazione dell'agente non verificabile";
                AgentExplanation.Text = "La lettura della chiave Run non è riuscita; lo stato di MemoryAgent resta sconosciuto.";
            }
        }
        catch (Exception ex)
        {
            // Conserviamo l'ultimo dato visualizzato ma non lo presentiamo come aggiornato.
            RefreshStatus.Text = "ERRORE: aggiornamento RAM non riuscito; i valori visibili potrebbero essere precedenti. " +
                ex.GetType().Name + ": " + ex.Message;
        }
        finally { _refreshing = false; }
    }

    private void UpdateCategoryCards(WindowsPhysicalMemoryReader.FullMemoryAnalysisSnapshot s)
    {
        ulong totalRam = s.Memory.TotalBytes;
        void FillCard(string catName, TextBox valBlock, TextBox metaBlock)
        {
            var c = s.Categories.FirstOrDefault(x => x.Category == catName);
            if (c is null) return;
            double mib = c.PrivateWorkingSetBytes / 1048576d;
            valBlock.Text = mib >= 1024 ? $"{mib / 1024d:F2} GiB ({mib:F0} MiB)" : $"{mib:F1} MiB";
            double pct = totalRam > 0 ? 100.0 * c.PrivateWorkingSetBytes / totalRam : 0;
            metaBlock.Text = $"{pct:F1}% RAM PC · {c.ProcessCount} proc. ({c.GroupCount} app)";
        }

        FillCard("Applicazioni", CatAppsValueText, CatAppsMetaText);
        FillCard("Sviluppo", CatDevValueText, CatDevMetaText);
        FillCard("Windows", CatWinValueText, CatWinMetaText);
        FillCard("Secondario", CatBgValueText, CatBgMetaText);

        double kernelGib = s.KernelAndSharedPhysicalBytes / 1073741824d;
        double kernelPct = totalRam > 0 ? 100.0 * s.KernelAndSharedPhysicalBytes / totalRam : 0;
        CatKernelValueText.Text = $"{kernelGib:F2} GiB ({s.KernelAndSharedPhysicalBytes / 1048576d:F0} MiB)";
        CatKernelMetaText.Text = $"{kernelPct:F1}% RAM PC · residuo non attribuito ai PID";
    }

    private void PopulateTable()
    {
        if (_lastSnapshot is null) return;
        var s = _lastSnapshot;
        ulong totalRam = s.Memory.TotalBytes;

        static string FormatCatLabel(string cat) => cat == "Secondario" ? "Servizi / Sec." : cat;

        if (_groupByProgram)
        {
            var filtered = _categoryFilter == "ALL"
                ? s.Groups
                : s.Groups.Where(g => g.Category == _categoryFilter).ToArray();

            var rows = filtered.Select(g =>
            {
                long eff = g.EffectiveResidentBytes;
                string share = totalRam > 0 ? $"{(100.0 * eff / totalRam):F1}%" : "N/D";
                string instText = g.InstanceCount == 1
                    ? $"1 proc. (PID {g.LargestPid})"
                    : $"{g.InstanceCount} proc. (max PID {g.LargestPid})";
                return new MemoryRow(
                    g.GroupName,
                    FormatCatLabel(g.Category),
                    instText,
                    g.PrivateAvailable ? (g.TotalPrivateWorkingSetBytes / 1048576d).ToString("F1") : "N/D",
                    g.PrivateAvailable ? (g.SharedWorkingSetBytes / 1048576d).ToString("F1") : "N/D",
                    (g.TotalWorkingSetBytes / 1048576d).ToString("F1"),
                    (g.TotalPrivateCommitBytes / 1048576d).ToString("F1"),
                    share,
                    g.LargestPid, g.ExecutableName, true);
            }).ToArray();

            TopMemoryProcesses.ItemsSource = rows;
            string catDesc = _categoryFilter == "ALL" ? "tutte le categorie" : $"categoria {FormatCatLabel(_categoryFilter)}";
            TopProcessesCaption.Text = $"Vista per Programma ({catDesc}): {rows.Length} programmi attivi, ordinati per RAM privata esclusiva sommata.";
        }
        else
        {
            var filtered = _categoryFilter == "ALL"
                ? s.Processes
                : s.Processes.Where(p => p.Category == _categoryFilter).ToArray();

            var rows = filtered.Select(p =>
            {
                long eff = p.EffectiveResidentBytes;
                string share = totalRam > 0 ? $"{(100.0 * eff / totalRam):F1}%" : "N/D";
                return new MemoryRow(
                    p.FriendlyName,
                    FormatCatLabel(p.Category),
                    $"PID {p.Pid}",
                    p.PrivateWorkingSetAvailable ? (p.PrivateWorkingSetBytes / 1048576d).ToString("F1") : "N/D",
                    p.PrivateWorkingSetAvailable ? (p.SharedWorkingSetBytes / 1048576d).ToString("F1") : "N/D",
                    (p.WorkingSetBytes / 1048576d).ToString("F1"),
                    (p.PrivateCommitBytes / 1048576d).ToString("F1"),
                    share,
                    p.Pid, p.Name, false);
            }).ToArray();

            TopMemoryProcesses.ItemsSource = rows;
            string catDesc = _categoryFilter == "ALL" ? "tutte le categorie" : $"categoria {FormatCatLabel(_categoryFilter)}";
            TopProcessesCaption.Text = $"Vista per Singolo Processo ({catDesc}): {rows.Length} processi, ordinati per RAM privata esclusiva decrescente.";
        }
    }
}
