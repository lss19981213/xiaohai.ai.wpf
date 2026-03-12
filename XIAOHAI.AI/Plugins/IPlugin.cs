using System.Threading.Tasks;

namespace XIAOHAI.AI.Plugins;

/// <summary>
/// 插件接口 - 所有插件必须实现此接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// 插件作者
    /// </summary>
    string Author { get; }
    
    /// <summary>
    /// 插件图标（Emoji）
    /// </summary>
    string Icon { get; }
    
    /// <summary>
    /// 初始化插件
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// 获取插件的设置界面（可选）
    /// </summary>
    System.Windows.Controls.UserControl? GetSettingsView();
    
    /// <summary>
    /// 处理用户消息（可选）
    /// </summary>
    Task<PluginResponse?> ProcessMessageAsync(PluginContext context);
    
    /// <summary>
    /// 插件是否启用
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// 启用/禁用插件
    /// </summary>
    void SetEnabled(bool enabled);
    
    /// <summary>
    /// 卸载插件
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// 插件上下文
/// </summary>
public class PluginContext
{
    /// <summary>
    /// 用户消息
    /// </summary>
    public string Message { get; set; } = "";
    
    /// <summary>
    /// 对话历史
    /// </summary>
    public List<ChatMessage> History { get; set; } = new();
    
    /// <summary>
    /// 当前模型
    /// </summary>
    public string CurrentModel { get; set; } = "";
    
    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object> ExtraData { get; set; } = new();
}

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>
/// 插件响应
/// </summary>
public class PluginResponse
{
    /// <summary>
    /// 是否拦截消息（不再发送给 AI）
    /// </summary>
    public bool Intercept { get; set; }
    
    /// <summary>
    /// 响应内容
    /// </summary>
    public string Content { get; set; } = "";
    
    /// <summary>
    /// 附加操作（如打开窗口等）
    /// </summary>
    public string? Action { get; set; }
    
    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object>? ExtraData { get; set; }
}
