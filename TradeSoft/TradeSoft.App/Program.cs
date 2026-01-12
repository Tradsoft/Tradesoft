using Microsoft.Extensions.Logging;
using TradeSoft.Core.Pipeline;
using TradeSoft.Engine;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Strategies;
using TradeSoft.Infrastructure.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;
using TradeSoft.Infrastructure.Processing;

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

            //Alejandro
            @"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",

            ///Pierre-Ugo
            @"C:\Users\Pc\Bureau\Projet\Data\EURUSD",

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

            // A. On crée les maillons (dont ton nouvel agrégateur)
            Handler aggregator = new BarAggregator();

            // Menu de sélection de stratégie (et paramètres si nécessaire)
            Handler strategy = CreateStrategyFromMenu();

            Handler broker = new SimulatedBroker();

            // B. On définit l'ordre du pipeline (L'agrégateur est en premier !)
            aggregator.SetNext(strategy);
            strategy.SetNext(broker);
            broker.SetNext(portfolioManager);

            var runnerLogger = loggerFactory.CreateLogger<BacktestRunner>();

            // C. IMPORTANT : On donne "aggregator" au runner au lieu de "strategy"
            var runner = new BacktestRunner(provider, aggregator, runnerLogger);

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

    private static Handler CreateStrategyFromMenu()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("       MENU - CHOIX STRATEGIE           ");
            Console.WriteLine("========================================");
            Console.WriteLine("1) RSI (paramétrable)");
            Console.WriteLine("2) RandomStrategy");
            Console.WriteLine("3) RSI + RandomStrategy (router)");
            Console.WriteLine("========================================");

            int choice = ReadInt("Sélection (1-3) : ", min: 1, max: 3);

            switch (choice)
            {
                case 1:
                    return CreateRsiFromMenu();

                case 2:
                    return new RandomStrategy();

                case 3:
                    return new StrategyRouter(
                        CreateRsiFromMenu(),
                        new RandomStrategy()
                    );

                default:
                    Console.WriteLine("Choix invalide.");
                    break;
            }
        }
    }

    private static Handler CreateRsiFromMenu()
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("         PARAMETRES RSI                 ");
        Console.WriteLine("========================================");

        int period = ReadInt("Period (défaut 14) : ", defaultValue: 14, min: 2, max: 10_000);
        double oversold = ReadDouble("Oversold (défaut 30) : ", defaultValue: 30, minExclusive: 0, maxExclusive: 100);
        double overbought = ReadDouble("Overbought (défaut 70) : ", defaultValue: 70, minExclusive: 0, maxExclusive: 100);
        double quantity = ReadDouble("Quantity (défaut 1.0) : ", defaultValue: 1.0, minExclusive: 0, maxExclusive: double.MaxValue);

        return new RSI(period, oversold, overbought, lot: 1.0, cooldown: TimeSpan.FromMinutes(1));

    }

    private static int ReadInt(string prompt, int? defaultValue = null, int? min = null, int? max = null)
    {
        while (true)
        {
            Console.Write(prompt);

            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            if (!int.TryParse(input, out int value))
            {
                Console.WriteLine("Veuillez saisir un nombre entier valide.");
                continue;
            }

            if (min.HasValue && value < min.Value)
            {
                Console.WriteLine($"Valeur invalide (min={min.Value}).");
                continue;
            }

            if (max.HasValue && value > max.Value)
            {
                Console.WriteLine($"Valeur invalide (max={max.Value}).");
                continue;
            }

            return value;
        }
    }

    private static double ReadDouble(string prompt, double? defaultValue = null, double? minExclusive = null, double? maxExclusive = null)
    {
        while (true)
        {
            Console.Write(prompt);

            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            if (!double.TryParse(input, out double value))
            {
                Console.WriteLine("Veuillez saisir un nombre valide (ex: 1.5).");
                continue;
            }

            if (minExclusive.HasValue && value <= minExclusive.Value)
            {
                Console.WriteLine($"Valeur invalide (> {minExclusive.Value}).");
                continue;
            }

            if (maxExclusive.HasValue && value >= maxExclusive.Value)
            {
                Console.WriteLine($"Valeur invalide (< {maxExclusive.Value}).");
                continue;
            }

            return value;
        }
    }
}