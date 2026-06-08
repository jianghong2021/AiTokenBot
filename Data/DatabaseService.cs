using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using AiTokenBot.Views;

namespace AiTokenBot.Data
{
    public class DatabaseService
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

            // Add LlmModelId column if not exists
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

        // ===== Robots =====

        public List<RobotModel> LoadRobots()
        {
            var robots = new List<RobotModel>();
            using var conn = OpenConnection();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Robots ORDER BY Id";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var robot = ReadRobot(reader);
                robot.Positions = LoadPositions(conn, robot.Id);
                robots.Add(robot);
            }

            if (robots.Count == 0)
            {
                SeedRobots(conn);
                robots.AddRange(LoadRobotsFromConnection(conn));
            }

            return robots;
        }

        private List<RobotModel> LoadRobotsFromConnection(SqliteConnection conn)
        {
            var robots = new List<RobotModel>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Robots ORDER BY Id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var robot = ReadRobot(reader);
                robot.Positions = LoadPositions(conn, robot.Id);
                robots.Add(robot);
            }
            return robots;
        }

        private static RobotModel ReadRobot(SqliteDataReader reader)
        {
            return new RobotModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Strategy = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Personality = reader.IsDBNull(3) ? "" : reader.GetString(3),
                MinProfitPercent = reader.IsDBNull(4) ? "" : reader.GetString(4),
                MaxTradeAmount = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SlippagePercent = reader.IsDBNull(6) ? "" : reader.GetString(6),
                IsRunning = !reader.IsDBNull(7) && reader.GetInt32(7) == 1,
                Balance = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                TodayPnL = reader.IsDBNull(9) ? "$ 0.00" : reader.GetString(9),
                TotalPnL = reader.IsDBNull(10) ? "$ 0.00" : reader.GetString(10),
                LlmModelId = reader.FieldCount > 11 && !reader.IsDBNull(11) ? reader.GetInt32(11) : 0,
            };
        }

        private void SeedRobots(SqliteConnection conn)
        {
            var robots = new[]
            {
                (Name: "套利猎手", Strategy: "SOL/USDC 跨池套利", Personality: "你是一个激进的套利机器人。专门扫描 Solana 链上所有 DEX 的 SOL/USDC 价格差异。", MinProfit: "0.3%", MaxTrade: "8 SOL", Slippage: "0.5%", Running: true, Balance: 5000m, Today: "+ $ 92.10", Total: "+ $ 1,240.50"),
                (Name: "趋势追踪", Strategy: "BONK/JUP 波段交易", Personality: "你是一个耐心的趋势跟踪机器人。专注于 BONK 和 JUP 代币的波段操作。", MinProfit: "1.0%", MaxTrade: "3 SOL", Slippage: "1.5%", Running: true, Balance: 3000m, Today: "+ $ 45.20", Total: "+ $ 870.30"),
                (Name: "信号侦察", Strategy: "全代币异常检测", Personality: "你是一个信号侦察机器人。不主动执行交易，只监控全代币的异常价格波动。", MinProfit: "2.0%", MaxTrade: "2 SOL", Slippage: "2.0%", Running: false, Balance: 1500m, Today: "$ 0.00", Total: "+ $ 120.00"),
            };

            using var tx = conn.BeginTransaction();
            foreach (var r in robots)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Robots (Name, Strategy, Personality, MinProfitPercent, MaxTradeAmount, SlippagePercent, IsRunning, Balance, TodayPnL, TotalPnL, LlmModelId)
                    VALUES (@n, @s, @p, @mp, @mt, @sl, @ir, @b, @td, @tl, 0); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@n", r.Name);
                cmd.Parameters.AddWithValue("@s", r.Strategy);
                cmd.Parameters.AddWithValue("@p", r.Personality);
                cmd.Parameters.AddWithValue("@mp", r.MinProfit);
                cmd.Parameters.AddWithValue("@mt", r.MaxTrade);
                cmd.Parameters.AddWithValue("@sl", r.Slippage);
                cmd.Parameters.AddWithValue("@ir", r.Running ? 1 : 0);
                cmd.Parameters.AddWithValue("@b", r.Balance);
                cmd.Parameters.AddWithValue("@td", r.Today);
                cmd.Parameters.AddWithValue("@tl", r.Total);
                var robotId = (long)cmd.ExecuteScalar()!;

                // Seed positions for first two robots
                if (r.Name == "套利猎手")
                {
                    InsertPosition(conn, robotId, "SOL", "3.25", "$ 141.50", "$ 142.30", "+ $ 2.60");
                    InsertPosition(conn, robotId, "USDC", "2,100", "$ 1.00", "$ 1.00", "$ 0.00");
                }
                else if (r.Name == "趋势追踪")
                {
                    InsertPosition(conn, robotId, "BONK", "12,000", "$ 0.02", "$ 0.022", "+ $ 24.00");
                    InsertPosition(conn, robotId, "JUP", "85", "$ 1.72", "$ 1.68", "- $ 3.40");
                }
            }
            tx.Commit();
        }

        public int SaveRobot(RobotModel robot)
        {
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            if (robot.Id == 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Robots (Name, Strategy, Personality, MinProfitPercent, MaxTradeAmount, SlippagePercent, IsRunning, Balance, TodayPnL, TotalPnL, LlmModelId)
                    VALUES (@n,@s,@p,@mp,@mt,@sl,@ir,@b,@td,@tl,@lmi); SELECT last_insert_rowid();";
                BindRobot(cmd, robot);
                robot.Id = (int)(long)cmd.ExecuteScalar()!;
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE Robots SET Name=@n,Strategy=@s,Personality=@p,MinProfitPercent=@mp,MaxTradeAmount=@mt,SlippagePercent=@sl,IsRunning=@ir,Balance=@b,TodayPnL=@td,TotalPnL=@tl,LlmModelId=@lmi WHERE Id=@id";
                BindRobot(cmd, robot);
                cmd.Parameters.AddWithValue("@id", robot.Id);
                cmd.ExecuteNonQuery();
            }

            // Sync positions
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM Positions WHERE RobotId = @rid";
            delCmd.Parameters.AddWithValue("@rid", robot.Id);
            delCmd.ExecuteNonQuery();

            foreach (var pos in robot.Positions)
                InsertPosition(conn, robot.Id, pos.Token, pos.Amount, pos.AvgPrice, pos.CurrentPrice, pos.PnL);

            tx.Commit();
            return robot.Id;
        }

        public void DeleteRobot(int id)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Robots WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static void BindRobot(SqliteCommand cmd, RobotModel r)
        {
            cmd.Parameters.AddWithValue("@n", r.Name);
            cmd.Parameters.AddWithValue("@s", r.Strategy);
            cmd.Parameters.AddWithValue("@p", r.Personality);
            cmd.Parameters.AddWithValue("@mp", r.MinProfitPercent);
            cmd.Parameters.AddWithValue("@mt", r.MaxTradeAmount);
            cmd.Parameters.AddWithValue("@sl", r.SlippagePercent);
            cmd.Parameters.AddWithValue("@ir", r.IsRunning ? 1 : 0);
            cmd.Parameters.AddWithValue("@b", r.Balance);
            cmd.Parameters.AddWithValue("@td", r.TodayPnL);
            cmd.Parameters.AddWithValue("@tl", r.TotalPnL);
            cmd.Parameters.AddWithValue("@lmi", r.LlmModelId);
        }

        private static void InsertPosition(SqliteConnection conn, long robotId, string token, string amount, string avg, string current, string pnl)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Positions (RobotId, Token, Amount, AvgPrice, CurrentPrice, PnL) VALUES (@rid,@t,@a,@ap,@cp,@p)";
            cmd.Parameters.AddWithValue("@rid", robotId);
            cmd.Parameters.AddWithValue("@t", token);
            cmd.Parameters.AddWithValue("@a", amount);
            cmd.Parameters.AddWithValue("@ap", avg);
            cmd.Parameters.AddWithValue("@cp", current);
            cmd.Parameters.AddWithValue("@p", pnl);
            cmd.ExecuteNonQuery();
        }

        private static List<PositionItem> LoadPositions(SqliteConnection conn, int robotId)
        {
            var positions = new List<PositionItem>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Token, Amount, AvgPrice, CurrentPrice, PnL FROM Positions WHERE RobotId = @rid ORDER BY Id";
            cmd.Parameters.AddWithValue("@rid", robotId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                positions.Add(new PositionItem
                {
                    Token = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Amount = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    AvgPrice = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CurrentPrice = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    PnL = reader.IsDBNull(4) ? "" : reader.GetString(4),
                });
            }
            return positions;
        }

        // ===== Trade Records =====

        public List<TradeRecord> LoadTradeRecords()
        {
            var records = new List<TradeRecord>();
            using var conn = OpenConnection();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM TradeRecords ORDER BY Id DESC";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                records.Add(ReadTrade(reader));
            }

            if (records.Count == 0)
            {
                SeedTrades(conn);
                records.AddRange(LoadTradeRecordsFromConnection(conn));
            }

            return records;
        }

        private List<TradeRecord> LoadTradeRecordsFromConnection(SqliteConnection conn)
        {
            var records = new List<TradeRecord>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM TradeRecords ORDER BY Id DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                records.Add(ReadTrade(reader));
            return records;
        }

        private static TradeRecord ReadTrade(SqliteDataReader reader)
        {
            return new TradeRecord
            {
                Id = reader.GetInt32(0),
                Time = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Robot = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Pair = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Direction = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Detail = reader.IsDBNull(5) ? "" : reader.GetString(5),
                PnL = reader.IsDBNull(6) ? null : reader.GetString(6),
                Status = reader.IsDBNull(7) ? "" : reader.GetString(7),
            };
        }

        private void SeedTrades(SqliteConnection conn)
        {
            var trades = new[]
            {
                ("2026-06-07 15:42", "套利猎手", "SOL/USDC", "买入", "0.85 SOL @ $142.30", "+ $1.22", "已完成"),
                ("2026-06-07 15:18", "趋势追踪", "BONK/SOL", "卖出", "1,200 BONK @ 0.027 SOL", "+ $3.40", "已完成"),
                ("2026-06-07 14:55", "套利猎手", "SOL/USDC", "卖出", "0.50 SOL @ $141.80", "+ $0.85", "已完成"),
                ("2026-06-07 14:30", "信号侦察", "JUP/SOL", "买入", "45 JUP @ 1.82 SOL", null, "信号"),
                ("2026-06-07 13:20", "套利猎手", "SOL/USDC", "买入", "1.20 SOL @ $140.95", "+ $2.10", "已完成"),
                ("2026-06-07 12:05", "趋势追踪", "BONK/SOL", "买入", "800 BONK @ 0.025 SOL", "- $1.15", "已完成"),
                ("2026-06-07 10:40", "套利猎手", "SOL/USDC", "卖出", "0.60 SOL @ $143.20", "+ $1.95", "已完成"),
            };

            using var tx = conn.BeginTransaction();
            foreach (var t in trades)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO TradeRecords (Time, Robot, Pair, Direction, Detail, PnL, Status) VALUES (@tm,@rb,@pr,@dr,@dt,@pl,@st)";
                cmd.Parameters.AddWithValue("@tm", t.Item1);
                cmd.Parameters.AddWithValue("@rb", t.Item2);
                cmd.Parameters.AddWithValue("@pr", t.Item3);
                cmd.Parameters.AddWithValue("@dr", t.Item4);
                cmd.Parameters.AddWithValue("@dt", t.Item5);
                cmd.Parameters.AddWithValue("@pl", (object?)t.Item6 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@st", t.Item7);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        // ===== Wallets =====

        public List<WalletInfo> LoadWallets()
        {
            var wallets = new List<WalletInfo>();
            using var conn = OpenConnection();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Wallets ORDER BY Id";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                wallets.Add(new WalletInfo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Address = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ImportType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                });
            }

            if (wallets.Count == 0)
            {
                SeedWallets(conn);
                wallets.AddRange(LoadWalletsFromConnection(conn));
            }

            return wallets;
        }

        private List<WalletInfo> LoadWalletsFromConnection(SqliteConnection conn)
        {
            var wallets = new List<WalletInfo>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Wallets ORDER BY Id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                wallets.Add(new WalletInfo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Address = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ImportType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                });
            }
            return wallets;
        }

        private void SeedWallets(SqliteConnection conn)
        {
            var wallets = new[]
            {
                (Name: "主钱包", Address: "8xHy2RqGcXnBk4pTvWmD9sLfZjE5aN6iYtUwHqKjRmVoPp3QxZyBdFhJl7Mk1AnC", Type: "助记词"),
                (Name: "交易专用", Address: "DfK3mNp2qR8sT5vW9xY1aB4cE7gH2jL6oP0uZ3yX5nM8kQ1rV4tW6", Type: "私钥"),
            };

            using var tx = conn.BeginTransaction();
            foreach (var w in wallets)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO Wallets (Name, Address, ImportType) VALUES (@n,@a,@t)";
                cmd.Parameters.AddWithValue("@n", w.Name);
                cmd.Parameters.AddWithValue("@a", w.Address);
                cmd.Parameters.AddWithValue("@t", w.Type);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public void SaveWallet(WalletInfo wallet)
        {
            using var conn = OpenConnection();
            if (wallet.Id == 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO Wallets (Name, Address, ImportType) VALUES (@n,@a,@t); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@n", wallet.Name);
                cmd.Parameters.AddWithValue("@a", wallet.Address);
                cmd.Parameters.AddWithValue("@t", wallet.ImportType);
                wallet.Id = (int)(long)cmd.ExecuteScalar()!;
            }
        }

        public void DeleteWallet(int id)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Wallets WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ===== Settings =====

        public Dictionary<string, string> LoadSettings()
        {
            var settings = new Dictionary<string, string>();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM Settings";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                settings[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);

            if (settings.Count == 0)
                SeedSettings(conn);

            // Re-read after seed
            if (settings.Count == 0)
            {
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT Key, Value FROM Settings";
                using var reader2 = cmd2.ExecuteReader();
                while (reader2.Read())
                    settings[reader2.GetString(0)] = reader2.IsDBNull(1) ? "" : reader2.GetString(1);
            }

            return settings;
        }

        public void SaveSetting(string key, string value)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        private void SeedSettings(SqliteConnection conn)
        {
            var defaults = new Dictionary<string, string>
            {
                ["rpc_url"] = "https://api.mainnet-beta.solana.com",
                ["jupiter_url"] = "https://quote-api.jup.ag/v6",
                ["jupiter_api_key"] = "",
                ["theme"] = "dark",
                ["notify_trade"] = "true",
                ["notify_alert"] = "true",
            };

            using var tx = conn.BeginTransaction();
            foreach (var kv in defaults)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO Settings (Key, Value) VALUES (@k, @v)";
                cmd.Parameters.AddWithValue("@k", kv.Key);
                cmd.Parameters.AddWithValue("@v", kv.Value);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        // ===== LLM =====

        public void SeedLlmIfEmpty()
        {
            using var conn = OpenConnection();
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM LLMPlatforms";
            var count = (long)check.ExecuteScalar()!;
            if (count > 0) return;

            using var tx = conn.BeginTransaction();

            // Insert DeepSeek platform
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO LLMPlatforms (Name, BaseUrl, ApiKey) VALUES ('DeepSeek', 'https://api.deepseek.com', ''); SELECT last_insert_rowid();";
            var platformId = (long)ins.ExecuteScalar()!;

            // Insert DeepSeek V4 Pro model
            using var insM = conn.CreateCommand();
            insM.CommandText = "INSERT INTO LLMModels (PlatformId, Name, ModelId) VALUES (@p, 'DeepSeek V4 Pro', 'deepseek-v4-pro'); SELECT last_insert_rowid();";
            insM.Parameters.AddWithValue("@p", platformId);
            insM.ExecuteScalar();

            tx.Commit();
        }

        public List<LlmPlatform> LoadPlatforms()
        {
            var platforms = new List<LlmPlatform>();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LLMPlatforms ORDER BY Id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                platforms.Add(ReadPlatform(reader));

            SeedLlmIfEmpty();
            if (platforms.Count == 0)
            {
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT * FROM LLMPlatforms ORDER BY Id";
                using var reader2 = cmd2.ExecuteReader();
                while (reader2.Read())
                    platforms.Add(ReadPlatform(reader2));
            }

            foreach (var p in platforms)
                p.Models = LoadModels(conn, p.Id);

            return platforms;
        }

        private static LlmPlatform ReadPlatform(SqliteDataReader reader) => new()
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            BaseUrl = reader.IsDBNull(2) ? "" : reader.GetString(2),
            ApiKey = reader.IsDBNull(3) ? "" : reader.GetString(3),
        };

        private static List<LlmModel> LoadModels(SqliteConnection conn, int platformId)
        {
            var models = new List<LlmModel>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ModelId FROM LLMModels WHERE PlatformId = @p ORDER BY Id";
            cmd.Parameters.AddWithValue("@p", platformId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                models.Add(new LlmModel
                {
                    Id = reader.GetInt32(0),
                    PlatformId = platformId,
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ModelId = reader.IsDBNull(2) ? "" : reader.GetString(2),
                });
            return models;
        }

        public void SavePlatform(LlmPlatform platform)
        {
            using var conn = OpenConnection();
            if (platform.Id == 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO LLMPlatforms (Name, BaseUrl, ApiKey) VALUES (@n,@u,@k); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@n", platform.Name);
                cmd.Parameters.AddWithValue("@u", platform.BaseUrl);
                cmd.Parameters.AddWithValue("@k", platform.ApiKey);
                platform.Id = (int)(long)cmd.ExecuteScalar()!;
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE LLMPlatforms SET Name=@n, BaseUrl=@u, ApiKey=@k WHERE Id=@id";
                cmd.Parameters.AddWithValue("@n", platform.Name);
                cmd.Parameters.AddWithValue("@u", platform.BaseUrl);
                cmd.Parameters.AddWithValue("@k", platform.ApiKey);
                cmd.Parameters.AddWithValue("@id", platform.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeletePlatform(int id)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM LLMPlatforms WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public LlmModel SaveModel(LlmModel model)
        {
            using var conn = OpenConnection();
            if (model.Id == 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO LLMModels (PlatformId, Name, ModelId) VALUES (@p,@n,@m); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@p", model.PlatformId);
                cmd.Parameters.AddWithValue("@n", model.Name);
                cmd.Parameters.AddWithValue("@m", model.ModelId);
                model.Id = (int)(long)cmd.ExecuteScalar()!;
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE LLMModels SET Name=@n, ModelId=@m WHERE Id=@id";
                cmd.Parameters.AddWithValue("@n", model.Name);
                cmd.Parameters.AddWithValue("@m", model.ModelId);
                cmd.Parameters.AddWithValue("@id", model.Id);
                cmd.ExecuteNonQuery();
            }
            return model;
        }

        public void DeleteModel(int id)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM LLMModels WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public List<LlmModel> GetAllModels()
        {
            var models = new List<LlmModel>();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT m.Id, m.Name, m.ModelId, m.PlatformId, p.Name FROM LLMModels m
                JOIN LLMPlatforms p ON p.Id = m.PlatformId ORDER BY p.Id, m.Id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                models.Add(new LlmModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ModelId = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    PlatformId = reader.GetInt32(3),
                    PlatformName = reader.GetString(4),
                });
            return models;
        }
    }

    public class LlmPlatform
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public List<LlmModel> Models { get; set; } = new();
    }

    public class LlmModel
    {
        public int Id { get; set; }
        public int PlatformId { get; set; }
        public string Name { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string PlatformName { get; set; } = "";
    }
}
