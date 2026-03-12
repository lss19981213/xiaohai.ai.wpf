using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XIAOHAI.AI.Services;

public class ChatHistoryService
{
    private readonly string _historyFilePath;
    private readonly int _maxMessages;

    public ChatHistoryService(string fileName, int maxMessages = 100)
    {
        _historyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        _maxMessages = maxMessages;
    }

    public List<ChatMessageRecord> LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
                return new List<ChatMessageRecord>();

            var json = File.ReadAllText(_historyFilePath);
            var history = JsonSerializer.Deserialize<List<ChatMessageRecord>>(json);
            return history ?? new List<ChatMessageRecord>();
        }
        catch
        {
            return new List<ChatMessageRecord>();
        }
    }

    public void SaveHistory(List<ChatMessageRecord> messages)
    {
        try
        {
            var toSave = messages.Count > _maxMessages
                ? messages.TakeLast(_maxMessages).ToList()
                : messages;

            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存聊天历史失败: {ex.Message}");
        }
    }

    public void ClearHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                File.Delete(_historyFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清除聊天历史失败: {ex.Message}");
        }
    }
}

public class ChatMessageRecord
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
