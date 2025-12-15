using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradeSoft.Core.MarketData;
using TradeSoft.Infrastructure.Cache;
using Xunit;

public class LocalCacheManagerTests
{
    [Fact]
    public async Task EnsureAsync_FileExists_WithValidHash_ShouldNotDownload() // Si fichier existe et hash valide, ne pas retélécharger
    {
        using var temp = new TempDir();
        var content = Encoding.UTF8.GetBytes("hello");
        var fileName = "EURUSD_2019.bin";
        var finalPath = Path.Combine(temp.Path, fileName);

        await File.WriteAllBytesAsync(finalPath, content);
        var sha = Sha256Hex(content);

        var store = new FakeRemoteFileStore(content);
        var cache = new LocalCacheManager(temp.Path, store);

        var rf = new RemoteFile("k", fileName, sha, content.Length);

        var path = await cache.EnsureAsync(rf, CancellationToken.None);

        Assert.Equal(finalPath, path);
        Assert.Equal(0, store.DownloadCalls);
    }

    [Fact]
    public async Task EnsureAsync_FileMissing_ShouldDownload_AndCreateFinalFile() // Si fichier absent, le télécharger 
    {
        using var temp = new TempDir();
        var content = Encoding.UTF8.GetBytes("downloaded");
        var sha = Sha256Hex(content);

        var store = new FakeRemoteFileStore(content);
        var cache = new LocalCacheManager(temp.Path, store);

        var rf = new RemoteFile("remote/key", "EURUSD_2020.bin", sha, content.Length);

        var path = await cache.EnsureAsync(rf, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
        Assert.Equal(1, store.DownloadCalls);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task EnsureAsync_FileExists_WithInvalidHash_ShouldRedownload_AndReplace() // Si fichier existe mais hash invalide, retélécharger et remplacer
    {
        using var temp = new TempDir();
        var bad = Encoding.UTF8.GetBytes("bad");
        var good = Encoding.UTF8.GetBytes("good content");
        var shaGood = Sha256Hex(good);

        var fileName = "EURUSD_2021.bin";
        var finalPath = Path.Combine(temp.Path, fileName);

        await File.WriteAllBytesAsync(finalPath, bad); // corrompu

        var store = new FakeRemoteFileStore(good);
        var cache = new LocalCacheManager(temp.Path, store);

        var rf = new RemoteFile("remote/key", fileName, shaGood, good.Length);

        var path = await cache.EnsureAsync(rf, CancellationToken.None);

        Assert.Equal(finalPath, path);
        Assert.Equal(good, await File.ReadAllBytesAsync(finalPath));
        Assert.Equal(1, store.DownloadCalls);
    }

    [Fact]
    public async Task EnsureAsync_CancelDuringDownload_ShouldNotLeaveFinalFile() // Si annulation pendant téléchargement, ne pas laisser de fichier final incomplet
    {
        using var temp = new TempDir();
        var content = new byte[1024 * 1024];
        Random.Shared.NextBytes(content);
        var sha = Sha256Hex(content);

        var store = new CancellableRemoteStore(content, chunkSize: 64 * 1024, delayMs: 30);
        var cache = new LocalCacheManager(temp.Path, store);

        var rf = new RemoteFile("remote/key", "EURUSD_2022.bin", sha, content.Length);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.EnsureAsync(rf, cts.Token));

        var finalPath = Path.Combine(temp.Path, rf.LocalFileName);
        Assert.False(File.Exists(finalPath));
        Assert.False(File.Exists(finalPath + ".tmp"));
    }

    [Fact]
    public async Task EnsureAsync_ConcurrentRequests_ShouldDownloadOnlyOnce() // Plusieurs requêtes concurrentes pour le même fichier ne doivent déclencher qu'un seul téléchargement
    {
        using var temp = new TempDir();
        var content = Encoding.UTF8.GetBytes("same file");
        var sha = Sha256Hex(content);

        var store = new SlowCountingStore(content, delayMs: 50);
        var cache = new LocalCacheManager(temp.Path, store);

        var rf = new RemoteFile("remote/key", "EURUSD_2018.bin", sha, content.Length);

        var tasks = new Task<string>[10];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = cache.EnsureAsync(rf, CancellationToken.None);

        await Task.WhenAll(tasks);

        Assert.Equal(1, store.DownloadCalls);
        foreach (var t in tasks)
            Assert.True(File.Exists(t.Result));
    }

    // -------- Helpers --------

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        return Convert.ToHexString(hash);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TradeSoftTests_" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }

    private sealed class FakeRemoteFileStore : IRemoteFile
    {
        private readonly byte[] _content;
        public int DownloadCalls { get; private set; }
        public FakeRemoteFileStore(byte[] content) => _content = content;

        public async Task DownloadAsync(string key, Stream destination, IProgress<long>? progress, CancellationToken ct)
        {
            DownloadCalls++;
            await destination.WriteAsync(_content, ct);
            progress?.Report(_content.Length);
        }
    }

    private sealed class SlowCountingStore : IRemoteFile
    {
        private readonly byte[] _content;
        private readonly int _delayMs;
        public int DownloadCalls => _calls;
        private int _calls;

        public SlowCountingStore(byte[] content, int delayMs)
        {
            _content = content;
            _delayMs = delayMs;
        }

        public async Task DownloadAsync(string key, Stream destination, IProgress<long>? progress, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(_delayMs, ct);
            await destination.WriteAsync(_content, ct);
            progress?.Report(_content.Length);
        }
    }

    private sealed class CancellableRemoteStore : IRemoteFile
    {
        private readonly byte[] _content;
        private readonly int _chunkSize;
        private readonly int _delayMs;

        public CancellableRemoteStore(byte[] content, int chunkSize, int delayMs)
        {
            _content = content;
            _chunkSize = chunkSize;
            _delayMs = delayMs;
        }

        public async Task DownloadAsync(string key, Stream destination, IProgress<long>? progress, CancellationToken ct)
        {
            long written = 0;
            for (int offset = 0; offset < _content.Length; offset += _chunkSize)
            {
                ct.ThrowIfCancellationRequested();
                var len = Math.Min(_chunkSize, _content.Length - offset);
                await destination.WriteAsync(_content.AsMemory(offset, len), ct);
                written += len;
                progress?.Report(written);
                await Task.Delay(_delayMs, ct);
            }
        }
    }
}
