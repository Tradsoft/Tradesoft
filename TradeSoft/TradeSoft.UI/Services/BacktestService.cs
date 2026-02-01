using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;
using TradeSoft.Engine.Strategies;
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Analysis;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

using Bar = TradeSoft.Core.Domain.Bar;

namespace TradeSoft.UI.Services
{
    // 1. Collecteur pour le Graphique
    public class BarCollector : Handler
    {
        public List<Bar> Bars { get; } = new List<Bar>();
        protected override void OnProcess(TickContext context)
        {
            if (context.GeneratedBar != null)
            {
                var b = context.GeneratedBar;
                Bars.Add(new Bar { Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Timestamp = b.Timestamp });
            }
        }
    }

    // 2. Logger d'Intentions (AVANT le RiskManager)
    public class OrderLogger : Handler
    {
        protected override void OnProcess(TickContext context)
        {
            if (context.NewOrders.Count > 0)
            {
                DateTime time = new DateTime(context.Tick.Timestamp);
                foreach (var order in context.NewOrders)
                {
                    string side = order.Side.ToString().ToUpper();
                    string type = order.Type.ToString().ToUpper();
                    // On logue l'intention brute
                    System.Diagnostics.Debug.WriteLine($"📋 [INTENTION] {time:dd/MM HH:mm} | {side} {type} | Qty: {order.Quantity} | Prix: {order.Price}");
                }
            }
        }
    }

    // 3. Logger d'Exécutions (APRÈS le Broker)
    public class ConsoleTradeLogger : Handler
    {
        protected override void OnProcess(TickContext context)
        {
            foreach (var trade in context.Trades)
            {
                string action = trade.Side.ToString().ToUpper();
                DateTime tradeTime = new DateTime(trade.Timestamp);
                // On logue le trade confirmé
                System.Diagnostics.Debug.WriteLine($"⚡ [EXECUTION] {tradeTime:dd/MM HH:mm} | {action} | Prix: {trade.Price:F5} | Qty: {trade.Quantity}");
            }
        }
    }

    // 4. Service Principal
    public class BacktestService
    {
        public async Task<List<Bar>> GetHistoricalBarsAsync(BacktestConfig config)
        {
            return await Task.Run(() =>
            {
                string? dataPath = FindDataPath();
                if (dataPath == null) return new List<Bar>();

                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();

                var provider = new MultiFileTickProvider(dataPath, logger);
                var aggregator = new BarAggregator(config.Timeframe);
                var collector = new BarCollector();

                aggregator.SetNext(collector);

                var context = new TickContext();
                foreach (var tick in provider.GetTicks(config.Symbol, config.StartDate, config.EndDate))
                {
                    context.Reset(tick);
                    aggregator.Execute(context);
                }
                return collector.Bars;
            });
        }

        public async Task<BacktestResult> RunBacktestAsync(BacktestConfig config, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();
                var runnerLogger = loggerFactory.CreateLogger<TradeSoft.Engine.BacktestRunner>();

                string? dataPath = FindDataPath();
                if (dataPath == null) throw new FileNotFoundException("Data introuvable.");

                var provider = new MultiFileTickProvider(dataPath, logger);
                var aggregator = new BarAggregator(config.Timeframe);

                // --- Création des composants ---
                var barCollector = new BarCollector();
                var orderLogger = new OrderLogger();        // Logger d'intentions
                var tradeLogger = new ConsoleTradeLogger(); // Logger d'exécutions

                var portfolioManager = new PortfolioManager(config.InitialCash);
                var broker = new SimulatedBroker(config);
                var riskManager = new RiskManager(config, portfolioManager);
                Handler strategy = CreateStrategy(config.Strategy, config.StrategyParameters);

                // --- PIPELINE ORDONNÉ (CRUCIAL) ---

                // 1. Data -> Barres
                aggregator.SetNext(barCollector);

                // 2. Barres -> Stratégie (Génère des NewOrders)
                barCollector.SetNext(strategy);

                // 3. Stratégie -> Logger (On voit ce que la stratégie veut faire)
                strategy.SetNext(orderLogger);

                // 4. Logger -> RiskManager (Filtre les ordres dangereux)
                orderLogger.SetNext(riskManager);

                // 5. RiskManager -> Broker (Exécute les ordres validés)
                riskManager.SetNext(broker);

                // 6. Broker -> TradeLogger (Affiche les trades réalisés)
                broker.SetNext(tradeLogger);

                // 7. TradeLogger -> Portfolio (Met à jour le compte)
                tradeLogger.SetNext(portfolioManager);

                // --- Exécution ---
                System.Diagnostics.Debug.WriteLine("=== DÉBUT DU BACKTEST ===");

                var runner = new TradeSoft.Engine.BacktestRunner(provider, aggregator, runnerLogger);
                runner.Run(config.Symbol, config.StartDate, config.EndDate, null, token);

                System.Diagnostics.Debug.WriteLine("=== FIN DU BACKTEST ===");

                var result = StatisticsCalculator.Calculate(portfolioManager.CurrentPortfolio, portfolioManager.EquityCurve);
                result.Bars = barCollector.Bars;

                if (result.EquityCurve.Count == 0) result.FinalEquity = config.InitialCash;

                return result;
            }, token);
        }

        private Handler CreateStrategy(StrategyType type, Dictionary<string, object> parameters)
        {
            double GetD(string key, double def) => parameters.ContainsKey(key) ? Convert.ToDouble(parameters[key]) : def;
            int GetI(string key, int def) => parameters.ContainsKey(key) ? Convert.ToInt32(parameters[key]) : def;

            return type switch
            {
                StrategyType.RSI => new RSI(
                    period: GetI("Period", 14),
                    oversold: GetD("Oversold", 30),
                    overbought: GetD("Overbought", 70),
                    lot: GetD("Lot", 1.0),
                    slPips: GetD("SL Pips", 50),
                    tpPips: GetD("TP Pips", 100)),

                StrategyType.MAC => new MovingAverageCrossover(
                    shortPeriod: GetI("Short Period", 20),
                    longPeriod: GetI("Long Period", 50),
                    lot: GetD("Lot", 1.0)),

                StrategyType.MeanReversion => new MeanReversion(
                    period: GetI("Period", 20),
                    k: GetD("K", 2.0),
                    lot: GetD("Lot", 1.0)),

                StrategyType.Breakout => new BreakoutSupportResistance(
                    lookback: GetI("Lookback", 20),
                    lot: GetD("Lot", 1.0)),

                _ => new StrategyRouter()
            };
        }

        private string? FindDataPath()
        {
            string[] paths = new[] {
                @"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",
                @"C:\Users\louis\source\repos\Tradesoft\Data\EURUSD",
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD"))
            };
            return paths.FirstOrDefault(Directory.Exists);
        }
    }
}