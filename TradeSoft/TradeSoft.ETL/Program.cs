using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeSoft.Core.Entities;
using TradeSoft.Core.Interfaces;
using TradeSoft.Infrastructure.Dukascopy;
using TradeSoft.Infrastructure.Persistence;
using TradeSoft.ETL;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("--- TradeSoft ETL V5 : TURBO PARALLEL (2015 - Now) ---");

var services = new ServiceCollection();
var startup = new Startup();
startup.ConfigureServices(services);
var provider = services.BuildServiceProvider();

var config = provider.GetRequiredService<IConfiguration>();
string connString = config.GetConnectionString("DefaultConnection");
string dukascopyUrl = config["DataSources:DukascopyBaseUrl"];

var initRepo = new SqlMarketRepository(connString);
try { initRepo.InitializeDatabase(); } catch { }

string symbol = "EURUSD";

DateTime specificStartDate = new DateTime(2015, 1, 1, 0, 0, 0);
DateTime endDate = DateTime.Today.AddDays(-1);

var allDays = new List<DateTime>();
for (var dt = specificStartDate.Date; dt <= endDate.Date; dt = dt.AddDays(1))
{
    if (dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday)
    {
        allDays.Add(dt);
    }
}

Console.WriteLine($"🚀 Démarrage TURBO pour {symbol}");
Console.WriteLine($"📅 Période : {specificStartDate.ToShortDateString()} -> {endDate.ToShortDateString()}");
Console.WriteLine($"📅 {allDays.Count} jours à traiter en parallèle.");
Console.WriteLine("---------------------------------------------");

Stopwatch globalWatch = Stopwatch.StartNew();
long totalTicks = 0;
object consoleLock = new object();

var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10 };

await Parallel.ForEachAsync(allDays, parallelOptions, async (currentDate, token) =>
{
    IDataDownloader localDownloader = new DukascopyDownloader(dukascopyUrl);
    IMarketRepository localRepo = new SqlMarketRepository(connString);
    var dayTicks = new ConcurrentBag<MarketQuote>();

    int startHour = 0;

    if (currentDate.Date == specificStartDate.Date)
    {
        startHour = specificStartDate.Hour;
    }

    var hoursToDownload = Enumerable.Range(startHour, 24 - startHour);

    await Parallel.ForEachAsync(hoursToDownload, async (hour, innerToken) =>
    {
        try
        {
            byte[] data = await localDownloader.DownloadDataAsync(symbol, currentDate, hour);
            if (data.Length > 0)
            {
                DateTime hourStart = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hour, 0, 0);
                var hourTicks = DukascopyParser.Parse(data, hourStart, symbol);

                foreach (var t in hourTicks) dayTicks.Add(t);
            }
        }
        catch { }
    });

    if (!dayTicks.IsEmpty)
    {
        var sortedTicks = dayTicks.OrderBy(t => t.Timestamp).ToList();
        localRepo.SaveTicks(sortedTicks);

        Interlocked.Add(ref totalTicks, sortedTicks.Count);

        lock (consoleLock)
        {
            Console.WriteLine($"✅ {currentDate.ToShortDateString()} terminé ({sortedTicks.Count} ticks)");
        }
    }
});

globalWatch.Stop();
Console.WriteLine("---------------------------------------------");
Console.WriteLine($"🏁 FINI ! {totalTicks:N0} ticks en {globalWatch.Elapsed.TotalSeconds:F0}s");
Console.ReadLine();