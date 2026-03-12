using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace XIAOHAI.AI.Services;

public class VectorDatabaseService : IDisposable
{
    private const string DbFileName = "knowledge_vectors.db";
    private const int VectorDimension = 768;
    private readonly string _dbPath;
    private SqliteConnection _connection;
    private bool _disposed = false;

    public VectorDatabaseService()
    {
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
        InitializeDatabase().GetAwaiter().GetResult();
    }

    private async Task InitializeDatabase()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        await _connection.OpenAsync();

        var createTableCmd = _connection.CreateCommand();
        createTableCmd.CommandText = @"
            -- 知识条目表
            CREATE TABLE IF NOT EXISTS knowledge_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                type TEXT NOT NULL,
                is_locked INTEGER DEFAULT 0,
                original_content TEXT,
                compression_level INTEGER DEFAULT 0,
                created_time TEXT NOT NULL,
                updated_time TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_knowledge_title ON knowledge_entries(title);
            
            -- 知识向量表
            CREATE TABLE IF NOT EXISTS knowledge_vectors (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                entry_id INTEGER NOT NULL UNIQUE,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                vector BLOB NOT NULL,
                created_time TEXT NOT NULL,
                updated_time TEXT NOT NULL,
                FOREIGN KEY (entry_id) REFERENCES knowledge_entries(id)
            );
            CREATE INDEX IF NOT EXISTS idx_entry_id ON knowledge_vectors(entry_id);
            CREATE INDEX IF NOT EXISTS idx_content_hash ON knowledge_vectors(content_hash);
        ";
        await createTableCmd.ExecuteNonQueryAsync();
    }

    #region 向量数据库操作方法
    public async Task<bool> StoreVectorAsync(int entryId, string title, string content, float[] vector)
    {
        if (vector == null || vector.Length == 0)
        {
            Console.WriteLine($"[VectorDB] 向量为空，跳过存储 entryId={entryId}");
            return false;
        }

        var contentHash = ComputeHash(content);
        var now = DateTime.UtcNow.ToString("o");
        var vectorBytes = VectorToBytes(vector);

        bool exists = false;
        bool sameHash = false;

        {
            var checkCmd = _connection.CreateCommand();
            checkCmd.CommandText = "SELECT id, content_hash FROM knowledge_vectors WHERE entry_id = @entryId";
            checkCmd.Parameters.AddWithValue("@entryId", entryId);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                exists = true;
                var existingHash = reader.GetString(1);
                sameHash = (existingHash == contentHash);
            }
        }

        if (exists)
        {
            if (sameHash)
            {
                Console.WriteLine($"[VectorDB] 内容未变化，跳过更新 entryId={entryId}");
                return true;
            }

            var updateCmd = _connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE knowledge_vectors 
                SET title = @title, content = @content, content_hash = @contentHash, 
                    vector = @vector, updated_time = @updatedTime
                WHERE entry_id = @entryId";
            updateCmd.Parameters.AddWithValue("@entryId", entryId);
            updateCmd.Parameters.AddWithValue("@title", title);
            updateCmd.Parameters.AddWithValue("@content", content);
            updateCmd.Parameters.AddWithValue("@contentHash", contentHash);
            updateCmd.Parameters.AddWithValue("@vector", vectorBytes);
            updateCmd.Parameters.AddWithValue("@updatedTime", now);
            var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"[VectorDB] 更新记录 entryId={entryId}, rowsAffected={rowsAffected}");
            return rowsAffected > 0;
        }

        var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO knowledge_vectors (entry_id, title, content, content_hash, vector, created_time, updated_time)
            VALUES (@entryId, @title, @content, @contentHash, @vector, @createdTime, @updatedTime)";
        insertCmd.Parameters.AddWithValue("@entryId", entryId);
        insertCmd.Parameters.AddWithValue("@title", title);
        insertCmd.Parameters.AddWithValue("@content", content);
        insertCmd.Parameters.AddWithValue("@contentHash", contentHash);
        insertCmd.Parameters.AddWithValue("@vector", vectorBytes);
        insertCmd.Parameters.AddWithValue("@createdTime", now);
        insertCmd.Parameters.AddWithValue("@updatedTime", now);
        var insertedRows = await insertCmd.ExecuteNonQueryAsync();
        Console.WriteLine($"[VectorDB] 插入记录 entryId={entryId}, title={title}, rowsAffected={insertedRows}");
        return insertedRows > 0;
    }

    public async Task<List<VectorSearchResult>> SearchSimilarAsync(float[] queryVector, int topK = 5, double threshold = 0.3)
    {
        var results = new List<VectorSearchResult>();

        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT entry_id, title, content, vector FROM knowledge_vectors";

        using var reader = await cmd.ExecuteReaderAsync();
        var candidates = new List<(int EntryId, string Title, string Content, float[] Vector)>();

        while (await reader.ReadAsync())
        {
            var entryId = reader.GetInt32(0);
            var title = reader.GetString(1);
            var content = reader.GetString(2);
            var vectorBytes = GetBytesFromReader(reader, 3);
            var vector = BytesToVector(vectorBytes);

            if (vector != null && vector.Length == queryVector.Length)
            {
                candidates.Add((entryId, title, content, vector));
            }
        }

        var scoredResults = candidates
            .Select(c => new VectorSearchResult
            {
                EntryId = c.EntryId,
                Title = c.Title,
                Content = c.Content,
                Similarity = CosineSimilarity(queryVector, c.Vector)
            })
            .Where(r => r.Similarity >= threshold)
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();

        return scoredResults;
    }

    public async Task DeleteVectorAsync(int entryId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM knowledge_vectors WHERE entry_id = @entryId";
        cmd.Parameters.AddWithValue("@entryId", entryId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<int>> GetAllEntryIdsAsync()
    {
        var ids = new List<int>();
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT entry_id FROM knowledge_vectors";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }

    public async Task<int> GetVectorCountAsync()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM knowledge_vectors";
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task ClearAllAsync()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM knowledge_vectors";
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region 知识条目数据库操作方法
    public async Task<int> CreateEntryAsync(string title, string content, string type, 
        string? originalContent = null, int compressionLevel = 0, bool isLocked = false)
    {
        var now = DateTime.UtcNow.ToString("o");
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO knowledge_entries (title, content, type, original_content, compression_level, is_locked, created_time, updated_time)
            VALUES (@title, @content, @type, @originalContent, @compressionLevel, @isLocked, @createdTime, @updatedTime);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@originalContent", (object?)originalContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@compressionLevel", compressionLevel);
        cmd.Parameters.AddWithValue("@isLocked", isLocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@createdTime", now);
        cmd.Parameters.AddWithValue("@updatedTime", now);
        
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : -1;
    }

    public async Task<bool> UpdateEntryAsync(int id, string title, string content, 
        string? originalContent = null, int compressionLevel = 0, bool isLocked = false)
    {
        var now = DateTime.UtcNow.ToString("o");
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE knowledge_entries 
            SET title = @title, content = @content, original_content = @originalContent, 
                compression_level = @compressionLevel, is_locked = @isLocked, updated_time = @updatedTime
            WHERE id = @id
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@originalContent", (object?)originalContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@compressionLevel", compressionLevel);
        cmd.Parameters.AddWithValue("@isLocked", isLocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@updatedTime", now);
        
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteEntryAsync(int id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM knowledge_entries WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<List<KnowledgeEntryData>> GetAllEntriesAsync()
    {
        var entries = new List<KnowledgeEntryData>();
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, content, type, is_locked, original_content, compression_level, created_time FROM knowledge_entries ORDER BY id DESC";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new KnowledgeEntryData
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                Type = reader.GetString(3),
                IsLocked = reader.GetInt32(4) == 1,
                OriginalContent = reader.IsDBNull(5) ? null : reader.GetString(5),
                CompressionLevel = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CreatedTime = reader.GetDateTime(7)
            });
        }
        
        return entries;
    }

    public async Task<KnowledgeEntryData?> GetEntryByIdAsync(int id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, content, type, is_locked, original_content, compression_level, created_time FROM knowledge_entries WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new KnowledgeEntryData
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                Type = reader.GetString(3),
                IsLocked = reader.GetInt32(4) == 1,
                OriginalContent = reader.IsDBNull(5) ? null : reader.GetString(5),
                CompressionLevel = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CreatedTime = reader.GetDateTime(7)
            };
        }
        
        return null;
    }

    public async Task<int> GetEntryCountAsync()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM knowledge_entries";
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }
    #endregion

    #region 私有辅助方法
    private static float CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length)
            return 0f;

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < v1.Length; i++)
        {
            dotProduct += v1[i] * v2[i];
            magnitude1 += v1[i] * v1[i];
            magnitude2 += v2[i] * v2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0f;

        return (float)(dotProduct / (magnitude1 * magnitude2));
    }

    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToVector(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return Array.Empty<float>();

        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private static byte[] GetBytesFromReader(SqliteDataReader reader, int ordinal)
    {
        using var stream = reader.GetStream(ordinal);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static string ComputeHash(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(bytes);
    }
    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}

public class VectorSearchResult
{
    public int EntryId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public float Similarity { get; set; }

    public string SimilarityPercent => $"{(Similarity * 100):F1}%";
}

// 知识条目数据模型
public class KnowledgeEntryData
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsLocked { get; set; }
    public string? OriginalContent { get; set; }
    public int CompressionLevel { get; set; }
    public DateTime CreatedTime { get; set; }
}