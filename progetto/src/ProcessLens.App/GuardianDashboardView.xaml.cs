using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProcessLens.App;

public partial class GuardianDashboardView : UserControl
{
    private readonly DispatcherTimer _timer;
    private bool _active;
    private bool _refreshing;
    private bool _lineageReading;
    private int _minutes = 30;
    private DateTime _lastLoggedErrorUtc = DateTime.MinValue;
    private IReadOnlyList<GuardianEventView> _events = Array.Empty<GuardianEventView>();

    public GuardianDashboardView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Unloaded += (_, _) => _timer.Stop();
        Loaded += (_, _) => { if (_active) _timer.Start(); };
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (!active) { _timer.Stop(); return; }
        if (IsLoaded) _timer.Start();
        _ = RefreshAsync();
    }

    private void WindowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowSelector?.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out int minutes))
            _minutes = minutes;
        if (LineageStatusText is not null && _active && IsLoaded)
            LineageStatusText.Text = "Intervallo cambiato: premi Analizza PID per aggiornare la parentela.";
        if (_active && IsLoaded) _ = RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void EventSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyEventFilter();

    private void ApplyEventFilter()
    {
        if (EventsTable is null) return;
        string filter = EventSearchBox?.Text.Trim() ?? string.Empty;
        IReadOnlyList<GuardianEventView> matching = string.IsNullOrEmpty(filter) ? _events : _events.Where(x =>
            x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            x.Kind.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            x.Source.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            x.Pid.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            x.ParentPid.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        EventsTable.ItemsSource = matching;
        EventCountText.Text = $"{matching.Count} / {_events.Count} nel campione";
    }


    private async void AnalyzeLineage_Click(object sender, RoutedEventArgs e) => await AnalyzeLineageAsync();

    private async void UseParent_Click(object sender, RoutedEventArgs e)
    {
        if (ParentsTable.SelectedItem is GuardianParentView selected)
        {
            LineagePidBox.Text = selected.ParentPid.ToString(CultureInfo.InvariantCulture);
            GuardianTabs.SelectedItem = LineageTab;
            await AnalyzeLineageAsync();
        }
        else LineageStatusText.Text = "Seleziona prima una riga in Attivita' e processi padre.";
    }

    private async void ParentsTable_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ParentsTable.SelectedItem is GuardianParentView selected)
        {
            LineagePidBox.Text = selected.ParentPid.ToString(CultureInfo.InvariantCulture);
            GuardianTabs.SelectedItem = LineageTab;
            await AnalyzeLineageAsync();
        }
    }

    private async void ExploreChild_Click(object sender, RoutedEventArgs e) => await ExploreSelectedChildAsync();
    private async void LineageChildrenTable_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => await ExploreSelectedChildAsync();

    private async Task ExploreSelectedChildAsync()
    {
        if (LineageChildrenTable.SelectedItem is not GuardianLineageChild child)
        {
            LineageStatusText.Text = "Seleziona un figlio nella tabella prima di approfondirlo.";
            return;
        }
        LineagePidBox.Text = child.Pid.ToString(CultureInfo.InvariantCulture);
        await AnalyzeLineageAsync(child.EventId);
    }

    private async Task AnalyzeLineageAsync(long? selectedStartEventId = null)
    {
        if (_lineageReading) return;
        if (!int.TryParse(LineagePidBox.Text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int pid) || pid <= 0)
        {
            LineageStatusText.Text = "Inserisci un PID positivo valido.";
            return;
        }
        int requestedMinutes = _minutes;
        _lineageReading = true;
        LineageStatusText.Text = $"Ricostruzione storica del PID {pid} in corso (lettura SQLite)...";
        try
        {
            var report = await Task.Run(() => GuardianLineageReader.Read(
                GuardianDashboardReader.DefaultDatabasePath, pid, requestedMinutes,
                focusStartEventId: selectedStartEventId));
            if (!IsLoaded) return;
            if (requestedMinutes != _minutes)
            {
                LineageStatusText.Text = "Intervallo modificato nel frattempo: riesegui l'analisi.";
                return;
            }
            LineageRootText.Text = $"PID {report.FocusPid} · {report.FocusName} · " +
                $"avvio: {report.StartedLocal} · uscita: {report.ExitedLocal}";
            LineageStatusText.Text = report.FocusEvidence +
                $" · dati letti alle {report.ReadUtc.ToLocalTime():HH:mm:ss}";
            LineageAncestorsTable.ItemsSource = report.Ancestors;
            LineageChildrenTable.ItemsSource = report.Children;
            LineageCoverageText.Text = $"Figli diretti osservati nel periodo ({report.WindowMinutes} min): " +
                $"{report.TotalChildrenInWindow}; visualizzati: {report.Children.Count}" +
                (report.Truncated ? " · risultato parziale, limite di 200 righe." : ".") +
                " Doppio clic su un figlio per approfondire la sua istanza storica (ID avvio). " +
                "Il riutilizzo PID e le uscite non osservate impediscono conferme assolute.";
        }
        catch (Exception ex)
        {
            LineageStatusText.Text = "Ricostruzione non disponibile: " + ex.Message;
            AppDiagnostics.Record("Guardian parent-child analysis failed", ex);
        }
        finally { _lineageReading = false; }
    }

    private async Task RefreshAsync()
    {
        if (!_active || _refreshing || !IsLoaded) return;
        _refreshing = true;
        var targetWindow = _minutes;
        try
        {
            // Disk operations and SQLite queries stay outside the WPF dispatcher.
            var snapshot = await Task.Run(() => GuardianDashboardReader.Read(
                GuardianDashboardReader.DefaultDatabasePath, targetWindow));
            if (!_active) return;
            // An older refresh must never replace data for a newly selected time window.
            if (targetWindow != _minutes) { _ = Dispatcher.BeginInvoke(new Action(() => { _ = RefreshAsync(); })); return; }
            Render(snapshot);
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow - _lastLoggedErrorUtc > TimeSpan.FromMinutes(1))
            {
                AppDiagnostics.Record("Guardian dashboard read failed", ex);
                _lastLoggedErrorUtc = DateTime.UtcNow;
            }
            StatusText.Text = "Guardian: lettura non disponibile — " + ex.Message +
                ". Verifica servizio, database e permessi. Ultimi dati visibili potenzialmente obsoleti.";
            UpdatedText.Text = "Errore di lettura";
        }
        finally { _refreshing = false; }
    }

    private void Render(GuardianDashboardSnapshot value)
    {
        string N(long count) => count.ToString("N0", CultureInfo.CurrentCulture);
        StartedValue.Text = N(value.Started);
        ExitedValue.Text = N(value.Exited);
        EtwValue.Text = N(value.EtwStarts);
        TotalValue.Text = N(value.TotalEvents);
        StorageValue.Text = ((value.DatabaseBytes + value.WalBytes) / 1048576.0).ToString("F1", CultureInfo.CurrentCulture) + " MiB";
        _events = value.Events;
        ApplyEventFilter();
        TopProcessesTable.ItemsSource = value.TopProcesses;
        ActivityTrendList.ItemsSource = value.Trend;
        ParentsTable.ItemsSource = value.Parents;
        ResourcesTable.ItemsSource = value.Resources;
        var previous = DiagnosticsTable.SelectedItem as GuardianDiagnosticPresentation;
        var diagnostics = value.Diagnostics.Select(GuardianDiagnosticPresentation.From).ToArray();
        // Unchanged diagnostics must not clear the row or an in-progress text selection.
        if (DiagnosticsTable.ItemsSource is not GuardianDiagnosticPresentation[] current || !current.SequenceEqual(diagnostics))
        {
            DiagnosticsTable.ItemsSource = diagnostics;
            DiagnosticsTable.SelectedItem = diagnostics.FirstOrDefault(x => x.LocalTime == previous?.LocalTime &&
                x.OriginalSource == previous?.OriginalSource && x.OriginalMessage == previous?.OriginalMessage)
                ?? diagnostics.FirstOrDefault();
        }

        string etwText;
        if (value.LastEtwUtc is null)
            etwText = "nessun evento ETW nel periodo selezionato";
        else
        {
            var age = value.ReadUtc - value.LastEtwUtc.Value;
            etwText = $"ultimo evento ETW {value.LastEtwUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm:ss} ({Math.Max(0, age.TotalSeconds):F0} s fa)";
        }
        StatusText.Text = $"Storico disponibile · {etwText} · finestra {value.WindowMinutes} min · sola lettura";
        UpdatedText.Text = $"Letto alle {value.ReadUtc.ToLocalTime():HH:mm:ss}";
        HealthSummaryText.Text = $"ETW: {N(value.EtwStarts)} avvii, {N(value.EtwExits)} terminazioni  |  " +
            $"diagnostiche coda: {N(value.QueueWarnings)}  |  segnalazioni di perdita SQLite: {N(value.StorageWarnings)}";
        LastEtwText.Text = $"{etwText}. File principale: {value.DatabaseBytes / 1048576.0:F2} MiB; " +
            $"WAL: {value.WalBytes / 1048576.0:F2} MiB. Il WAL non equivale a crescita permanente.";
    }
}
