using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace XIAOHAI.AI.Services;

/// <summary>
/// 软件更新服务 - 检查 Gitee 上的最新版本
/// </summary>
public class UpdateService
{
    private readonly string _giteeApiUrl;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _currentVersion;

    public UpdateService(string owner, string repo, string currentVersion)
    {
        _owner = owner;
        _repo = repo;
        _currentVersion = currentVersion;
        _giteeApiUrl = $"https://gitee.com/api/v5/repos/{owner}/{repo}/releases/latest";
    }

    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "XIAOHAI.AI-Updater");

            var response = await httpClient.GetAsync(_giteeApiUrl);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Error = "无法连接到 Gitee 服务器"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            var releaseInfo = JsonSerializer.Deserialize<GiteeReleaseInfo>(json);

            if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.tag_name))
            {
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Error = "未获取到版本信息"
                };
            }

            // 解析版本号
            var latestVersion = releaseInfo.tag_name.TrimStart('v', 'V');
            var isUpdateAvailable = IsNewerVersion(latestVersion, _currentVersion);

            return new UpdateCheckResult
            {
                HasUpdate = isUpdateAvailable,
                CurrentVersion = _currentVersion,
                LatestVersion = latestVersion,
                ReleaseNotes = releaseInfo.body ?? "",
                DownloadUrl = (releaseInfo.assets != null && releaseInfo.assets.Length > 0) ? releaseInfo.assets[0].browser_download_url : releaseInfo.HtmlUrl,
                ReleaseDate = releaseInfo.PublishedAt,
                Error = null
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                HasUpdate = false,
                Error = $"检查更新失败：{ex.Message}"
            };
        }
    }

    /// <summary>
    /// 下载更新文件
    /// </summary>
    public async Task<DownloadProgress> DownloadUpdateAsync(string downloadUrl, string savePath, IProgress<int> progress = null)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "XIAOHAI.AI-Updater");

            using var response = await httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(savePath);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    var percent = (int)((downloadedBytes * 100) / totalBytes);
                    progress.Report(percent);
                }
            }

            return new DownloadProgress
            {
                Success = true,
                DownloadedPath = savePath,
                TotalBytes = totalBytes,
                Message = "下载完成"
            };
        }
        catch (Exception ex)
        {
            return new DownloadProgress
            {
                Success = false,
                Message = $"下载失败：{ex.Message}"
            };
        }
    }

    /// <summary>
    /// 比较版本号
    /// </summary>
    private bool IsNewerVersion(string newVersion, string currentVersion)
    {
        try
        {
            var newVer = new Version(newVersion);
            var currentVer = new Version(currentVersion);
            return newVer > currentVer;
        }
        catch
        {
            // 如果版本号格式不正确，使用字符串比较
            return string.Compare(newVersion, currentVersion, StringComparison.Ordinal) > 0;
        }
    }
}

/// <summary>
/// 更新检查结果
/// </summary>
public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string? DownloadUrl { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 下载进度
/// </summary>
public class DownloadProgress
{
    public bool Success { get; set; }
    public string DownloadedPath { get; set; } = "";
    public long TotalBytes { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Gitee Release 信息
/// </summary>
public class GiteeReleaseInfo
{
    public string tag_name { get; set; } = "";
    public string Name { get; set; } = "";
    public string body { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public GiteeAsset[]? assets { get; set; }
}

/// <summary>
/// Gitee 资源文件
/// </summary>
public class GiteeAsset
{
    public string Name { get; set; } = "";
    public string browser_download_url { get; set; } = "";
    public long Size { get; set; }
}
