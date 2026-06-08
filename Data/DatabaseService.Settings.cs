using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace AiTokenBot.Data
{
    public partial class DatabaseService
    {
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
            {
                SeedSettings(conn);
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
    }
}
