using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TradeSoft.Core.Entities;
using TradeSoft.Core.Interfaces;

namespace TradeSoft.Infrastructure.Persistence
{
    public class SqlMarketRepository : IMarketRepository
    {
        private readonly string _connectionString;

        public SqlMarketRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void InitializeDatabase()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
            }
        }

        public void SaveTicks(List<MarketQuote> ticks)
        {
            if (ticks == null || !ticks.Any()) return;

            // 1. Préparation de la structure de données en mémoire
            using (var table = new DataTable())
            {
                table.Columns.Add("Symbol", typeof(string));
                table.Columns.Add("Timestamp", typeof(DateTime));
                table.Columns.Add("Ask", typeof(decimal));
                table.Columns.Add("Bid", typeof(decimal));
                table.Columns.Add("Volume", typeof(long));

                foreach (var tick in ticks)
                {
                    table.Rows.Add(tick.Symbol, tick.Timestamp, tick.Ask, tick.Bid, tick.Volume);
                }

                // 2. Envoi Massif vers Azure
                using (var bulkCopy = new SqlBulkCopy(_connectionString))
                {
                    bulkCopy.DestinationTableName = "MarketHistory";
                    bulkCopy.BatchSize = 10000; // Envoie par paquets de 10 000
                    bulkCopy.BulkCopyTimeout = 600; // 10 minutes max avant timeout

                    // Mapping explicite pour éviter les erreurs
                    bulkCopy.ColumnMappings.Add("Symbol", "Symbol");
                    bulkCopy.ColumnMappings.Add("Timestamp", "Timestamp");
                    bulkCopy.ColumnMappings.Add("Ask", "Ask");
                    bulkCopy.ColumnMappings.Add("Bid", "Bid");
                    bulkCopy.ColumnMappings.Add("Volume", "Volume");

                    bulkCopy.WriteToServer(table);
                }
            }
        }

        public List<MarketQuote> GetAllTicks()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.Query<MarketQuote>("SELECT TOP 2000 * FROM MarketHistory ORDER BY Timestamp ASC").ToList();
            }
        }
    }
}