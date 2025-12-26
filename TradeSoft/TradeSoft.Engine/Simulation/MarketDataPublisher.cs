using System;
using System.Threading;
using System.Threading.Tasks;
using TradeSoft.Core.MarketData;
using TradeSoft.Infrastructure.Streaming;

namespace TradeSoft.Engine.Simulation;

public sealed class MarketDataPublisher
{
    private readonly MultiFileStreamer _streamer;
    private readonly BinaryTickReader _reader;

    public MarketDataPublisher(MultiFileStreamer streamer, BinaryTickReader reader)
    {
        _streamer = streamer ?? throw new ArgumentNullException(nameof(streamer));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// Rejoue les ticks vers le runner. Le runner doit déjà être Start().
    /// </summary>
    public async Task PublishAsync(SimulationRunner runner, CancellationToken ct = default)
    {
        if (runner is null) throw new ArgumentNullException(nameof(runner));
        if (!runner.IsRunning)
            throw new InvalidOperationException("SimulationRunner must be started before publishing ticks.");

        await foreach (var stream in _streamer.StreamFilesAsync(ct).ConfigureAwait(false))
        {
            await foreach (var tick in _reader.ReadTicksAsync(stream, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                runner.PublishTick(tick);
            }
        }
    }

    /// <summary>
    /// Variante plus générique : rejoue vers un callback (utile si demain tu veux publier ailleurs qu'un runner).
    /// </summary>
    public async Task PublishAsync(Action<TickData> publishTick, CancellationToken ct = default)
    {
        if (publishTick is null) throw new ArgumentNullException(nameof(publishTick));

        await foreach (var stream in _streamer.StreamFilesAsync(ct).ConfigureAwait(false))
        {
            await foreach (var tick in _reader.ReadTicksAsync(stream, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                publishTick(tick);
            }
        }
    }
}
