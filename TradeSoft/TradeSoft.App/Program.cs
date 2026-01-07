using Microsoft.Extensions.Logging;
using TradeSoft.Core.Pipeline;
using TradeSoft.Engine;
using TradeSoft.Engine.Strategies;
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
        // 2. Configuration du Chemin des Données (Multi-Dev Friendly)
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
            Console.WriteLine("ERREUR FATALE : Aucune donnée trouvée !");
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

            Handler strategy = new RandomStrategy();

            var runnerLogger = loggerFactory.CreateLogger<BacktestRunner>();
            var runner = new BacktestRunner(provider, strategy, runnerLogger);

            // ---------------------------------------------------------
            // 4. Lancement du Backtest
            // ---------------------------------------------------------
            var start = new DateTime(2015, 01, 01);
            var end = new DateTime(2015, 02, 01);

            Console.WriteLine("--- Démarrage du moteur ---");

            runner.Run("EURUSD", start, end);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nCRASH DU PROGRAMME :\n{ex}");
            Console.ResetColor();
        }
    }
}