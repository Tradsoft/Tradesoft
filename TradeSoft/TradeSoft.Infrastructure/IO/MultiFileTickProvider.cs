using Microsoft.Extensions.Logging; 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TradeSoft.Core.Domain;

namespace TradeSoft.Infrastructure.IO
{
    public class MultiFileTickProvider
    {
        private readonly string _directoryPath;
        private readonly ILogger<MultiFileTickProvider> _logger;

        private static readonly Regex FilePattern = new Regex(@"_(\d{4})_(\d{2})\.bin$");

        public MultiFileTickProvider(string directoryPath, ILogger<MultiFileTickProvider> logger)
        {
            if (!Directory.Exists(directoryPath))
            {
                logger.LogError("Le dossier source {DirectoryPath} est introuvable.", directoryPath);
                throw new DirectoryNotFoundException($"Le dossier {directoryPath} n'existe pas.");
            }

            _directoryPath = directoryPath;
            _logger = logger;
        }

        public IEnumerable<TickData> GetTicks(string symbol, DateTime start, DateTime end)
        {
            var loaders = GetLoaders(symbol, start, end);
            long startTicks = start.Ticks;
            long endTicks = end.Ticks;

            foreach (var loader in loaders)
            {
                using (loader)
                {
                    foreach (var tick in ReadTicksInRange(loader, startTicks, endTicks))
                    {
                        yield return tick;
                    }
                }
            }
        }

        private static List<TickData> ReadTicksInRange(BinaryTickLoader loader, long startTicks, long endTicks)
        {
            if (loader.Count == 0)
            {
                return new List<TickData>();
            }

            var span = loader.GetTicksAsSpan(loader.Count);
            var ticks = new List<TickData>(span.Length);

            for (int i = 0; i < span.Length; i++)
            {
                TickData tick = span[i];
                if (tick.Timestamp >= startTicks && tick.Timestamp <= endTicks)
                {
                    ticks.Add(tick);
                }
            }

            return ticks;
        }

        public static string? FindDataDirectory(string symbol = "EURUSD")
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string installPath = Path.Combine(baseDir, "Data", symbol);
            if (Directory.Exists(installPath))
            {
                return installPath;
            }

            string? current = baseDir;
            for (int i = 0; i <= 6 && current != null; i++)
            {
                string directData = Path.Combine(current, "Data", symbol);
                if (Directory.Exists(directData))
                {
                    return directData;
                }

                string nestedData = Path.Combine(current, "TradeSoft", "Data", symbol);
                if (Directory.Exists(nestedData))
                {
                    return nestedData;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            var fallbackPaths = new[]
            {
                $@"C:\Users\FlowUP\source\repos\GitHub\Tradesoft\Data\{symbol}",
                $@"C:\Users\louis\source\repos\Tradesoft\Data\{symbol}",
                $@"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\{symbol}\{symbol}",
                $@"C:\Users\Pc\Bureau\Projet\Data\{symbol}",
                $@"C:\Users\inesm\OneDrive\Documents\M2\Tradesoft\Data\{symbol}"
            };

            return fallbackPaths.FirstOrDefault(Directory.Exists);
        }

        public IEnumerable<BinaryTickLoader> GetLoaders(string symbol, DateTime start, DateTime end)
        {
            _logger.LogInformation("Recherche de fichiers pour '{Symbol}' dans {Directory}...", symbol, _directoryPath);

            var files = Directory.GetFiles(_directoryPath, $"*{symbol}*.bin");

            if (files.Length == 0)
            {
                _logger.LogWarning("Aucun fichier trouvé pour le symbole '{Symbol}'.", symbol);
                yield break;
            }

            var fileInfos = files
                .Select(path => new
                {
                    Path = path,
                    Date = ParseFileDate(path)
                })
                .Where(x => x.Date != DateTime.MinValue)
                .OrderBy(x => x.Date)
                .ToList();

            _logger.LogInformation("{Count} fichiers identifiés. Filtrage pour la période {Start:d} à {End:d}.", fileInfos.Count, start, end);

            int loadedCount = 0;

            foreach (var file in fileInfos)
            {
                DateTime fileStart = file.Date;
                DateTime fileEnd = file.Date.AddMonths(1).AddTicks(-1);

                if (fileEnd >= start && fileStart <= end)
                {
                    string fileName = Path.GetFileName(file.Path);
                    _logger.LogInformation("Chargement du segment : {FileName} ({Date:MMM yyyy})", fileName, file.Date);

                    loadedCount++;
                    yield return new BinaryTickLoader(file.Path);
                }
            }

            if (loadedCount == 0)
            {
                _logger.LogWarning("Aucun fichier ne correspond à la plage de dates demandée.");
            }
        }

        private DateTime ParseFileDate(string filePath)
        {
            var match = FilePattern.Match(filePath);
            if (match.Success)
            {
                int year = int.Parse(match.Groups[1].Value);
                int month = int.Parse(match.Groups[2].Value);
                return new DateTime(year, month, 1);
            }

            _logger.LogWarning("Format de fichier ignoré (date introuvable) : {FilePath}", Path.GetFileName(filePath));
            return DateTime.MinValue;
        }
    }
}