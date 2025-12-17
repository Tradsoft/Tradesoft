using TradeSoft.Infrastructure.Streaming;

namespace TradeSoft.Engine.Simulation;

public sealed class MarketDataPublisher
{
    private readonly SimulationRunner _runner;
    private readonly MultiFileStreamer _streamer;
    private readonly BinaryTickReader _reader;

    public MarketDataPublisher(SimulationRunner runner, MultiFileStreamer streamer, BinaryTickReader reader)
    {
        _runner = runner;
        _streamer = streamer;
        _reader = reader;
    }

    public async Task PublishAsync(CancellationToken ct = default)
    {
        // StreamFilesAsync() ouvre/ferme chaque Stream automatiquement.
        await foreach (var stream in _streamer.StreamFilesAsync(ct).ConfigureAwait(false))
        {
            await foreach (var tick in _reader.ReadTicksAsync(stream, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                _runner.PublishTick(tick);
            }
        }
    }
}
