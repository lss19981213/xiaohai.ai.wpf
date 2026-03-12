namespace XIAOHAI.AI;

public class Settings
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "glm-4.7-flash";
    public string VisionModel { get; set; } = "llava";
    public string? BaiduApiKey { get; set; }
    public string? BaiduUserId { get; set; }
    public string? OpenClawUrl { get; set; } = "http://127.0.0.1:18789";

    // Gitee 更新配置
    public string GiteeOwner { get; set; } = "lss97_admin";  // 替换为你的 Gitee 用户名
    public string GiteeRepo { get; set; } = "xiaohai.ai.wpf";      // 仓库名称
    public string AppVersion { get; set; } = "1.0.0";          // 当前版本号
    public bool AutoCheckUpdate { get; set; } = true;          // 是否自动检查更新
}