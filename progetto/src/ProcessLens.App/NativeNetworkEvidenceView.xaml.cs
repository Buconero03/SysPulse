using System.Windows;
using System.Windows.Controls;
using ProcessLens.Core;
using ProcessLens.Windows;

namespace ProcessLens.App;

public partial class NativeNetworkEvidenceView : UserControl
{
    private readonly INativeNetworkEvidenceSource _source;
    private readonly NetworkActivityJournal _journal;
    private bool _active, _busy;
    private int _generation;
    private CancellationTokenSource? _operation;
    private NativeNetworkSnapshot? _snapshot;
    public NativeNetworkEvidenceView() : this(new WindowsNativeNetworkEvidence()) { }
    internal NativeNetworkEvidenceView(INativeNetworkEvidenceSource source, NetworkActivityJournal? journal = null)
    { _source = source; _journal = journal ?? new(); InitializeComponent(); NativeNetworkSetupText.Text = NetworkEvidenceStatus.Setup; UpdateButtons(); }
    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (!active)
        {
            _generation++; _operation?.Cancel(); _snapshot = null;
            NativeNetworkEventsTable.ItemsSource = null;
            NativeNetworkStatusText.Text = "Campione da aggiornare.";
            NativeNetworkCoverageText.Text = "Configurazione audit: N/D";
            NativeNetworkSelectionText.Text = "Seleziona un evento dopo la lettura.";
            NativeEventCountsText.Text = "Conteggi N/D · campione precedente rilasciato";
            NativeEventEmptyText.Text = "Campione da rileggere. Premi Leggi eventi Windows."; NativeEventEmptyBorder.Visibility = Visibility.Visible;
        }
        UpdateButtons();
    }
    internal async Task RefreshAsync()
    {
        if (!_active || _busy) return;
        _busy = true; int generation = _generation;
        using var operation = new CancellationTokenSource(); _operation = operation;
        _snapshot = null; NativeNetworkEventsTable.ItemsSource = null;
        NativeNetworkStatusText.Text = "Lettura del registro Security…";
        NativeNetworkCoverageText.Text = "Configurazione audit: lettura in corso.";
        NativeNetworkSelectionText.Text = "Seleziona un evento dopo la lettura.";
        NativeEventCountsText.Text = "Conteggi in attesa"; NativeEventEmptyText.Text = "Lettura del registro Security in corso…";
        NativeEventEmptyBorder.Visibility = Visibility.Visible;
        var options = new NetworkEventReadOptions(NativeEventWindowBox.SelectedIndex == 1 ? 60 : 15, NativePacketDropsBox.IsChecked == true);
        _journal.Add("Security", "INIZIO", $"Ultimi {options.Minutes} min · scarti5152={options.IncludePacketDrops} · massimo 256 record; budget enumerazione 3 s / 1 Mi caratteri XML.");
        var started = System.Diagnostics.Stopwatch.StartNew();
        UpdateButtons();
        try
        {
            var result = await Task.Run(() => _source is INativeNetworkEvidenceQuerySource query ? query.Capture(options, operation.Token) : _source.Capture(operation.Token), operation.Token);
            if (!_active || generation != _generation || operation.IsCancellationRequested) { _journal.Add("Security", "SCARTO", "Risultato tardivo o annullato non pubblicato dopo il cambio di scheda."); return; }
            _snapshot = result;
            NativeEventCountsText.Text = NetworkReadPresentation.EventCounts(result);
            NativeEventEmptyText.Text = !result.Available ? "Registro non disponibile. Conteggi N/D: consulta Fonti e log." :
                "Nessun evento compatibile trovato. Controlla intervallo e audit in Fonti; non è un verdetto sulla sicurezza.";
            NativeEventEmptyBorder.Visibility = result.Events.Count == 0 || !result.Available ? Visibility.Visible : Visibility.Collapsed;
            _journal.Add("Security", "ESITO", $"Disponibile={result.Available} · parziale={result.Partial} · esaminati={result.ScannedEvents} · mostrati={result.Events.Count} · XML non interpretabili={result.MalformedEvents} · {started.Elapsed.TotalMilliseconds:0} ms. " + result.Note);
            foreach (var audit in result.Audit) _journal.Add("Audit Windows", "COPERTURA", audit.Text);
            NativeNetworkEventsTable.ItemsSource = result.Available ? result.Events : null;
            NativeNetworkStatusText.Text = NetworkEvidenceStatus.Summary(result);
            NativeNetworkCoverageText.Text = NetworkEvidenceStatus.Action(result) + "\n\n" +
                string.Join("\n", result.Audit.Select(a => a.Text)) + "\n" + result.Note +
                $"\nIntervallo UTC: {result.SinceUtc:O} → {result.CapturedUtc:O}\nRecord esaminati: {result.ScannedEvents}; XML non interpretabili: {result.MalformedEvents}; caratteri XML: {result.XmlCharacters}.";
            NativeNetworkSetupText.Text = NetworkEvidenceStatus.Setup;
        }
        catch (OperationCanceledException) { _journal.Add("Security", "ANNULLAMENTO", "Lettura interrotta; nessun nuovo risultato pubblicato."); }
        catch (Exception ex)
        {
            if (_active && generation == _generation)
            { NativeNetworkStatusText.Text = "Lettura non disponibile · conteggi N/D"; NativeNetworkCoverageText.Text = ex.Message; NativeEventEmptyText.Text = "Lettura non riuscita. Consulta Fonti e log."; NativeEventCountsText.Text = "Conteggi N/D"; _journal.Add("Security", "ERRORE", ex.Message); }
        }
        finally
        {
            if (ReferenceEquals(_operation, operation)) _operation = null;
            _busy = false; UpdateButtons();
        }
    }
    private void UpdateButtons()
    { NativeEventWindowBox.IsEnabled = NativePacketDropsBox.IsEnabled = _active && !_busy && _source is INativeNetworkEvidenceQuerySource; ReadNativeNetworkButton.IsEnabled = _active && !_busy; CopyNativeNetworkButton.IsEnabled = _active && !_busy && _snapshot is not null; }
    private async void Read_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void Event_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NativeNetworkSelectionText is null) return;
        NativeNetworkSelectionText.Text = NativeNetworkEventsTable.SelectedItem is NativeNetworkEvent row
            ? row.Details : "Seleziona un evento per leggere i dettagli.";
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    { if (_snapshot is not null && !_busy) ClipboardText.Copy(this, NativeNetworkEvidence.Report(_snapshot)); }
}
