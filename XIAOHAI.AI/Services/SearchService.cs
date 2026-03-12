using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace XIAOHAI.AI.Services;

public class SearchService
{
    private readonly string _baiduApiKey;
    private readonly string _baiduUserId;

    public SearchService(string baiduApiKey = "", string baiduUserId = "")
    {
        _baiduApiKey = baiduApiKey;
        _baiduUserId = baiduUserId;
    }

    public async Task<string> SearchWebAsync(string query)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var searchResult = await GetBaiduQianfanSearchResult(query);

            if (searchResult == "mock")
            {
                await Task.Delay(1000);
                return $@"搜索结果：问题：{query}
                          相关搜索结果：
                          1. 根据网络信息，关于""{query}""的相关内容...
                          2. 最新信息显示...
                          3. 相关资料表明...
                        （注意：这是模拟的搜索结果，实际应用中需要集成真实的搜索引擎API）";
            }
            return searchResult;
        }
        catch (Exception ex)
        {
            return $"网络搜索失败: {ex.Message}";
        }
    }

    private async Task<string> GetBaiduQianfanSearchResult(string query)
    {
        try
        {
            string apiKey = _baiduApiKey;
            string userId = _baiduUserId;

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_API_KEY_HERE")
            {
                return "网络搜索功能尚未配置：请在settings.json中设置百度千帆API密钥";
            }

            if (string.IsNullOrEmpty(userId) || userId == "YOUR_USER_ID_HERE")
            {
                return "网络搜索功能尚未配置：请在settings.json中设置百度千帆用户ID";
            }

            using var client = new HttpClient();

            var apiUrl = "https://qianfan.baidubce.com/v2/ai_search/web_summary";
            var requestId = Guid.NewGuid().ToString();

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-Appbuilder-Authorization", $"Bearer {apiKey}");
            client.DefaultRequestHeaders.Add("X-Appbuilder-Request-Id", requestId);
            client.DefaultRequestHeaders.Add("X-Appbuilder-User-Id", userId);
            client.Timeout = TimeSpan.FromSeconds(60);

            var requestBody = new
            {
                messages = new[] {
                    new {
                        role = "user",
                        content = query
                    }
                },
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var responseDoc = JsonDocument.Parse(responseJson);

                if (responseDoc.RootElement.TryGetProperty("result", out var resultElement))
                {
                    if (resultElement.TryGetProperty("summary", out var summaryElement))
                    {
                        var summary = summaryElement.GetString();
                        if (!string.IsNullOrEmpty(summary))
                        {
                            return $@"网络搜索结果：{summary}";
                        }
                    }

                    if (resultElement.TryGetProperty("web_search_results", out var webResultsElement))
                    {
                        var webResults = webResultsElement.GetString();
                        if (!string.IsNullOrEmpty(webResults))
                        {
                            return $@"网络搜索结果：{webResults}";
                        }
                    }
                    return $@"网络搜索结果：{resultElement.ToString()}";
                }
                else
                {
                    return $@"网络搜索结果：{responseJson}";
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return $"API请求失败: {response.StatusCode} ({(int)response.StatusCode}) - {errorContent}";
            }
        }
        catch (HttpRequestException httpEx)
        {
            return $"网络请求失败: {httpEx.Message}";
        }
        catch (TaskCanceledException tcEx)
        {
            return $"请求超时: {tcEx.Message}";
        }
        catch (Exception ex)
        {
            return $"搜索API调用失败: {ex.Message}";
        }
    }
}
