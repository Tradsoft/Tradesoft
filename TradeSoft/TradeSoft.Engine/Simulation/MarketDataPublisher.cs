using System;
using System.Diagnostics;
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
    public Task PublishAsync(
        SimulationRunner runner,
        CancellationToken ct = default)
        => PublishAsync(runner, throttle: null, onDebug: null, ct);

    /// <summary>
    /// Rejoue les ticks vers le runner avec throttle (simulation réaliste) + hook debug.
    /// </summary>
    public async Task PublishAsync(
        SimulationRunner runner,
        ReplayThrottleOptions? throttle,
        Action<ReplayDebugTick>? onDebug,
        CancellationToken ct = default)
    {
        if (runner is null) throw new ArgumentNullException(nameof(runner));
        if (!runner.IsRunning)
            throw new InvalidOperationException("SimulationRunner must be started before publishing ticks.");

        await PublishAsync(
            publishTick: runner.PublishTick,
            throttle: throttle,
            onDebug: onDebug,
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Variante plus générique : rejoue vers un callback (utile si demain tu veux publier ailleurs qu'un runner).
    /// </summary>
    public Task PublishAsync(
        Action<TickData> publishTick,
        CancellationToken ct = default)
        => PublishAsync(publishTick, throttle: null, onDebug: null, ct);

    /// <summary>
    /// Variante générique + throttle + debug.
    /// </summary>
    public async Task PublishAsync(
        Action<TickData> publishTick,
        ReplayThrottleOptions? throttle,
        Action<ReplayDebugTick>? onDebug,
        CancellationToken ct = default)
    {
        if (publishTick is null) throw new ArgumentNullException(nameof(publishTick));
        throttle?.Validate();

        var sw = Stopwatch.StartNew();

        long? startTs = null;
        TimeSpan startWall = default;

        await foreach (var stream in _streamer.StreamFilesAsync(ct).ConfigureAwait(false))
        {
            await foreach (var tick in _reader.ReadTicksAsync(stream, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                if (startTs is null)
                {
                    startTs = tick.Timestamp;       
                    startWall = sw.Elapsed;
                }

                if (throttle is not null && !throttle.Disabled)
                {
                    var desiredMs = (tick.Timestamp - startTs.Value) / throttle.Speed;
                    var desired = TimeSpan.FromMilliseconds(desiredMs);

                    var elapsed = sw.Elapsed - startWall;
                    var delay = desired - elapsed;

                    if (delay < TimeSpan.Zero && throttle.CatchUpWhenBehind)
                    {
                        startWall += (-delay);
                        delay = TimeSpan.Zero;
                    }

                    if (delay > TimeSpan.Zero)
                    {
                        var capped = TimeSpan.FromMilliseconds(
                            Math.Min(delay.TotalMilliseconds, throttle.MaxDelayMs));

                        onDebug?.Invoke(new ReplayDebugTick(
                            Tick: tick,
                            DesiredElapsed: desired,
                            ActualElapsed: elapsed,
                            DelayApplied: capped));

                        await Task.Delay(capped, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        onDebug?.Invoke(new ReplayDebugTick(
                            Tick: tick,
                            DesiredElapsed: desired,
                            ActualElapsed: elapsed,
                            DelayApplied: TimeSpan.Zero));
                    }
                }
                else
                {
                    onDebug?.Invoke(new ReplayDebugTick(
                        Tick: tick,
                        DesiredElapsed: TimeSpan.Zero,
                        ActualElapsed: TimeSpan.Zero,
                        DelayApplied: TimeSpan.Zero));
                }

                publishTick(tick);
            }
        }
    }
}

/// <summary>
/// Payload debug (loggable) pour comprendre le timing du replay.
/// </summary>
public readonly record struct ReplayDebugTick(
    TickData Tick,
    TimeSpan DesiredElapsed,
    TimeSpan ActualElapsed,
    TimeSpan DelayApplied);
