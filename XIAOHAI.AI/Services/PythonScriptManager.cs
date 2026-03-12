using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace XIAOHAI.AI.Services;

/// <summary>
/// Python 脚本管理器 - 管理自动化技能库
/// </summary>
public class PythonScriptManager
{
    private readonly string _scriptsFolder;
    private readonly Dictionary<string, ScriptMetadata> _metadataCache;

    public PythonScriptManager()
    {
        _scriptsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "automation_scripts");
        _metadataCache = new Dictionary<string, ScriptMetadata>();

        // 确保目录存在
        if (!Directory.Exists(_scriptsFolder))
        {
            Directory.CreateDirectory(_scriptsFolder);
        }

        // 加载元数据
        LoadMetadata();
    }

    /// <summary>
    /// 保存脚本
    /// </summary>
    public async Task SaveScriptAsync(string taskDescription, string script, string result)
    {
        try
        {
            // 生成技能名称
            string skillName = GenerateSkillName(taskDescription);
            
            // 保存脚本文件
            string scriptPath = GetScriptPath(skillName);
            await File.WriteAllTextAsync(scriptPath, script);

            // 更新元数据
            var metadata = new ScriptMetadata
            {
                Name = skillName,
                Description = taskDescription,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now,
                LastUsedTime = DateTime.Now,
                UsageCount = 1,
                LastResult = result
            };

            _metadataCache[skillName] = metadata;
            await SaveMetadataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存脚本失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取脚本
    /// </summary>
    public async Task<string?> GetScriptAsync(string skillName)
    {
        try
        {
            string scriptPath = GetScriptPath(skillName);
            if (File.Exists(scriptPath))
            {
                // 更新使用次数
                if (_metadataCache.TryGetValue(skillName, out var metadata))
                {
                    metadata.UsageCount++;
                    metadata.LastUsedTime = DateTime.Now;
                    await SaveMetadataAsync();
                }

                return await File.ReadAllTextAsync(scriptPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取脚本失败：{ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 删除脚本
    /// </summary>
    public async Task<bool> DeleteScriptAsync(string skillName)
    {
        try
        {
            string scriptPath = GetScriptPath(skillName);
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
                _metadataCache.Remove(skillName);
                await SaveMetadataAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除脚本失败：{ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// 更新脚本
    /// </summary>
    public async Task<bool> UpdateScriptAsync(string skillName, string newCode)
    {
        try
        {
            string scriptPath = GetScriptPath(skillName);
            if (File.Exists(scriptPath))
            {
                await File.WriteAllTextAsync(scriptPath, newCode);
                
                if (_metadataCache.TryGetValue(skillName, out var metadata))
                {
                    metadata.ModifiedTime = DateTime.Now;
                    await SaveMetadataAsync();
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"更新脚本失败：{ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// 获取所有技能描述
    /// </summary>
    public async Task<string> GetAllScriptsDescriptionAsync()
    {
        var descriptions = new List<string>();
        foreach (var metadata in _metadataCache.Values)
        {
            descriptions.Add($"- {metadata.Name}: {metadata.Description}");
        }
        return string.Join("\n", descriptions);
    }

    /// <summary>
    /// 获取所有技能
    /// </summary>
    public Task<List<SkillInfo>> GetAllSkillsAsync()
    {
        var skills = _metadataCache.Values.Select(m => new SkillInfo
        {
            Name = m.Name,
            Description = m.Description,
            CreatedTime = m.CreatedTime,
            ModifiedTime = m.ModifiedTime
        }).ToList();
        return Task.FromResult(skills);
    }

    /// <summary>
    /// 生成技能名称
    /// </summary>
    private string GenerateSkillName(string taskDescription)
    {
        // 简化任务描述为技能名称
        string name = taskDescription.ToLower();
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
        name = name.Replace(" ", "_");
        
        if (name.Length > 50)
        {
            name = name.Substring(0, 50);
        }

        // 添加时间戳避免重复
        name = $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}";
        return name;
    }

    /// <summary>
    /// 获取脚本路径
    /// </summary>
    private string GetScriptPath(string skillName)
    {
        return Path.Combine(_scriptsFolder, $"{skillName}.py");
    }

    /// <summary>
    /// 加载元数据
    /// </summary>
    private void LoadMetadata()
    {
        try
        {
            string metadataPath = GetMetadataPath();
            if (File.Exists(metadataPath))
            {
                var json = File.ReadAllText(metadataPath);
                var metadataList = JsonSerializer.Deserialize<List<ScriptMetadata>>(json);
                if (metadataList != null)
                {
                    _metadataCache.Clear();
                    foreach (var metadata in metadataList)
                    {
                        _metadataCache[metadata.Name] = metadata;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载元数据失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 保存元数据
    /// </summary>
    private async Task SaveMetadataAsync()
    {
        try
        {
            string metadataPath = GetMetadataPath();
            var json = JsonSerializer.Serialize(_metadataCache.Values, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存元数据失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取元数据文件路径
    /// </summary>
    private string GetMetadataPath()
    {
        return Path.Combine(_scriptsFolder, "metadata.json");
    }
}

/// <summary>
/// 脚本元数据
/// </summary>
public class ScriptMetadata
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
    public DateTime LastUsedTime { get; set; }
    public int UsageCount { get; set; }
    public string LastResult { get; set; } = "";
}
