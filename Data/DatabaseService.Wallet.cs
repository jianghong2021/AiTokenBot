using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using AiTokenBot.Views;

namespace AiTokenBot.Data
{
    public partial class DatabaseService
    {
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
    }
}
