using System.Collections.Concurrent;

namespace TradeSoft.Infrastructure.Streaming;

/// <summary>
/// Streams a queue of files sequentially without loading them entirely into memory.
/// Each file is opened just-in-time and disposed once the caller moves to the next one.
/// </summary>
public sealed class MultiFileStreamer
{
    private readonly ConcurrentQueue<string> _fileQueue;
    private readonly Func<string, Stream> _streamFactory;

    public MultiFileStreamer(IEnumerable<string> filePaths, Func<string, Stream>? streamFactory = null)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        _fileQueue = new ConcurrentQueue<string>(filePaths);
        _streamFactory = streamFactory ?? OpenReadSequential;
    }

    public int RemainingFiles => _fileQueue.Count;

    /// <summary>
    /// Opens each file sequentially and yields its stream.
    /// The provided stream remains valid until the consumer advances the enumerator.
    /// </summary>
    public async IAsyncEnumerable<Stream> StreamFilesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (_fileQueue.TryDequeue(out var nextFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stream = _streamFactory(nextFile);
            try
            {
                yield return stream;
            }
            finally
            {
                if (stream is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    stream.Dispose();
                }
            }
        }
    }

    private static Stream OpenReadSequential(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found for streaming.", path);
        }

        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }
}