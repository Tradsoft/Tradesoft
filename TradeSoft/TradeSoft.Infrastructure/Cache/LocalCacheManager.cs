using System.Collections.Concurrent;
using System.Security.Cryptography;
using TradeSoft.Core.MarketData;         

namespace TradeSoft.Infrastructure.Cache;

public sealed class LocalCacheManager
{
    private readonly string _cacheRoot;
    private readonly IRemoteFile _store;

    // Lock par fichier (évite 10 downloads simultanés du même .bin)
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.OrdinalIgnoreCase);

    public LocalCacheManager(string cacheRoot, IRemoteFile store)
    {
        _cacheRoot = cacheRoot;
        _store = store;
    }

    public async Task<string> EnsureAsync(RemoteFile file, CancellationToken ct, IProgress<long>? progress = null)
    {
        Directory.CreateDirectory(_cacheRoot);

        var finalPath = Path.Combine(_cacheRoot, file.LocalFileName);
        var sem = _locks.GetOrAdd(finalPath, _ => new SemaphoreSlim(1, 1));

        await sem.WaitAsync(ct);
        try
        {
            // 1) Cache HIT : fichier présent + hash OK => aucun download
            if (File.Exists(finalPath))
            {
                var localHash = Sha256HexOfFile(finalPath);
                if (HashEquals(localHash, file.Sha256Hex))
                    return finalPath;
            }

            // 2) Cache MISS ou fichier corrompu : download vers .tmp
            var tmpPath = finalPath + ".tmp";
            TryDelete(tmpPath);

            await using (var fs = new FileStream(
                tmpPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                options: FileOptions.SequentialScan))
            {
                // Important : on passe bien le ct => ton test d'annulation va marcher
                await _store.DownloadAsync(file.Key, fs, progress, ct);
            }

            // 3) Vérif hash du .tmp
            var tmpHash = Sha256HexOfFile(tmpPath);
            if (!HashEquals(tmpHash, file.Sha256Hex))
            {
                TryDelete(tmpPath);
                throw new InvalidDataException(
                    $"Hash mismatch for {file.LocalFileName}. Expected={file.Sha256Hex} Actual={tmpHash}");
            }

            // 4) Commit atomique
            if (File.Exists(finalPath))
            {
                // remplace atomiquement (Windows)
                File.Replace(tmpPath, finalPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmpPath, finalPath);
            }

            return finalPath;
        }
        catch
        {
            // Cleanup best-effort
            TryDelete(finalPath + ".tmp");
            throw;
        }
        finally
        {
            sem.Release();
        }
    }

    private static bool HashEquals(string a, string b)
        => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup, on ignore
        }
    }

    private static string Sha256HexOfFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
