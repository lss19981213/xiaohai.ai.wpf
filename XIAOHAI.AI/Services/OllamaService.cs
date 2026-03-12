using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOHAI.AI.Services;

public class OllamaService
{
    private HttpClient _http;
    private string _baseUrl = "http://localhost:11434";

    public OllamaService() : this("http://localhost:11434") { }

    public OllamaService(string baseUrl)
    {
        _baseUrl = baseUrl;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(10) };
    }

    public void UpdateBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(10) };
    }

    public string BaseUrl => _baseUrl;

    public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync("/api/tags", ct);
        res.EnsureSuccessStatusCode();
        var data = await res.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: ct);
        var list = new List<string>();
        foreach (var m in data?.Models ?? [])
            list.Add(m.Name);
        return list;
    }

    public async Task<List<string>> ListModelsAsync(string baseUrl, CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var res = await client.GetAsync("/api/tags", ct);
            res.EnsureSuccessStatusCode();
            var data = await res.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: ct);
            var list = new List<string>();
            foreach (var m in data?.Models ?? [])
                list.Add(m.Name);
            return list;
        }
        catch
        {
            return new List<string>();
        }
    }

    public record StreamChunk(string? ThinkingDelta, string? ContentDelta);

    public async Task ChatStreamAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        IProgress<StreamChunk> onChunk,
        CancellationToken ct = default)
    {
        var req = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            stream = true
        };
        using var content = new StringContent(
            JsonSerializer.Serialize(req),
            Encoding.UTF8,
            "application/json");
        using var resp = await _http.PostAsync("/api/chat", content, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var buffer = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var json = JsonSerializer.Deserialize<OllamaChatChunk>(line);
                var msg = json?.Message;
                if (msg == null) continue;
                var thinking = msg.Thinking;
                var text = msg.Content;
                if (!string.IsNullOrEmpty(thinking) || !string.IsNullOrEmpty(text))
                    onChunk?.Report(new StreamChunk(thinking, text));
            }
            catch { /* skip invalid lines */ }
        }
    }

    public async Task<string> ChatWithImageAsync(
        string model,
        string prompt,
        string base64Image,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Ollama] ChatWithImageAsync - 模型: {model}");
        Console.WriteLine($"[Ollama] ChatWithImageAsync - 图片Base64长度: {base64Image.Length}");
        Console.WriteLine($"[Ollama] ChatWithImageAsync - Prompt长度: {prompt.Length}");

        var req = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt,
                    images = new[] { base64Image }
                }
            },
            stream = false
        };

        var jsonReq = JsonSerializer.Serialize(req);
        Console.WriteLine($"[Ollama] 请求JSON长度: {jsonReq.Length}");
        Console.WriteLine($"[Ollama] 请求包含images字段: {jsonReq.Contains("\"images\"")}");

        using var content = new StringContent(
            jsonReq,
            Encoding.UTF8,
            "application/json");
        using var resp = await _http.PostAsync("/api/chat", content, ct);

        Console.WriteLine($"[Ollama] 响应状态码: {resp.StatusCode}");

        resp.EnsureSuccessStatusCode();

        var responseText = await resp.Content.ReadAsStringAsync(ct);
        Console.WriteLine($"[Ollama] 响应长度: {responseText.Length}");

        var json = JsonSerializer.Deserialize<OllamaChatResponse>(responseText);
        var result = json?.Message?.Content ?? "";

        Console.WriteLine($"[Ollama] 结果长度: {result.Length}");
        Console.WriteLine($"[Ollama] 结果预览: {(result.Length > 200 ? result.Substring(0, 200) + "..." : result)}");

        return result;
    }

    public async Task<string> ChatAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var req = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            stream = false
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(req),
            Encoding.UTF8,
            "application/json");
        using var resp = await _http.PostAsync("/api/chat", content, ct);
        resp.EnsureSuccessStatusCode();

        var responseText = await resp.Content.ReadAsStringAsync(ct);
        var json = JsonSerializer.Deserialize<OllamaChatResponse>(responseText);
        return json?.Message?.Content ?? "";
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            var res = await _http.GetAsync("/api/tags");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private const int MaxEmbeddingTextLength = 8000;

    public async Task<float[]> GetEmbeddingAsync(string text, string model = "nomic-embed-text", CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[Ollama] Embedding 文本为空");
                return Array.Empty<float>();
            }

            if (text.Length > MaxEmbeddingTextLength)
            {
                Console.WriteLine($"[Ollama] 文本过长 ({text.Length} 字符)，截断到 {MaxEmbeddingTextLength} 字符");
                text = text.Substring(0, MaxEmbeddingTextLength);
            }

            var req = new { model, prompt = text };
            using var content = new StringContent(
                JsonSerializer.Serialize(req),
                Encoding.UTF8,
                "application/json");
            using var resp = await _http.PostAsync("/api/embeddings", content, ct);
            
            if (!resp.IsSuccessStatusCode)
            {
                var errorContent = await resp.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[Ollama] Embedding API 返回错误 ({resp.StatusCode}): {errorContent}");
                return Array.Empty<float>();
            }

            var responseText = await resp.Content.ReadAsStringAsync(ct);
            var json = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseText);
            
            if (json?.Embedding == null || json.Embedding.Length == 0)
            {
                Console.WriteLine($"[Ollama] Embedding 返回空向量，请确保模型 '{model}' 已安装: ollama pull {model}");
                return Array.Empty<float>();
            }

            Console.WriteLine($"[Ollama] Embedding 生成成功，向量维度: {json.Embedding.Length}");
            return json.Embedding;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[Ollama] Embedding 网络请求失败: {ex.Message}");
            return Array.Empty<float>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ollama] Embedding 生成异常: {ex}");
            return Array.Empty<float>();
        }
    }

    public async Task<float[]> GetEmbeddingForLongTextAsync(string text, string model = "nomic-embed-text", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        if (text.Length <= MaxEmbeddingTextLength)
            return await GetEmbeddingAsync(text, model, ct);

        var chunks = SplitTextIntoChunks(text, MaxEmbeddingTextLength);
        Console.WriteLine($"[Ollama] 长文本分块处理，共 {chunks.Count} 块");

        var vectors = new List<float[]>();
        foreach (var chunk in chunks)
        {
            var vector = await GetEmbeddingAsync(chunk, model, ct);
            if (vector != null && vector.Length > 0)
                vectors.Add(vector);
        }

        if (vectors.Count == 0)
            return Array.Empty<float>();

        return AverageVectors(vectors);
    }

    private static List<string> SplitTextIntoChunks(string text, int maxChunkSize)
    {
        var chunks = new List<string>();
        var sentences = text.Split(new[] { '。', '！', '？', '\n', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        
        var currentChunk = new StringBuilder();
        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length + 1 > maxChunkSize)
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }
            }
            currentChunk.Append(sentence).Append('。');
        }
        
        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString());

        return chunks;
    }

    private static float[] AverageVectors(List<float[]> vectors)
    {
        if (vectors.Count == 0)
            return Array.Empty<float>();

        var dimension = vectors[0].Length;
        var result = new float[dimension];

        foreach (var vector in vectors)
        {
            for (int i = 0; i < dimension && i < vector.Length; i++)
                result[i] += vector[i];
        }

        for (int i = 0; i < dimension; i++)
            result[i] /= vectors.Count;

        return result;
    }

    public async Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts, string model = "nomic-embed-text", CancellationToken ct = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await GetEmbeddingAsync(text, model, ct);
            results.Add(embedding);
        }
        return results;
    }

    public record ChatMessage(string Role, string Content);

    private class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = [];
    }

    private class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    private class OllamaChatChunk
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
