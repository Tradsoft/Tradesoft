using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TradeSoft.Core.DTO;
using TradeSoft.Engine.Services;
using TradeSoft.Infrastructure.IO;
using Xunit;

namespace TradeSoft.Tests
{
    public class Scenario10_ParametersIncoherentsTests
    {
        // Helper pour créer un BacktestService avec un dossier temporaire et logger neutre
        private static BacktestService CreateService()
        {
            // Crée un répertoire temporaire pour MultiFileTickProvider
            var dir = Path.Combine(Path.GetTempPath(), "TradeSoftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            // Logger neutre pour les tests
            var loggerFactory = NullLoggerFactory.Instance;
            var providerLogger = loggerFactory.CreateLogger<MultiFileTickProvider>();
            var provider = new MultiFileTickProvider(dir, providerLogger);

            return new BacktestService(provider, loggerFactory);
        }

        [Fact]
        public async Task RunBacktestAsync_ShouldFailCleanly_WhenDatesAreInverted()
        {
            var service = CreateService();

            var cfg = new BacktestConfig
            {
                Symbol = "EURUSD",
                StartDate = new DateTime(2024, 01, 02),
                EndDate = new DateTime(2024, 01, 01), // date inversée
                Timeframe = TimeSpan.FromMinutes(1),
                InitialCash = 10_000,
                MinLotSize = 100,
                LotSize = 100_000
            };

            Func<Task> act = async () => await service.RunBacktestAsync(cfg);

            var ex = await act.Should().ThrowAsync<ArgumentException>();
            ex.Which.ParamName.Should().Be("EndDate");
        }

        [Fact]
        public async Task RunBacktestAsync_ShouldFailCleanly_WhenLotSizeIsNegative()
        {
            var service = CreateService();

            var cfg = new BacktestConfig
            {
                Symbol = "EURUSD",
                StartDate = new DateTime(2024, 01, 01),
                EndDate = new DateTime(2024, 01, 02),
                Timeframe = TimeSpan.FromMinutes(1),
                InitialCash = 10_000,
                MinLotSize = 100,
                LotSize = -1 // lot négatif
            };

            Func<Task> act = async () => await service.RunBacktestAsync(cfg);

            var ex = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
            ex.Which.ParamName.Should().Be("LotSize");
        }
    }
}
