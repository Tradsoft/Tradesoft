using Microsoft.Extensions.Logging;
using TradeSoft.Core.Pipeline;
using TradeSoft.Engine;
using TradeSoft.Engine.Strategies;
using TradeSoft.Engine.Management;
using TradeSoft.Infrastructure.IO;

class Program
{
    static void Main(string[] args)
    {
        // ---------------------------------------------------------
        // 1. Configuration du Logger
        // ---------------------------------------------------------
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        // ---------------------------------------------------------
        // 2. Configuration du Chemin des Données
        // ---------------------------------------------------------
        string[] cheminsPossibles = new[]
        {
            // Louis
            @"C:\Users\FlowUP\source\repos\GitHub\Tradesoft\Data\EURUSD",
            
            // Chemin relatif standard (si le dossier Data est à la racine de la solution)
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD")),
            
        };

        string? dossierData = cheminsPossibles.FirstOrDefault(path => Directory.Exists(path));

        if (string.IsNullOrEmpty(dossierData))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Aucune donnée trouvée !");
            Console.WriteLine("Chemins testés :");
            foreach (var p in cheminsPossibles) Console.WriteLine($" - {p}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"✅ Dossier de données validé : {dossierData}");

        try
        {
            // ---------------------------------------------------------
            // 3. Initialisation des Composants (Dependency Injection manuelle)
            // ---------------------------------------------------------

            var providerLogger = loggerFactory.CreateLogger<MultiFileTickProvider>();
            var provider = new MultiFileTickProvider(dossierData, providerLogger);

            // On démarre avec 100,000$
            var portfolioManager = new PortfolioManager(100_000);

            Handler strategy = new RandomStrategy();
            Handler broker = new TradeSoft.Engine.Execution.SimulatedBroker();

            strategy.SetNext(broker);
            broker.SetNext(portfolioManager);

            var runnerLogger = loggerFactory.CreateLogger<BacktestRunner>();
            var runner = new BacktestRunner(provider, strategy, runnerLogger);

            // ---------------------------------------------------------
            // 4. Lancement du Backtest
            // ---------------------------------------------------------
            var start = new DateTime(2015, 01, 01);
            var end = new DateTime(2015, 02, 01);

            runner.Run("EURUSD", start, end);

            var result = portfolioManager.CurrentPortfolio;
            double profit = result.Equity - result.InitialCash;
            double returnPct = (profit / result.InitialCash) * 100;

            Console.WriteLine("\n========================================");
            Console.WriteLine("          RESULTATS DU BACKTEST         ");
            Console.WriteLine("========================================");
            Console.WriteLine($"Capital Initial : {result.InitialCash:C0}");
            Console.WriteLine($"Capital Final   : {result.Equity:C2}");
            Console.WriteLine($"PnL Net         : {profit:C2} ({returnPct:F2}%)");
            Console.WriteLine($"Position Finale : {result.Position} lots");
            Console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nCRASH DU PROGRAMME :\n{ex}");
            Console.ResetColor();
        }
    }
}