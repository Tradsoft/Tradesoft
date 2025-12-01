using System;
using System.Threading.Tasks;

namespace TradeSoft.Core.Interfaces
{
    public interface IDataDownloader
    {
        Task<byte[]> DownloadDataAsync(string symbol, DateTime date, int hour);
    }
}