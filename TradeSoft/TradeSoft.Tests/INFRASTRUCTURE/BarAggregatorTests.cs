using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Infrastructure.Processing;
using Xunit;

namespace TradeSoft.Tests.INFRASTRUCTURE
{
    public class BarAggregatorTests
    {
        [Fact]
        public void Constructor_ShouldThrow_WhenTimeframeIsZeroOrNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarAggregator(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarAggregator(TimeSpan.FromMinutes(-5)));
        }

        [Fact]
        public void OnProcess_ShouldAggregateOHLCVAndEmitBarOnNextPeriod()
        {
            var aggregator = new BarAggregator(TimeSpan.FromMinutes(1));
            var t0 = new DateTime(2024, 1, 1, 10, 0, 5);
            var ctx = new TickContext();

            ctx.Reset(new TickData(t0.Ticks, 1.1000, 1.1000, 10));
            aggregator.Execute(ctx);
            Assert.Null(ctx.GeneratedBar);

            ctx.Reset(new TickData(t0.AddSeconds(15).Ticks, 1.1050, 1.1050, 20));
            aggregator.Execute(ctx);
            Assert.Null(ctx.GeneratedBar);

            ctx.Reset(new TickData(t0.AddSeconds(30).Ticks, 1.0980, 1.0980, 15));
            aggregator.Execute(ctx);
            Assert.Null(ctx.GeneratedBar);

            ctx.Reset(new TickData(t0.AddSeconds(45).Ticks, 1.1020, 1.1020, 5));
            aggregator.Execute(ctx);
            Assert.Null(ctx.GeneratedBar);

            // Tick dans la minute suivante -> émet la barre précédente
            ctx.Reset(new TickData(new DateTime(2024, 1, 1, 10, 1, 0).Ticks, 1.1030, 1.1030, 12));
            aggregator.Execute(ctx);

            Assert.NotNull(ctx.GeneratedBar);
            Assert.Equal(new DateTime(2024, 1, 1, 10, 0, 0), ctx.GeneratedBar!.Timestamp);
            Assert.Equal(1.1000, ctx.GeneratedBar.Open, 5);
            Assert.Equal(1.1050, ctx.GeneratedBar.High, 5);
            Assert.Equal(1.0980, ctx.GeneratedBar.Low, 5);
            Assert.Equal(1.1020, ctx.GeneratedBar.Close, 5);
            Assert.Equal(50, ctx.GeneratedBar.Volume);
        }

        [Fact]
        public void OnProcess_ShouldIgnoreInvalidPricesAndMassiveSingleTickSpikesWithinOneHour()
        {
            var aggregator = new BarAggregator(TimeSpan.FromMinutes(1));
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);
            var ctx = new TickContext();

            // Prix nul ignoré
            ctx.Reset(new TickData(t0.Ticks, 0.0, 0.0, 10));
            aggregator.Execute(ctx);

            // Premier tick valide
            ctx.Reset(new TickData(t0.AddSeconds(5).Ticks, 1.1000, 1.1000, 10));
            aggregator.Execute(ctx);

            // Pic aberrant (+50% dans la même minute) ignoré
            ctx.Reset(new TickData(t0.AddSeconds(10).Ticks, 1.6500, 1.6500, 10));
            aggregator.Execute(ctx);

            // Minute suivante
            ctx.Reset(new TickData(t0.AddMinutes(1).Ticks, 1.1010, 1.1010, 10));
            aggregator.Execute(ctx);

            Assert.NotNull(ctx.GeneratedBar);
            Assert.Equal(1.1000, ctx.GeneratedBar!.High, 5);
        }
    }
}
