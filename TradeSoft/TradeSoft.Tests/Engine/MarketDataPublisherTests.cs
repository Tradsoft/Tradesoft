using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;
using TradeSoft.Infrastructure.Streaming;
using Xunit;

namespace TradeSoft.Tests.Engine;

public sealed class MarketDataPublisherTests
{
    [Fact]
    public async Task PublishesTicksAcrossMultipleFilesWithoutResettingPipeline()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var file1 = Path.Combine(tempDir.FullName, "ticks-1.bin");
            var file2 = Path.Combine(tempDir.FullName, "ticks-2.bin");

            var ticksFirstFile = new[]
            {
                new TickData(1, 100.1, 100.2, 10),
                new TickData(2, 100.2, 100.3, 20)
            };

            var ticksSecondFile = new[]
            {
                new TickData(3, 100.3, 100.4, 30),
                new TickData(4, 100.4, 100.5, 40),
                new TickData(5, 100.5, 100.6, 50)
            };

            WriteTicks(file1, ticksFirstFile);
            WriteTicks(file2, ticksSecondFile);

            var expected = ticksFirstFile.Concat(ticksSecondFile).ToArray();
            var totalTicks = expected.Length;

            using var completion = new ManualResetEventSlim(false);

            var strategy = new CollectingStrategy(expectedCount: totalTicks, completion);
            var runner = new SimulationRunner(DisruptorConfig.Default, strategy: strategy);

            runner.Start();
            try
            {
                var publisher = new MarketDataPublisher(
                    streamer: new MultiFileStreamer(new[] { file1, file2 }),
                    reader: new BinaryTickReader());

                await publisher.PublishAsync(runner);

                completion.Wait(TimeSpan.FromSeconds(5))
                    .Should().BeTrue("all ticks should flow through the disruptor");

                strategy.Ticks.Should().HaveCount(totalTicks);
                strategy.Ticks.Should().ContainInOrder(expected);
            }
            finally
            {
                runner.Stop();
            }
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    private static void WriteTicks(string filePath, IEnumerable<TickData> ticks)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        foreach (var tick in ticks)
        {
            writer.Write(tick.Timestamp);
            writer.Write(tick.Bid);
            writer.Write(tick.Ask);
            writer.Write(tick.Volume);
            writer.Write((byte)tick.Flags);
        }
    }

    private sealed class CollectingStrategy : IStrategy
    {
        private readonly int _expectedCount;
        private readonly ManualResetEventSlim _completion;
        private readonly List<TickData> _ticks = new();

        public CollectingStrategy(int expectedCount, ManualResetEventSlim completion)
        {
            _expectedCount = expectedCount;
            _completion = completion;
        }

        public IReadOnlyList<TickData> Ticks
        {
            get
            {
                lock (_ticks)
                {
                    return _ticks.ToList();
                }
            }
        }

        public void OnMarketEvent(MarketEvent marketEvent)
        {
            if (marketEvent.Tick is null) return;

            lock (_ticks)
            {
                _ticks.Add(marketEvent.Tick.Value);

                if (_ticks.Count >= _expectedCount)
                    _completion.Set();
            }
        }
    }
}
