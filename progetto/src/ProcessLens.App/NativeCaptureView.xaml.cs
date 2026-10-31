using System.Windows;
using System.Windows.Controls;
using ProcessLens.Core;
using ProcessLens.Windows;
namespace ProcessLens.App;

public partial class NativeCaptureView : UserControl
{
    private readonly INativeCaptureClient _client;
    private readonly NetworkActivityJournal _journal;
    private bool _active, _busy;
    private int _generation;
    private string _report = "", _lastDiagnostics = "";
    private CancellationTokenSource? _operation;
    public NativeCaptureView() : this(new WindowsCompatibleCapture()) { }
    internal NativeCaptureView(INativeCaptureClient client, NetworkActivityJournal? journal = null) { _client = client; _journal = journal ?? new(); InitializeComponent(); UpdateButtons(); }
    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (!active)
        {
            _generation++; _operation?.Cancel(); Clear(); NativeCaptureSourcesBox.ItemsSource = null;
            NativeCaptureStatusText.Text = _busy ? "Lettura interrotta. Elenca di nuovo le sorgenti o leggi le reti Wi-Fi." : "Elenca di nuovo le sorgenti o leggi le reti Wi-Fi.";
            NativeCaptureNoteText.Text = _lastDiagnostics;
            if (!_busy) _client.Release();
        }
        UpdateButtons();
    }
    private void Clear()
    { _report = ""; NativePacketsTable.ItemsSource = null; WifiBssTable.ItemsSource = null; NativeCaptureNoteText.Text = ""; NativeCaptureSelectionText.Text = "Seleziona una riga per leggere i dati e il loro significato."; }
    internal async Task DiscoverAsync() => await RunAsync(0);
    internal async Task CaptureAsync() => await RunAsync(1);
    internal async Task ReadWifiAsync() => await RunAsync(2);
    private async Task RunAsync(int action)
    {
        if (!_active || _busy) return;
        var selected = NativeCaptureSourcesBox.SelectedItem as NativeCaptureSource;
        if (action == 1 && selected is null) return;
        _busy = true; int generation = _generation;
        using var operation = new CancellationTokenSource(); _operation = operation;
        Clear(); if (action == 0) NativeCaptureSourcesBox.ItemsSource = null;
        NativeCaptureStatusText.Text = action == 0 ? "Elenco delle sorgenti Windows…" : action == 1 ? "Cattura in corso · massimo 20 s…" : "Lettura della cache radio Windows…";
        string operationName = action == 0 ? "Sorgenti Windows" : action == 1 ? "Cattura locale" : "Cache radio Wi-Fi";
        var started = System.Diagnostics.Stopwatch.StartNew();
        _journal.Add(operationName, "INIZIO", action == 1 ? "Sorgente: " + selected!.Name + "; massimo 20 s, 2048 osservazioni, 128 byte campionati." :
            action == 0 ? "Enumerazione manuale delle sorgenti; nessuna cattura avviata." : "Lettura cache BSS; nessuna scansione forzata o modalità monitor.");
        UpdateButtons();
        try
        {
            if (action == 0)
            {
                var result = await Task.Run(() => _client.Discover(operation.Token), operation.Token);
                if (!_active || generation != _generation || operation.IsCancellationRequested) { _journal.Add(operationName, "SCARTO", "Risultato tardivo o annullato non pubblicato."); return; }
                NativeCaptureSourcesBox.ItemsSource = result.Available ? result.Sources : null;
                NativeCaptureSourcesBox.SelectedIndex = -1;
                NativeCaptureStatusText.Text = result.Available ? $"{result.Sources.Count} sorgenti Windows · seleziona una sorgente." : "Nessuna sorgente di cattura disponibile · leggi il motivo sotto";
                _report = "SORGENTI DI CATTURA WINDOWS\n" + NativeCaptureStatusText.Text + "\n" + result.Note + "\n" + string.Join("\n", result.Sources.Select(s => s.Name));
                NativeCaptureNoteText.Text = result.Note + "\nIP locale: richiede amministratore e copre IPv4 del PC. PktMon: solo se le API sono esposte da Windows. Selezionare una sorgente non avvia una cattura.";
            }
            else if (action == 1)
            {
                var result = await Task.Run(() => _client.Capture(selected!, operation.Token), operation.Token);
                if (!_active || generation != _generation) { _journal.Add(operationName, "SCARTO", "Risultato tardivo non pubblicato."); return; }
                NativePacketsTable.ItemsSource = result.Available ? result.Rows : null; NativeResultTabs.SelectedIndex = 0;
                _report = NativeCaptureEvidence.Report(result);
                NativeCaptureStatusText.Text = result.Available ? $"{result.Rows.Count} osservazioni (non connessioni uniche) · " + (result.Partial ? "cattura parziale" : "raccolta terminata") : "Cattura non disponibile · conteggio N/D";
                NativeCaptureNoteText.Text = result.Note + (result.Available && result.LossCountsAvailable ? $"\nPerdite: scrittura {result.MissedWrites}, lettura {result.MissedReads}.\n" : "\nPerdite: N/D\n") + NativeCaptureEvidence.Limits;
            }
            else
            {
                var result = await Task.Run(() => _client.ReadWifi(operation.Token), operation.Token);
                if (!_active || generation != _generation || operation.IsCancellationRequested) { _journal.Add(operationName, "SCARTO", "Risultato tardivo o annullato non pubblicato."); return; }
                WifiBssTable.ItemsSource = result.Available ? result.Rows : null; NativeResultTabs.SelectedIndex = 1;
                _report = NativeCaptureEvidence.Report(result);
                NativeCaptureStatusText.Text = result.Available ? $"{result.Rows.Count} voci radio nella cache (non client Wi-Fi)" + (result.Partial ? " · lettura parziale" : "") : "Wi-Fi non disponibile · voci N/D";
                NativeCaptureNoteText.Text = result.Note + "\n" + NativeCaptureEvidence.WifiLimits;
            }
            _journal.Add(operationName, "ESITO", NativeCaptureStatusText.Text + $" · {started.Elapsed.TotalMilliseconds:0} ms. " + NativeCaptureNoteText.Text);
            RememberDiagnostics();
        }
        catch (OperationCanceledException)
        { _journal.Add(operationName, "ANNULLAMENTO", "Richiesta interrotta; eventuali risultati non pubblicati sono esclusi."); if (_active && generation == _generation) { NativeCaptureStatusText.Text = "Lettura interrotta."; RememberDiagnostics(); } }
        catch (Exception ex)
        { if (_active && generation == _generation) { NativeCaptureStatusText.Text = "Operazione non disponibile · conteggio N/D"; NativeCaptureNoteText.Text = ex.Message; _journal.Add(operationName, "ERRORE", ex.Message); RememberDiagnostics(); } }
        finally
        {
            if (ReferenceEquals(_operation, operation)) _operation = null;
            if (generation != _generation || !_active) { _client.Release(); NativeCaptureSourcesBox.ItemsSource = null; }
            _busy = false; UpdateButtons();
        }
    }
    private void RememberDiagnostics()
    {
        _lastDiagnostics = "Ultimo esito UTC: " + DateTime.UtcNow.ToString("O") + "\n" + NativeCaptureStatusText.Text + "\n" +
            new string(NativeCaptureNoteText.Text.Take(4096).ToArray()) + "\nLe tabelle vengono rilasciate quando lasci la sezione.";
    }
    private void UpdateButtons()
    {
        if (StartNativeCaptureButton is null) return;
        DiscoverNativeCaptureButton.IsEnabled = _active && !_busy; ReadWifiBssButton.IsEnabled = _active && !_busy;
        StartNativeCaptureButton.IsEnabled = _active && !_busy && NativeCaptureSourcesBox.SelectedItem is NativeCaptureSource;
        NativeCaptureSourcesBox.IsEnabled = _active && !_busy;
        StopNativeCaptureButton.IsEnabled = _active && _busy;
        CopyNativeCaptureButton.IsEnabled = _active && !_busy && (_report.Length > 0 || _lastDiagnostics.Length > 0);
    }
    private void Observation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NativeCaptureSelectionText is null) return;
        NativeCaptureSelectionText.Text = (sender as DataGrid)?.SelectedItem switch {
            NativePacketRow packet => NetworkReadPresentation.PacketDetails(packet),
            WifiBssRow wifi => NetworkReadPresentation.WifiDetails(wifi),
            _ => "Seleziona una riga per leggere i dati e il loro significato." };
    }
    private void Source_Changed(object sender, SelectionChangedEventArgs e) => UpdateButtons();
    private async void Discover_Click(object sender, RoutedEventArgs e) => await DiscoverAsync();
    private async void Capture_Click(object sender, RoutedEventArgs e) => await CaptureAsync();
    private async void Wifi_Click(object sender, RoutedEventArgs e) => await ReadWifiAsync();
    private void Stop_Click(object sender, RoutedEventArgs e) => _operation?.Cancel();
    private void Copy_Click(object sender, RoutedEventArgs e) { if (!_busy && (_report.Length > 0 || _lastDiagnostics.Length > 0)) ClipboardText.Copy(this, _report.Length > 0 ? _report : _lastDiagnostics); }
}
