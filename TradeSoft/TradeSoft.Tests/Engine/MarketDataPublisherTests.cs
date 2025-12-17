using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;
using TradeSoft.Infrastructure.Streaming;

namespace TradeSoft.Tests.Engine;

public class MarketDataPublisherTests
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

            var totalTicks = ticksFirstFile.Length + ticksSecondFile.Length;
            using var completion = new ManualResetEventSlim(false);
            var strategy = new CollectingStrategy(totalTicks, completion);
            var runner = new SimulationRunner(DisruptorConfig.Default, strategy);

            runner.Start();

            var publisher = new MarketDataPublisher(
                runner,
                new MultiFileStreamer(new[] { file1, file2 }),
                new BinaryTickReader());

            await publisher.PublishAsync();

            completion.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("all ticks should flow through the disruptor");
            runner.Stop();

            strategy.Ticks.Should().HaveCount(totalTicks);
            strategy.Ticks.Should().ContainInOrder(ticksFirstFile.Concat(ticksSecondFile));
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
        private readonly int _expected;
        private readonly ManualResetEventSlim _completion;
        private readonly List<TickData> _ticks = new();

        public CollectingStrategy(int expected, ManualResetEventSlim completion)
        {
            _expected = expected;
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
            if (marketEvent.Type != MarketEventType.Tick || marketEvent.Tick is null)
            {
                return;
            }

            lock (_ticks)
            {
                _ticks.Add(marketEvent.Tick.Value);
                if (_ticks.Count == _expected)
                {
                    _completion.Set();
                }
            }
        }
    }
}