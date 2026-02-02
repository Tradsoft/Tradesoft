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
using TradeSoft.Engine;
using TradeSoft.Engine.Strategies;
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Analysis;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

using Bar = TradeSoft.Core.Domain.Bar;

namespace TradeSoft.UI.Services
{
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
    public class TradeCollector : Handler
    {
        public List<Trade> AllTrades { get; } = new List<Trade>();
        protected override void OnProcess(TickContext context)
        {
            if (context.Trades.Count > 0)
            {
                AllTrades.AddRange(context.Trades);
            }
        }
    }
    public class OrderLogger : Handler
    {
        private readonly List<LogEntryDTO> _logs;
        public OrderLogger(List<LogEntryDTO> logs) { _logs = logs; }

        protected override void OnProcess(TickContext context)
        {
            if (context.NewOrders.Count > 0)
            {
                DateTime time = new DateTime(context.Tick.Timestamp);
                foreach (var order in context.NewOrders)
                {
                    string msg = $"{order.Side} {order.Type} | Qty: {order.Quantity:F2}";
                    _logs.Add(new LogEntryDTO(time, "INTENTION", msg));
                }
            }
        }
    }

    public class ExecutionLogger : Handler
    {
        private readonly List<LogEntryDTO> _logs;
        public ExecutionLogger(List<LogEntryDTO> logs) { _logs = logs; }

        protected override void OnProcess(TickContext context)
        {
            foreach (var trade in context.Trades)
            {
                DateTime time = new DateTime(trade.Timestamp);
                string msg = $"{trade.Side} | Prix: {trade.Price:F5} | Qty: {trade.Quantity:F2}";
                _logs.Add(new LogEntryDTO(time, "EXECUTION", msg));
            }
        }
    }

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
                var capturedLogs = new List<LogEntryDTO>();

                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();
                var runnerLogger = loggerFactory.CreateLogger<TradeSoft.Engine.BacktestRunner>();

                string? dataPath = FindDataPath();
                if (dataPath == null) throw new FileNotFoundException("Data introuvable.");

                var provider = new MultiFileTickProvider(dataPath, logger);
                var aggregator = new BarAggregator(config.Timeframe);

                var barCollector = new BarCollector();
                var tradeCollector = new TradeCollector();

                var orderLogger = new OrderLogger(capturedLogs);
                var tradeLogger = new ExecutionLogger(capturedLogs);

                var portfolioManager = new PortfolioManager(config.InitialCash);
                var broker = new SimulatedBroker(config, capturedLogs);
                var riskManager = new RiskManager(config, portfolioManager, capturedLogs);

                Handler strategy = CreateStrategy(config.Strategy, config.StrategyParameters);

                aggregator.SetNext(barCollector);
                barCollector.SetNext(strategy);
                strategy.SetNext(orderLogger);     
                orderLogger.SetNext(riskManager);   
                riskManager.SetNext(broker);        
                broker.SetNext(tradeCollector);     
                tradeCollector.SetNext(tradeLogger);
                tradeLogger.SetNext(portfolioManager);

                var runner = new TradeSoft.Engine.BacktestRunner(provider, aggregator, runnerLogger);
                runner.Run(config.Symbol, config.StartDate, config.EndDate, null, token);

                var result = StatisticsCalculator.Calculate(portfolioManager.CurrentPortfolio, portfolioManager.EquityCurve);

                result.Bars = barCollector.Bars;
                result.Trades = tradeCollector.AllTrades.Select(t => new TradeResultItem
                {
                    Time = new DateTime(t.Timestamp),
                    Side = t.Side.ToString(),
                    Price = t.Price,
                    Quantity = t.Quantity,
                    PnL = 0
                }).ToList();

                result.Logs = capturedLogs.OrderByDescending(l => l.Time).ToList();

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
                StrategyType.RSI => new RSI(GetI("Period", 14), GetD("Oversold", 30), GetD("Overbought", 70), GetD("Lot", 1.0), GetD("SL Pips", 50), GetD("TP Pips", 100), GetD("Tick Size", 0.01)),
                StrategyType.MAC => new MovingAverageCrossover(GetI("Short Period", 20), GetI("Long Period", 50), GetD("Lot", 1.0)),
                StrategyType.MeanReversion => new MeanReversion(GetI("Period", 20), GetD("K", 2.0), GetD("Lot", 1.0)),
                StrategyType.Breakout => new BreakoutSupportResistance(GetI("Lookback", 20), GetD("Lot", 1.0)),
                _ => new StrategyRouter()
            };
        }

        private string? FindDataPath()
        {
            string[] paths = new[] {
                @"C:\Users\Pc\Bureau\Projet\Data\EURUSD",
                @"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",
                @"C:\Users\louis\source\repos\Tradesoft\Data\EURUSD",
                @"C:\Users\FlowUP\source\repos\GitHub\Tradesoft\Data\EURUSD",
                @"C:\Users\inesm\OneDrive\Documents\M2\Tradesoft\Data\EURUSD",
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD"))
            };
            return paths.FirstOrDefault(Directory.Exists);
        }
    }
}