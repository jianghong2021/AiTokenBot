using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace AiTokenBot.Data
{
    public partial class DatabaseService
    {
        private static DatabaseService? _instance;
        public static DatabaseService Instance => _instance ??= new DatabaseService();

        private readonly string _dbPath;

        private DatabaseService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aitokenbot.db");
            Initialize();
        }

        private void Initialize()
        {
            using var conn = OpenConnection();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Robots (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Strategy TEXT DEFAULT '',
                    Personality TEXT DEFAULT '',
                    MinProfitPercent TEXT DEFAULT '',
                    MaxTradeAmount TEXT DEFAULT '',
                    SlippagePercent TEXT DEFAULT '',
                    IsRunning INTEGER DEFAULT 0,
                    Balance REAL DEFAULT 0,
                    TodayPnL TEXT DEFAULT '$ 0.00',
                    TotalPnL TEXT DEFAULT '$ 0.00',
                    LlmModelId INTEGER DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS Positions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RobotId INTEGER NOT NULL REFERENCES Robots(Id) ON DELETE CASCADE,
                    Token TEXT DEFAULT '',
                    Amount TEXT DEFAULT '',
                    AvgPrice TEXT DEFAULT '',
                    CurrentPrice TEXT DEFAULT '',
                    PnL TEXT DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS TradeRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Time TEXT DEFAULT '',
                    Robot TEXT DEFAULT '',
                    Pair TEXT DEFAULT '',
                    Direction TEXT DEFAULT '',
                    Detail TEXT DEFAULT '',
                    PnL TEXT DEFAULT '',
                    Status TEXT DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS Wallets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Address TEXT DEFAULT '',
                    ImportType TEXT DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS LLMPlatforms (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    BaseUrl TEXT DEFAULT '',
                    ApiKey TEXT DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS LLMModels (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlatformId INTEGER NOT NULL REFERENCES LLMPlatforms(Id) ON DELETE CASCADE,
                    Name TEXT DEFAULT '',
                    ModelId TEXT DEFAULT ''
                );
            ";
            cmd.ExecuteNonQuery();

            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Robots ADD COLUMN LlmModelId INTEGER DEFAULT 0";
                alterCmd.ExecuteNonQuery();
            }
            catch { /* column already exists */ }
        }

        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return conn;
        }
    }
}
