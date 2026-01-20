using Microsoft.Extensions.Logging;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Services;
using TradeSoft.Engine.Services;
using TradeSoft.Infrastructure.IO;

class Program
{
    static async Task Main(string[] args)
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
            @"C:\Users\FlowUP\source\repos\GitHub\Tradesoft\Data\EURUSD",
            @"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",
            @"C:\Users\Pc\Bureau\Projet\Data\EURUSD",
            @"C:\Users\inesm\OneDrive\Documents\M2\Tradesoft\Data\EURUSD",
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD")),
        };

        string? dossierData = cheminsPossibles.FirstOrDefault(path => Directory.Exists(path));

        if (string.IsNullOrEmpty(dossierData))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERREUR : Aucune donnée trouvée !");
            return;
        }

        Console.WriteLine($"✅ Dossier de données validé : {dossierData}");

        try
        {
            // =========================================================
            // 3. INITIALISATION DU SERVICE (Couche Service)
            // =========================================================

            // On prépare les dépendances
            var providerLogger = loggerFactory.CreateLogger<MultiFileTickProvider>();
            var provider = new MultiFileTickProvider(dossierData, providerLogger);

            // On crée le service (C'est le seul point de contact avec le moteur)
            IBacktestService service = new BacktestService(provider, loggerFactory);

            // =========================================================
            // 4. INTERFACE UTILISATEUR (Création de la Config)
            // =========================================================

            // A. Choix du Timeframe
            TimeSpan timeframe = ChooseTimeframe();

            // B. Choix de la Stratégie (Type + Paramètres)
            (StrategyType stratType, Dictionary<string, object> stratParams) = ChooseStrategy();

            // C. Construction de l'objet de Configuration
            var config = new BacktestConfig
            {
                Symbol = "EURUSD",
                StartDate = new DateTime(2015, 01, 01),
                EndDate = new DateTime(2015, 02, 01),
                InitialCash = 100_000,

                Timeframe = timeframe,
                Strategy = stratType,
                StrategyParameters = stratParams // Assurez-vous d'avoir ajouté ce champ dans BacktestConfig
            };

            Console.WriteLine($"\n--- PRÊT À LANCER LE BACKTEST ---");
            Console.WriteLine($"Stratégie : {config.Strategy}");
            Console.WriteLine($"Timeframe : {config.Timeframe}");
            Console.WriteLine("Appuyez sur ENTRÉE pour démarrer...");
            Console.ReadLine();

            // =========================================================
            // 5. EXÉCUTION (Async)
            // =========================================================

            // Gestion de la progression
            var progress = new Progress<float>(p =>
            {
                Console.Write($"\rProgression : {p * 100:F1}%   ");
            });

            // Appel au Service (ne bloque pas l'UI)
            BacktestResult result = await service.RunBacktestAsync(config, progress);

            // =========================================================
            // 6. AFFICHAGE DES RÉSULTATS
            // =========================================================
            Console.WriteLine("\n\n========================================");
            Console.WriteLine("          RÉSULTATS DU BACKTEST         ");
            Console.WriteLine("========================================");
            Console.WriteLine($"Capital Initial : {result.InitialCash:C0}");
            Console.WriteLine($"Capital Final   : {result.FinalEquity:C2}");
            Console.WriteLine($"PnL Net         : {result.NetProfit:C2} ({result.ReturnPercent:F2}%)");
            Console.WriteLine($"Total Trades    : {result.TotalTrades}");
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
    // MENUS UTILISATEUR
    // =========================================================

    private static TimeSpan ChooseTimeframe()
    {
        Console.Clear();
        Console.WriteLine("--- CHOIX DU TIMEFRAME ---");
        Console.WriteLine(" 1) S5  (5 Secondes)");
        Console.WriteLine(" 2) S30 (30 Secondes)");
        Console.WriteLine(" 3) M1  (1 Minute)");
        Console.WriteLine(" 4) M5  (5 Minutes)");
        Console.WriteLine(" 5) M15 (15 Minutes)");
        Console.WriteLine(" 6) H1  (1 Heure)");
        Console.WriteLine(" 7) H4  (4 Heures)");
        Console.WriteLine(" 8) D1  (Daily)");

        int choice = ReadInt("Sélection : ", min: 1, max: 8);

        return choice switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(30),
            3 => TimeSpan.FromMinutes(1),
            4 => TimeSpan.FromMinutes(5),
            5 => TimeSpan.FromMinutes(15),
            6 => TimeSpan.FromHours(1),
            7 => TimeSpan.FromHours(4),
            8 => TimeSpan.FromDays(1),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    private static (StrategyType, Dictionary<string, object>) ChooseStrategy()
    {
        Console.WriteLine("\n--- CHOIX DE LA STRATÉGIE ---");
        Console.WriteLine("1) RSI");
        Console.WriteLine("2) Moving Average Crossover");
        Console.WriteLine("3) Mean Reversion");
        Console.WriteLine("4) Breakout");
        Console.WriteLine("5) Random (Test)");

        int choice = ReadInt("Sélection (1-5) : ", min: 1, max: 5);
        var paramsDict = new Dictionary<string, object>();

        switch (choice)
        {
            case 1: // RSI
                Console.WriteLine("\n[PARAMÈTRES RSI]");
                paramsDict["Period"] = ReadInt("Period (défaut 14) : ", 14);
                paramsDict["Oversold"] = ReadDouble("Oversold (défaut 30) : ", 30);
                paramsDict["Overbought"] = ReadDouble("Overbought (défaut 70) : ", 70);
                return (StrategyType.RSI, paramsDict);

            case 2: // MA Crossover
                Console.WriteLine("\n[PARAMÈTRES MA]");
                paramsDict["ShortPeriod"] = ReadInt("MA Courte (défaut 20) : ", 20);
                paramsDict["LongPeriod"] = ReadInt("MA Longue (défaut 50) : ", 50);
                return (StrategyType.MAC, paramsDict);

            case 3: // Mean Reversion
                Console.WriteLine("\n[PARAMÈTRES MEAN REVERSION]");
                paramsDict["Period"] = ReadInt("Period (défaut 20) : ", 20);
                paramsDict["K"] = ReadDouble("K (défaut 2.0) : ", 2.0);
                return (StrategyType.MeanReversion, paramsDict);

            case 4: // Breakout
                Console.WriteLine("\n[PARAMÈTRES BREAKOUT]");
                paramsDict["Lookback"] = ReadInt("Lookback (défaut 20) : ", 20);
                return (StrategyType.Breakout, paramsDict);

            case 5: // Random
            default:
                return (StrategyType.Random, paramsDict);
        }
    }

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