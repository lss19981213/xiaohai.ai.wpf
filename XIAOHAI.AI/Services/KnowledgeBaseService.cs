using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XIAOHAI.AI;

namespace XIAOHAI.AI.Services;

public class KnowledgeBaseService : IDisposable
{
    private const string CompressionStateFileName = "compression_state.json";
    private string _cachedContent = null;
    private DateTime _cacheTime = DateTime.MinValue;

    private readonly VectorDatabaseService _vectorDb;
    private OllamaService _ollama;
    private string _embeddingModel = "nomic-embed-text";
    private bool _disposed = false;

    public KnowledgeBaseService(OllamaService ollama = null)
    {
        _vectorDb = new VectorDatabaseService();
        _ollama = ollama ?? new OllamaService();
    }

    public void SetEmbeddingModel(string model)
    {
        _embeddingModel = model;
    }

    public void SetOllamaService(OllamaService ollama)
    {
        if (ollama != null)
            _ollama = ollama;
    }

    public async Task<string> LoadContentAsync()
    {
        try
        {
            var lastWriteTime = DateTime.Now;
            var compressionWriteTime = File.Exists(CompressionStateFileName)
                ? File.GetLastWriteTime(CompressionStateFileName)
                : DateTime.MinValue;

            if (_cachedContent != null && lastWriteTime <= _cacheTime && compressionWriteTime <= _cacheTime)
            {
                return _cachedContent;
            }

            var entries = await _vectorDb.GetAllEntriesAsync();

            if (entries == null || entries.Count == 0)
            {
                _cachedContent = "";
                _cacheTime = lastWriteTime;
                return "";
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 知识库内容 ===");
            foreach (var entry in entries)
            {
                var compressionNote = entry.CompressionLevel switch
                {
                    1 => " [轻度压缩]",
                    2 => " [中度压缩]",
                    3 => " [高度压缩]",
                    _ => ""
                };

                sb.AppendLine($"[{entry.Title}]({entry.Type}){compressionNote}: {entry.Content}");
                sb.AppendLine("---");
            }

            _cachedContent = sb.ToString();
            _cacheTime = lastWriteTime > compressionWriteTime ? lastWriteTime : compressionWriteTime;
            return _cachedContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载知识库失败：{ex.Message}");
            return "";
        }
    }

    public async Task<string> SearchRelevantContentAsync(string query, int topK = 5, double threshold = 0.3)
    {
        try
        {
            var queryVector = await _ollama.GetEmbeddingForLongTextAsync(query, _embeddingModel);
            if (queryVector == null || queryVector.Length == 0)
            {
                Console.WriteLine("[知识库] 无法生成查询向量，回退到全量加载");
                return await LoadContentAsync();
            }

            var results = await _vectorDb.SearchSimilarAsync(queryVector, topK, threshold);

            if (results == null || results.Count == 0)
            {
                Console.WriteLine("[知识库] 未找到相关内容，回退到全量加载");
                return await LoadContentAsync();
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 知识库相关内容 ===");
            foreach (var result in results)
            {
                sb.AppendLine($"[{result.Title}] (相关度：{result.SimilarityPercent}): {result.Content}");
                sb.AppendLine("---");
            }

            Console.WriteLine($"[知识库] 语义检索找到 {results.Count} 条相关内容");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[知识库] 语义检索失败：{ex.Message}，回退到全量加载");
            return await LoadContentAsync();
        }
    }

    public async Task SyncToVectorDbAsync(IProgress<string> progress = null)
    {
        try
        {
            Console.WriteLine($"[知识库] 开始同步到向量数据库");
            
            var pingOk = await _ollama.PingAsync();
            if (!pingOk)
            {
                progress?.Report("无法连接 Ollama 服务，请确保 Ollama 已启动");
                Console.WriteLine("[知识库] 无法连接 Ollama 服务");
                return;
            }

            var entries = await _vectorDb.GetAllEntriesAsync();

            Console.WriteLine($"[知识库] 从数据库读取条目数：{entries?.Count ?? 0}");

            if (entries == null || entries.Count == 0)
            {
                progress?.Report("知识库为空");
                return;
            }

            var existingIds = await _vectorDb.GetAllEntryIdsAsync();
            var toDelete = existingIds.Except(entries.Select(e => e.Id)).ToList();
            Console.WriteLine($"[知识库] 需要删除的向量：{toDelete.Count}");

            foreach (var id in toDelete)
            {
                await _vectorDb.DeleteVectorAsync(id);
            }

            int processed = 0;
            int failed = 0;
            int total = entries.Count;

            foreach (var entry in entries)
            {
                try
                {
                    Console.WriteLine($"[知识库] 处理条目 {entry.Id}: {entry.Title}");
                    var textForEmbedding = $"标题：{entry.Title}\n内容：{entry.Content}";
                    var vector = await _ollama.GetEmbeddingForLongTextAsync(textForEmbedding, _embeddingModel);

                    if (vector == null || vector.Length == 0)
                    {
                        Console.WriteLine($"[知识库] 条目 {entry.Id} 向量生成失败，请确保已安装 embedding 模型：ollama pull {_embeddingModel}");
                        failed++;
                        continue;
                    }

                    Console.WriteLine($"[知识库] 条目 {entry.Id} 向量生成成功，维度：{vector.Length}");
                    var stored = await _vectorDb.StoreVectorAsync(entry.Id, entry.Title, entry.Content, vector);
                    
                    if (stored)
                    {
                        processed++;
                        Console.WriteLine($"[知识库] 条目 {entry.Id} 存储成功");
                    }
                    else
                    {
                        failed++;
                        Console.WriteLine($"[知识库] 条目 {entry.Id} 存储失败");
                    }
                    
                    progress?.Report($"同步进度：{processed}/{total}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[知识库] 同步条目 {entry.Id} 失败：{ex}");
                    failed++;
                }
            }

            var finalCount = await _vectorDb.GetVectorCountAsync();
            Console.WriteLine($"[知识库] 同步完成，数据库中向量总数：{finalCount}");
            
            var resultMsg = failed > 0 
                ? $"同步完成：{processed} 成功，{failed} 失败 (请确保已安装 {_embeddingModel})" 
                : $"同步完成，共处理 {processed} 条记录";
            progress?.Report(resultMsg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[知识库] 同步异常：{ex}");
            progress?.Report($"同步失败：{ex.Message}");
        }
    }

    public async Task AddEntryToVectorDbAsync(KnowledgeEntryData entry)
    {
        try
        {
            var textForEmbedding = $"标题：{entry.Title}\n内容：{entry.Content}";
            var vector = await _ollama.GetEmbeddingForLongTextAsync(textForEmbedding, _embeddingModel);

            if (vector != null && vector.Length > 0)
            {
                await _vectorDb.StoreVectorAsync(entry.Id, entry.Title, entry.Content, vector);
                Console.WriteLine($"[知识库] 已添加向量：{entry.Title}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[知识库] 添加向量失败：{ex.Message}");
        }
    }

    public async Task RemoveEntryFromVectorDbAsync(int entryId)
    {
        try
        {
            await _vectorDb.DeleteVectorAsync(entryId);
            Console.WriteLine($"[知识库] 已删除向量：{entryId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[知识库] 删除向量失败：{ex.Message}");
        }
    }

    public async Task<int> GetVectorCountAsync()
    {
        return await _vectorDb.GetVectorCountAsync();
    }

    public void ClearCache()
    {
        _cachedContent = null;
        _cacheTime = DateTime.MinValue;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _vectorDb?.Dispose();
            _disposed = true;
        }
    }
}
