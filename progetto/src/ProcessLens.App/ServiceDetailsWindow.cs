using System.Windows;
using System.Windows.Controls;
using ProcessLens.Core;

namespace ProcessLens.App;

public sealed class ServiceDetailsWindow : Window
{
    public ServiceDetailsWindow(ServiceSnapshot service, DateTime? catalogUpdatedUtc = null)
    {
        Title = "Servizio — " + service.DisplayName;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        Width = 760; Height = 640; MinWidth = 480; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sections = new StackPanel { Margin = new Thickness(20) };
        sections.Children.Add(new SelectableText { Text = service.DisplayName, FontSize = 22, FontWeight = FontWeights.SemiBold });
        sections.Children.Add(ReportView.Body("Catalogo SCM aggiornato: " + (catalogUpdatedUtc?.ToLocalTime().ToString("g") ?? "data non disponibile") + "\nQuesta finestra mostra uno snapshot di dati e configurazione; non modifica il servizio."));
        void Add(string title, string text) => sections.Children.Add(ReportView.Section(title, () => ReportView.Body(text)));
        Add("01 · Identità e stato", $"Nome tecnico: {service.Name}\nStato: {service.StateLabel}\nAvvio: {service.StartupLabel}\nPID ospitante: {service.ProcessIdLabel}\nTipo: {service.HostType}");
        Add("02 · Descrizione dichiarata", string.IsNullOrWhiteSpace(service.Description) ? "Descrizione non disponibile; la funzione non viene dedotta dal solo nome." : service.Description);
        Add("03 · Configurazione e dipendenze", $"Account: {service.AccountName ?? "N/D"}\nImagePath: {service.BinaryPath ?? "N/D"}\nDipende da: {string.Join(", ", service.Dependencies)}\nGruppi: {string.Join(", ", service.GroupDependencies)}\nLe dipendenze non provano la causa dell’avvio.");
        Add("04 · Interpretazione e limiti", ServiceExplanation.WhyRunning(service) + "\n\n" + ServiceExplanation.Impact(service));
        foreach (var chapter in sections.Children.OfType<Expander>().Take(2)) chapter.IsExpanded = true;
        Content = new ScrollViewer { Content = sections, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
