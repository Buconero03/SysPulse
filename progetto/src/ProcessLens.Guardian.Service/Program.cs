using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProcessLens.Guardian.Service;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Guardian requires Windows");
// Diagnostic report command: must be invoked by an authorized administrator.
// Does not install, start, or modify the Windows service.
if (args.Length == 2 && args[0] == "--report")
{
    if (!DateOnly.TryParseExact(args[1], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var requestedDate))
        throw new ArgumentException("Usage: --report YYYY-MM-DD (UTC)");
    string location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProcessLens", "Guardian");
    if (!File.Exists(Path.Combine(location, "guardian.sqlite3")))
        throw new FileNotFoundException("No Guardian database found. Install and start Guardian first.");
    using var storage = new GuardianStore(location);
    Console.WriteLine(storage.DailyReport(requestedDate));
    return;
}
// Analyze an existing Guardian database without starting the Windows collector or modifying SQLite.
// This command may be run from the freshly published executable, before upgrading the service.
if (args.Length >= 1 && args[0] == "--activity")
{
    if (args.Length > 2 || (args.Length == 2 && !int.TryParse(args[1], out _)))
        throw new ArgumentException("Usage: --activity [minutes 5-1440]");
    int minutes = args.Length == 2 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 30;
    if (minutes < 5 || minutes > 1440)
        throw new ArgumentOutOfRangeException(nameof(minutes),"Minutes must be between 5 and 1440.");
    string dbFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProcessLens", "Guardian", "guardian.sqlite3");
    using var analyzer = new GuardianActivityAnalyzer(dbFile);
    Console.WriteLine(analyzer.BuildReport(minutes));
    return;
}
if (args.Length >= 1 && args[0] == "--health")
{
    if (args.Length > 2 || (args.Length == 2 && !int.TryParse(args[1], out _)))
        throw new ArgumentException("Usage: --health [minutes 5-1440]");
    int minutes = args.Length == 2 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 30;
    string dbFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProcessLens", "Guardian", "guardian.sqlite3");
    using var health = new GuardianStorageHealth(dbFile);
    Console.WriteLine(health.BuildReport(minutes));
    return;
}
var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "ProcessLensGuardian")
    .ConfigureServices(services => services.AddHostedService<GuardianWorker>())
    .Build();
await host.RunAsync();
