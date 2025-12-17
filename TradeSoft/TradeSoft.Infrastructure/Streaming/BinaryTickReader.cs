using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TradeSoft.Core.MarketData;

namespace TradeSoft.Infrastructure.Streaming;

/// <summary>
/// Reads <see cref="TickData"/> instances from a binary stream without buffering entire files.
/// </summary>
public sealed class BinaryTickReader
{
    private readonly int _tickSize = Unsafe.SizeOf<TickData>();

    public IEnumerable<TickData> ReadTicks(Stream stream, CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);

        var buffer = ArrayPool<byte>.Shared.Rent(_tickSize);
        try
        {
            while (TryReadTick(stream, buffer, cancellationToken, out var tick))
            {
                yield return tick;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async IAsyncEnumerable<TickData> ReadTicksAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);

        var buffer = ArrayPool<byte>.Shared.Rent(_tickSize);
        try
        {
            while (await TryReadTickAsync(stream, buffer, cancellationToken).ConfigureAwait(false) is TickData tick)
            {
                yield return tick;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private bool TryReadTick(Stream stream, byte[] buffer, CancellationToken cancellationToken, out TickData tick)
    {
        var read = FillBuffer(stream, buffer, cancellationToken);
        if (read == 0)
        {
            tick = default;
            return false;
        }

        if (read != _tickSize)
        {
            throw new InvalidDataException($"Incomplete tick encountered. Expected {_tickSize} bytes, got {read}.");
        }

        tick = MemoryMarshal.Read<TickData>(buffer.AsSpan(0, _tickSize));
        return true;
    }

    private async ValueTask<TickData?> TryReadTickAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = await FillBufferAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return null;
        }

        if (read != _tickSize)
        {
            throw new InvalidDataException($"Incomplete tick encountered. Expected {_tickSize} bytes, got {read}.");
        }

        return MemoryMarshal.Read<TickData>(buffer.AsSpan(0, _tickSize));
    }

    private int FillBuffer(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < _tickSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, offset, _tickSize - offset);
            if (read == 0)
            {
                return offset;
            }

            offset += read;
        }

        return offset;
    }

    private async ValueTask<int> FillBufferAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < _tickSize)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, _tickSize - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return offset;
            }

            offset += read;
        }

        return offset;
    }

    private void ValidateStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }
    }
}