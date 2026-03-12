using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace XIAOHAI.AI.Plugins;

/// <summary>
/// 插件加载器 - 负责加载和管理插件
/// </summary>
public class PluginLoader
{
    private readonly string _pluginsDirectory;
    private readonly List<IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, Assembly> _pluginAssemblies = new();

    public PluginLoader()
    {
        _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
    }

    /// <summary>
    /// 获取所有已加载的插件
    /// </summary>
    public IReadOnlyList<IPlugin> GetPlugins() => _loadedPlugins.AsReadOnly();

    /// <summary>
    /// 获取启用的插件
    /// </summary>
    public IReadOnlyList<IPlugin> GetEnabledPlugins() 
        => _loadedPlugins.Where(p => p.IsEnabled).ToList().AsReadOnly();

    /// <summary>
    /// 加载所有插件
    /// </summary>
    public async Task LoadAllPluginsAsync()
    {
        Console.WriteLine($"[PluginLoader] 开始加载插件，目录：{_pluginsDirectory}");
        
        var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);
        Console.WriteLine($"[PluginLoader] 找到 {pluginFiles.Length} 个 DLL 文件");

        foreach (var file in pluginFiles)
        {
            try
            {
                await LoadPluginAsync(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginLoader] 加载插件失败 {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"[PluginLoader] 加载完成，共 {_loadedPlugins.Count} 个插件");
    }

    /// <summary>
    /// 加载单个插件
    /// </summary>
    private async Task LoadPluginAsync(string pluginPath)
    {
        try
        {
            Console.WriteLine($"[PluginLoader] 尝试加载：{pluginPath}");
            
            var assembly = Assembly.LoadFrom(pluginPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var type in pluginTypes)
            {
                Console.WriteLine($"[PluginLoader] 发现插件类型：{type.FullName}");
                
                if (Activator.CreateInstance(type) is IPlugin plugin)
                {
                    await plugin.InitializeAsync();
                    _loadedPlugins.Add(plugin);
                    _pluginAssemblies[plugin.Name] = assembly;
                    
                    Console.WriteLine($"[PluginLoader] 插件加载成功：{plugin.Name} v{plugin.Version}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PluginLoader] 加载插件异常：{ex}");
            throw;
        }
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    public async Task UnloadPluginAsync(string pluginName)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.Name == pluginName);
        if (plugin != null)
        {
            await plugin.ShutdownAsync();
            _loadedPlugins.Remove(plugin);
            Console.WriteLine($"[PluginLoader] 插件已卸载：{pluginName}");
        }
    }

    /// <summary>
    /// 卸载所有插件
    /// </summary>
    public async Task UnloadAllAsync()
    {
        foreach (var plugin in _loadedPlugins.ToList())
        {
            await plugin.ShutdownAsync();
        }
        _loadedPlugins.Clear();
        _pluginAssemblies.Clear();
    }

    /// <summary>
    /// 获取插件状态
    /// </summary>
    public PluginStatus GetPluginStatus(string pluginName)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.Name == pluginName);
        if (plugin == null)
            return new PluginStatus { Name = pluginName, IsLoaded = false };

        return new PluginStatus
        {
            Name = plugin.Name,
            Description = plugin.Description,
            Version = plugin.Version,
            Author = plugin.Author,
            Icon = plugin.Icon,
            IsLoaded = true,
            IsEnabled = plugin.IsEnabled
        };
    }

    /// <summary>
    /// 启用/禁用插件
    /// </summary>
    public void SetPluginEnabled(string pluginName, bool enabled)
    {
        var plugin = _loadedPlugins.FirstOrDefault(p => p.Name == pluginName);
        plugin?.SetEnabled(enabled);
    }
}

/// <summary>
/// 插件状态信息
/// </summary>
public class PluginStatus
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Version { get; set; } = "";
    public string? Author { get; set; }
    public string? Icon { get; set; }
    public bool IsLoaded { get; set; }
    public bool IsEnabled { get; set; }
}
