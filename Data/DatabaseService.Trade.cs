using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using AiTokenBot.Views;

namespace AiTokenBot.Data
{
    public partial class DatabaseService
    {
        // ===== Trade Records =====

        public List<TradeRecord> LoadTradeRecords()
        {
            var records = new List<TradeRecord>();
            using var conn = OpenConnection();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM TradeRecords ORDER BY Id DESC";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                records.Add(ReadTrade(reader));

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
    }
}
