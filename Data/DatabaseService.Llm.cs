using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace AiTokenBot.Data
{
    public partial class DatabaseService
    {
        // ===== LLM =====

        public void SeedLlmIfEmpty()
        {
            using var conn = OpenConnection();
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM LLMPlatforms";
            var count = (long)check.ExecuteScalar()!;
            if (count > 0) return;

            using var tx = conn.BeginTransaction();
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO LLMPlatforms (Name, BaseUrl, ApiKey) VALUES ('DeepSeek', 'https://api.deepseek.com', ''); SELECT last_insert_rowid();";
            var platformId = (long)ins.ExecuteScalar()!;

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
