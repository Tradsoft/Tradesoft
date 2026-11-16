using System.Collections.Generic;
using System.Data;
using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using TradeSoft.Core.DTO;

namespace TradeSoft.Infrastructure.Persistence
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string dbPath = "tradesoft.db")
        {
            SQLitePCL.Batteries.Init();
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = GetConnection();
            connection.Open();

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Trades (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Strategy TEXT,
                    Symbol TEXT,
                    EntryTime TEXT,
                    ExitTime TEXT,
                    Side TEXT,
                    EntryPrice REAL,
                    ExitPrice REAL,
                    Quantity REAL,
                    PnL REAL,
                    Commission REAL,
                    DurationMinutes REAL
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS EquityCurve (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BacktestId TEXT, -- Pour lier à un run spécifique
                    Timestamp TEXT,
                    Equity REAL
                )");
        }

        public IDbConnection GetConnection() => new SqliteConnection(_connectionString);

        public void SaveBacktestResult(BacktestResult result, BacktestConfig config)
        {
            using var connection = GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (result.ClosedTrades != null && result.ClosedTrades.Count > 0)
                {
                    foreach (var trade in result.ClosedTrades)
                    {
                        connection.Execute(@"
                    INSERT INTO Trades 
                    (Strategy, Symbol, EntryTime, ExitTime, Side, EntryPrice, ExitPrice, Quantity, PnL, Commission, DurationMinutes) 
                    VALUES 
                    (@Strategy, @Symbol, @EntryTime, @ExitTime, @Side, @EntryPrice, @ExitPrice, @Quantity, @PnL, @Commission, @DurationMinutes)",
                            new
                            {
                                Strategy = config.Strategy.ToString(),
                                Symbol = string.IsNullOrEmpty(trade.Symbol) ? config.Symbol : trade.Symbol,
                                EntryTime = trade.EntryTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                ExitTime = trade.ExitTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                Side = trade.Direction.ToString(),
                                trade.EntryPrice,
                                trade.ExitPrice,
                                trade.Quantity,
                                trade.PnL,
                                trade.Commission,
                                DurationMinutes = trade.DurationSeconds / 60.0
                            }, transaction);
                    }
                }
                else
                {
                    foreach (var trade in result.Trades)
                    {
                        connection.Execute(@"
                    INSERT INTO Trades 
                    (Strategy, Symbol, EntryTime, ExitTime, Side, EntryPrice, ExitPrice, Quantity, PnL, Commission, DurationMinutes) 
                    VALUES 
                    (@Strategy, @Symbol, @Time, @Time, @Side, @Price, @Price, @Quantity, @PnL, 0, 0)",
                            new
                            {
                                Strategy = config.Strategy.ToString(),
                                config.Symbol,
                                Time = trade.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                                trade.Side,
                                trade.Price,
                                trade.Quantity,
                                trade.PnL
                            }, transaction);
                    }
                }

                foreach (var point in result.EquityCurve)
                {
                    connection.Execute(@"
                INSERT INTO EquityCurve (BacktestId, Timestamp, Equity) 
                VALUES (@BacktestId, @Timestamp, @Equity)",
                        new
                        {
                            BacktestId = runId,
                            Timestamp = point.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                            Equity = point.Value
                        }, transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}