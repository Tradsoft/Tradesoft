using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;
using TradeSoft.Core.Services;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Strategies;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

namespace TradeSoft.Engine.Services
{
    public class BacktestService : IBacktestService
    {
        private readonly MultiFileTickProvider _provider;
        private readonly ILoggerFactory _loggerFactory;

        public BacktestService(MultiFileTickProvider provider, ILoggerFactory loggerFactory)
        {
            _provider = provider;
            _loggerFactory = loggerFactory;
        }

        public async Task<BacktestResult> RunBacktestAsync(
            BacktestConfig config,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var portfolioManager = new PortfolioManager(config.InitialCash);
                var broker = new SimulatedBroker();
                var aggregator = new BarAggregator(config.Timeframe);

                Handler strategy = CreateStrategy(config);

                aggregator.SetNext(strategy);
                strategy.SetNext(broker);
                broker.SetNext(portfolioManager);

                var runnerLogger = _loggerFactory.CreateLogger<BacktestRunner>();
                var runner = new BacktestRunner(_provider, aggregator, runnerLogger);

                runner.Run(config.Symbol, config.StartDate, config.EndDate, progress, cancellationToken);

                return BuildResult(portfolioManager);

            }, cancellationToken);
        }

        private Handler CreateStrategy(BacktestConfig config)
        {
            switch (config.Strategy)
            {
                case StrategyType.RSI:
                    return new RSI(
                        period: 14,
                        oversold: 30,
                        overbought: 70,
                        lot: 1.0);

                case StrategyType.MAC:
                    return new MovingAverageCrossover(
                        shortPeriod: 20,
                        longPeriod: 50,
                        lot: 1.0);

                case StrategyType.MeanReversion:
                    return new MeanReversion(period: 20, k: 2.0, lot: 1.0);

                case StrategyType.Breakout:
                    return new BreakoutSupportResistance(lookback: 20, lot: 1.0);

                case StrategyType.Random:
                default:
                    return new RandomStrategy();
            }
        }

        private BacktestResult BuildResult(PortfolioManager pm)
        {
            var result = new BacktestResult
            {
                InitialCash = pm.CurrentPortfolio.InitialCash,
                FinalEquity = pm.CurrentPortfolio.Equity,
                NetProfit = pm.CurrentPortfolio.Equity - pm.CurrentPortfolio.InitialCash,
                TotalTrades = pm.TradeHistory.Count,
                EquityCurve = pm.EquityCurve
                    .Select(x => new EquityPoint(x.Time, x.Equity))
                    .ToList(),

                Trades = pm.TradeHistory.Select(t => new TradeResultItem
                {
                    Id = t.OrderId,
                    Time = new DateTime(t.Timestamp),
                    Side = t.Side.ToString(),
                    Price = t.Price,
                    Quantity = t.Quantity,
                    PnL = 0 // À calculer si on a la logique de fermeture
                }).ToList()
            };

            result.ReturnPercent = (result.NetProfit / result.InitialCash) * 100;
            return result;
        }
    }
}