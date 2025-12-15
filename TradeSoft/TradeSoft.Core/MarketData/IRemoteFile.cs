namespace TradeSoft.Core.MarketData;

public interface IRemoteFile
{
    Task DownloadAsync(string key, Stream destination, IProgress<long>? progress, CancellationToken ct);
}
