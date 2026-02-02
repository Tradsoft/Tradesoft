using Microsoft.Extensions.Logging;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;
using TradeSoft.Core.Services;
using TradeSoft.Engine.Analysis; 
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
    IProgress<float>? progress = null,
    CancellationToken cancellationToken = default)
        {
            ValidateConfig(config);

            return await Task.Run(() =>
            {
                var portfolioManager = new PortfolioManager(config.InitialCash);
                var broker = new SimulatedBroker(config);
                var aggregator = new BarAggregator(config.Timeframe);
                var riskManager = new RiskManager(config, portfolioManager);

                Handler strategy = CreateStrategy(config);

                aggregator.SetNext(strategy);
                strategy.SetNext(riskManager);
                riskManager.SetNext(broker);
                broker.SetNext(portfolioManager);

                var runnerLogger = _loggerFactory.CreateLogger<BacktestRunner>();
                var runner = new BacktestRunner(_provider, aggregator, runnerLogger);

                runner.Run(config.Symbol, config.StartDate, config.EndDate, progress, cancellationToken);

                return BuildResult(portfolioManager);

            }, cancellationToken);
        }
        private static void ValidateConfig(BacktestConfig config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.Symbol))
                throw new ArgumentException("Symbol is required.", nameof(config.Symbol));

            if (config.EndDate < config.StartDate)
                throw new ArgumentException("EndDate must be greater than or equal to StartDate.", nameof(config.EndDate));

            if (config.LotSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.LotSize), "LotSize must be > 0.");

            if (config.MinLotSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.MinLotSize), "MinLotSize must be > 0.");

            if (config.MinLotSize > config.LotSize)
                throw new ArgumentException("MinLotSize must be <= LotSize.", nameof(config.MinLotSize));
        }



        private Handler CreateStrategy(BacktestConfig config)
        {
            switch (config.Strategy)
            {
                case StrategyType.RSI:
                    return new RSI(period: 14, oversold: 30, overbought: 70, lot: 1.0);

                case StrategyType.MAC:
                    return new MovingAverageCrossover(shortPeriod: 20, longPeriod: 50, lot: 1.0);

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
            return StatisticsCalculator.Calculate(pm.CurrentPortfolio, pm.EquityCurve);
        }
    }
}