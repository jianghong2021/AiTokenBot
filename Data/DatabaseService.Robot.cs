using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using AiTokenBot.Views;

namespace AiTokenBot.Data
{
    public partial class DatabaseService
    {
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
    }
}
