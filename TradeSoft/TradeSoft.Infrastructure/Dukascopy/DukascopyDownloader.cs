using System;
using System.Net.Http;
using System.Threading.Tasks;
using TradeSoft.Core.Interfaces;

namespace TradeSoft.Infrastructure.Dukascopy
{
    public class DukascopyDownloader : IDataDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public DukascopyDownloader(string baseUrl)
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
        }

        public async Task<byte[]> DownloadDataAsync(string symbol, DateTime date, int hour)
        {
            // Conversion des dates pour Dukascopy (Janvier = 00)
            string monthStr = (date.Month - 1).ToString("00");
            string dayStr = date.Day.ToString("00");
            string hourStr = hour.ToString("00");

            string url = $"{_baseUrl}/{symbol.ToUpper()}/{date.Year}/{monthStr}/{dayStr}/{hourStr}h_ticks.bi5";

            Console.WriteLine($"[Download] Tentative : {url}");

            try
            {
                return await _httpClient.GetByteArrayAsync(url);
            }
            catch (HttpRequestException)
            {
                Console.WriteLine($"[Info] Pas de données pour {hour}h (Marché fermé ou 404).");
                return Array.Empty<byte>();
            }
        }
    }
}