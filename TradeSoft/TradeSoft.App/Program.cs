using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.Pipeline;
using TradeSoft.Engine;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Strategies;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

class Program
{
    static void Main(string[] args)
    {
        // =========================================================
        // 1. CONFIGURATION DU LOGGER
        // =========================================================
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        // =========================================================
        // 2. CONFIGURATION DES CHEMINS DE DONNÉES
        // =========================================================
        string[] cheminsPossibles = new[]
        {
            // Louis
            @"C:\Users\FlowUP\source\repos\GitHub\Tradesoft\Data\EURUSD",
            // Alejandro
            @"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",
            // Pierre-Ugo
            @"C:\Users\Pc\Bureau\Projet\Data\EURUSD",
            // Inès 
            @"C:\Users\inesm\OneDrive\Documents\M2\Tradesoft\Data\EURUSD",
            // Relatif standard
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD")),
        };

        string? dossierData = cheminsPossibles.FirstOrDefault(path => Directory.Exists(path));

        if (string.IsNullOrEmpty(dossierData))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERREUR : Aucune donnée trouvée !");
            Console.WriteLine("Chemins testés :");
            foreach (var p in cheminsPossibles) Console.WriteLine($" - {p}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"✅ Dossier de données validé : {dossierData}");

        try
        {
            // =========================================================
            // 3. INITIALISATION DU PIPELINE (Dependency Injection)
            // =========================================================

            // A. Infrastructure Données
            var providerLogger = loggerFactory.CreateLogger<MultiFileTickProvider>();
            var provider = new MultiFileTickProvider(dossierData, providerLogger);

            // B. Gestionnaire de Portefeuille (100k départ)
            var portfolioManager = new PortfolioManager(100_000);

            // C. Construction de la Chaîne de Responsabilité
            // Ordre : Aggregator -> Strategy -> Broker -> Portfolio

            Handler aggregator = new BarAggregator();       // Transforme Ticks en Bars
            Handler strategy = CreateStrategyFromMenu();    // Décide (Buy/Sell) sur Bars
            Handler broker = new SimulatedBroker();         // Exécute (au prix Tick)

            aggregator.SetNext(strategy);
            strategy.SetNext(broker);
            broker.SetNext(portfolioManager);

            // D. Moteur de Backtest
            var runnerLogger = loggerFactory.CreateLogger<BacktestRunner>();
            // NOTE IMPORTANTE : On donne la TÊTE de la chaîne (aggregator) au runner
            var runner = new BacktestRunner(provider, aggregator, runnerLogger);

            // =========================================================
            // 4. LANCEMENT DU BACKTEST
            // =========================================================
            var start = new DateTime(2015, 01, 01);
            var end = new DateTime(2015, 02, 01);

            Console.WriteLine($"--- Démarrage du moteur ({start:d} au {end:d}) ---");
            runner.Run("EURUSD", start, end);

            // =========================================================
            // 5. AFFICHAGE DES RÉSULTATS
            // =========================================================
            var result = portfolioManager.CurrentPortfolio;
            double profit = result.Equity - result.InitialCash;
            double returnPct = (profit / result.InitialCash) * 100;

            Console.WriteLine("\n========================================");
            Console.WriteLine("          RÉSULTATS DU BACKTEST         ");
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

    // =========================================================
    // MENU UTILISATEUR & FACTORIES
    // =========================================================

    private static Handler CreateStrategyFromMenu()
    {
        while (true)
        {
            Console.WriteLine("\n--- CHOIX DE LA STRATÉGIE ---");
            Console.WriteLine("1) RSI (paramétrable)");
            Console.WriteLine("2) RandomStrategy");
            Console.WriteLine("3) RSI + RandomStrategy (Router)");
            Console.WriteLine("4) Moving Average Crossover");
            Console.WriteLine("5) RSI + MA Crossover (Router)");
            Console.WriteLine("6) Mean Reversion");
            Console.WriteLine("7) Breakout / Support-Resistance");
            Console.WriteLine("-----------------------------");

            int choice = ReadInt("Sélection (1-7) : ", min: 1, max: 7);

            switch (choice)
            {
                case 1: return CreateRsiFromMenu();
                case 2: return new RandomStrategy();
                case 3: return new StrategyRouter(CreateRsiFromMenu(), new RandomStrategy());
                case 4: return CreateMovingAverageFromMenu();
                case 5: return new StrategyRouter(CreateRsiFromMenu(), CreateMovingAverageFromMenu());
                case 6: return CreateMeanReversionFromMenu();
                case 7: return CreateBreakoutFromMenu();
                default: Console.WriteLine("Choix invalide."); break;
            }
        }
    }

    private static Handler CreateRsiFromMenu()
    {
        Console.WriteLine("\n[PARAMÈTRES RSI]");
        int period = ReadInt("Period (défaut 14) : ", 14, 2, 1000);
        double oversold = ReadDouble("Oversold (défaut 30) : ", 30, 0, 100);
        double overbought = ReadDouble("Overbought (défaut 70) : ", 70, 0, 100);
        // Note : Le cooldown est géré par le BarAggregator (1 bougie/min), on ne le passe plus
        return new RSI(period, oversold, overbought, lot: 1.0);
    }

    private static Handler CreateMovingAverageFromMenu()
    {
        Console.WriteLine("\n[PARAMÈTRES MA CROSSOVER]");
        int shortP = ReadInt("MA courte (défaut 20) : ", 20, 1, 1000);
        int longP = ReadInt("MA longue (défaut 50) : ", 50, shortP + 1, 5000);
        double lot = ReadDouble("Lot (défaut 0.01) : ", 0.01, 0, 100);
        return new MovingAverageCrossover(shortP, longP, lot);
    }

    private static Handler CreateMeanReversionFromMenu()
    {
        Console.WriteLine("\n[PARAMÈTRES MEAN REVERSION]");
        int period = ReadInt("Period (défaut 20) : ", 20, 2, 1000);
        double k = ReadDouble("Multiplicateur K (défaut 2.0) : ", 2.0, 0, 10);
        double lot = ReadDouble("Lot (défaut 1.0) : ", 1.0, 0, 100);
        return new MeanReversion(period, k, lot);
    }

    private static Handler CreateBreakoutFromMenu()
    {
        Console.WriteLine("\n[PARAMÈTRES BREAKOUT]");
        int lookback = ReadInt("Lookback period (défaut 20) : ", 20, 2, 1000);
        double lot = ReadDouble("Lot (défaut 1.0) : ", 1.0, 0, 100);
        return new BreakoutSupportResistance(lookback, lot);
    }

    // =========================================================
    // HELPERS INPUT CONSOLE
    // =========================================================

    private static int ReadInt(string prompt, int? defaultValue = null, int? min = null, int? max = null)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue) return defaultValue.Value;
            if (!int.TryParse(input, out int value)) { Console.WriteLine("Entier invalide."); continue; }
            if (min.HasValue && value < min.Value) { Console.WriteLine($"Min requis : {min.Value}"); continue; }
            if (max.HasValue && value > max.Value) { Console.WriteLine($"Max requis : {max.Value}"); continue; }
            return value;
        }
    }

    private static double ReadDouble(string prompt, double? defaultValue = null, double? minExclusive = null, double? maxExclusive = null)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue) return defaultValue.Value;
            if (!double.TryParse(input, out double value)) { Console.WriteLine("Nombre invalide."); continue; }
            if (minExclusive.HasValue && value <= minExclusive.Value) { Console.WriteLine($"Doit être > {minExclusive.Value}"); continue; }
            if (maxExclusive.HasValue && value >= maxExclusive.Value) { Console.WriteLine($"Doit être < {maxExclusive.Value}"); continue; }
            return value;
        }
    }
}