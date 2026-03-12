using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using XIAOHAI.AI.Services;

namespace XIAOHAI.AI.Services;

public class AutomationService : IDisposable
{
    private readonly OllamaService _ollama;
    private readonly string _model;
    private readonly string _visionModel;
    private readonly SearchService _searchService;
    private readonly PythonScriptManager _scriptManager;
    private CancellationTokenSource? _internalCts;
    private bool _disposed = false;

    public delegate void LogEventHandler(string message, string type);
    public delegate void ProgressEventHandler(int progress, string message);
    public delegate Task<bool> ConfirmExecutionHandler(string script, string description, List<string> risks);

    public event LogEventHandler? OnLog;
    public event ProgressEventHandler? OnProgress;
    public event ConfirmExecutionHandler? OnConfirmExecution;

    public AutomationService(OllamaService ollama, string model = "glm-4.7-flash", string visionModel = "llava", SearchService? searchService = null)
    {
        _ollama = ollama;
        _model = model;
        _visionModel = visionModel;
        _searchService = searchService ?? new SearchService();
        _scriptManager = new PythonScriptManager();
    }

    public async Task<string> ExecuteTaskAsync(string task, string? screenshotBase64 = null, CancellationToken ct = default)
    {
        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            ReportProgress(0, "正在分析任务...");
            ReportLog("收到任务：" + task, "info");

            ReportProgress(10, "AI 正在分析任务...");
            var analysisResult = await AnalyzeTaskAsync(task, _internalCts.Token);

            if (string.IsNullOrEmpty(analysisResult))
            {
                ReportLog("任务分析失败", "error");
                return "任务分析失败";
            }

            ReportLog("分析完成", "progress");

            ReportProgress(30, "查找现成解决方案...");
            var existingScript = await FindExistingScriptAsync(task, analysisResult, _internalCts.Token);

            string pythonScript;
            if (!string.IsNullOrEmpty(existingScript))
            {
                ReportLog("找到现成技能，准备执行", "success");
                pythonScript = existingScript;
            }
            else
            {
                ReportProgress(50, "未找到现成方案，正在搜索并生成脚本...");
                pythonScript = await GenerateNewScriptAsync(task, analysisResult, _internalCts.Token);
            }

            ReportProgress(70, "安全检查...");
            var safetyCheck = await CheckScriptSafetyAsync(pythonScript, _internalCts.Token);
            
            if (!safetyCheck.IsSafe)
            {
                ReportLog("检测到风险：" + string.Join(", ", safetyCheck.Risks), "warning");
                
                if (OnConfirmExecution != null)
                {
                    var confirmed = await OnConfirmExecution.Invoke(pythonScript, safetyCheck.Description, safetyCheck.Risks);
                    if (!confirmed)
                    {
                        ReportLog("用户取消执行", "warning");
                        return "用户取消执行";
                    }
                }
                else
                {
                    ReportLog("存在潜在风险，已跳过用户确认", "warning");
                }
            }

            ReportProgress(90, "正在执行自动化脚本...");
            var result = await ExecutePythonScriptAsync(pythonScript, task, _internalCts.Token);

            if (string.IsNullOrEmpty(existingScript) && !string.IsNullOrEmpty(result))
            {
                await _scriptManager.SaveScriptAsync(task, pythonScript, result);
                ReportLog("已保存新技能到技能库", "success");
            }

            ReportProgress(100, "任务完成");
            ReportLog("任务执行完成", "success");

            return result;
        }
        catch (OperationCanceledException)
        {
            ReportLog("任务已取消", "warning");
            return "任务已取消";
        }
        catch (Exception ex)
        {
            ReportLog("执行失败：" + ex.Message, "error");
            throw;
        }
        finally
        {
            _internalCts?.Dispose();
            _internalCts = null;
        }
    }

    private async Task<string> AnalyzeTaskAsync(string task, CancellationToken ct)
    {
        try
        {
            string prompt = "你是一个专业的自动化任务分析助手。请分析用户的任务需求：\n\n用户任务：" + task + 
                "\n\n请分析：\n1. 这个任务的核心目标是什么？\n2. 需要操作哪些应用程序或系统？\n3. 可能涉及哪些自动化操作？\n4. 是否存在潜在的安全风险？\n\n请给出详细的分析结果：";

            var messages = new List<OllamaService.ChatMessage>
            {
                new OllamaService.ChatMessage("user", prompt)
            };

            var result = await _ollama.ChatAsync(_model, messages, ct);
            return result;
        }
        catch (Exception ex)
        {
            ReportLog("任务分析失败：" + ex.Message, "error");
            return "";
        }
    }

    private async Task<string?> FindExistingScriptAsync(string task, string analysis, CancellationToken ct)
    {
        try
        {
            string prompt = "你是一个技能匹配助手。现有以下技能库：\n\n" + await _scriptManager.GetAllScriptsDescriptionAsync() + 
                "\n\n用户任务：" + task + "\n\n任务分析：" + analysis + 
                "\n\n请判断：\n1. 是否有现成的技能可以解决这个任务？\n2. 如果有，请返回技能名称（只返回名称）\n3. 如果没有，返回 NONE\n\n技能名称：";

            var messages = new List<OllamaService.ChatMessage>
            {
                new OllamaService.ChatMessage("user", prompt)
            };

            var result = await _ollama.ChatAsync(_model, messages, ct);
            result = result.Trim().ToUpper();

            if (result != "NONE" && !string.IsNullOrEmpty(result))
            {
                var script = await _scriptManager.GetScriptAsync(result);
                if (!string.IsNullOrEmpty(script))
                {
                    ReportLog("找到匹配技能：" + result, "success");
                    return script;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            ReportLog("查找技能失败：" + ex.Message, "error");
            return null;
        }
    }

    private async Task<string> GenerateNewScriptAsync(string task, string analysis, CancellationToken ct)
    {
        try
        {
            ReportLog("正在联网搜索相关信息...", "progress");
            string searchResults = await _searchService.SearchWebAsync(task);
            ReportLog("搜索完成，获取到相关信息", "success");

            string prompt = "你是一个专业的自动化脚本编写助手。请根据以下信息编写一个脚本来完成用户的任务。\n\n" +
                "【用户任务】" + task + "\n\n" +
                "【任务分析】" + analysis + "\n\n" +
                "【搜索结果】" + searchResults + "\n\n" +
                "【重要要求】\n" +
                "1. 使用 Python 语法，但必须使用 .NET 类库（因为这是 IronPython 环境）\n" +
                "2. 使用 clr.AddReference 来引用 .NET 程序集\n" +
                "3. 正确的程序集引用方式：\n" +
                "   - System.dll 包含：System.Diagnostics, System.IO, System.Threading 等\n" +
                "   - System.Windows.Forms.dll 包含：SendKeys, Clipboard, MessageBox 等\n" +
                "   - System.Drawing.dll 包含：Point, Size, Color, Bitmap 等\n" +
                "4. 可用示例：\n" +
                "   - 启动程序：import clr; clr.AddReference('System'); from System.Diagnostics import Process; Process.Start('notepad.exe')\n" +
                "   - 文件操作：import clr; clr.AddReference('System'); from System.IO import File; File.ReadAllText('path')\n" +
                "   - 线程等待：import clr; clr.AddReference('System'); from System.Threading import Thread; Thread.Sleep(1000)\n" +
                "   - 键盘输入：import clr; clr.AddReference('System.Windows.Forms'); from System.Windows.Forms import SendKeys; SendKeys.SendWait('text')\n" +
                "   - 输出信息：使用 print() 函数\n" +
                "5. 代码要简洁、健壮、有错误处理\n" +
                "6. 在关键操作前添加注释说明\n" +
                "7. 脚本执行完成后输出结果\n\n" +
                "请直接输出 Python 代码，不要其他说明：\n\n```python\n# 你的代码在这里\n```";

            var messages = new List<OllamaService.ChatMessage>
            {
                new OllamaService.ChatMessage("user", prompt)
            };

            var result = await _ollama.ChatAsync(_model, messages, ct);
            
            var code = ExtractCodeFromResponse(result);
            return code;
        }
        catch (Exception ex)
        {
            ReportLog("生成脚本失败：" + ex.Message, "error");
            throw;
        }
    }

    private async Task<SafetyCheckResult> CheckScriptSafetyAsync(string script, CancellationToken ct)
    {
        try
        {
            string prompt = "你是一个代码安全审计助手。请检查以下 Python 脚本是否存在安全风险：\n\n```python\n" + script + "\n```\n\n" +
                "【检查项目】\n" +
                "1. 是否包含危险操作（删除文件、格式化磁盘、修改系统关键配置等）\n" +
                "2. 是否包含恶意代码（病毒、木马、后门等）\n" +
                "3. 是否包含隐私窃取行为（读取密码、窃取文件等）\n" +
                "4. 是否包含网络攻击行为（DDoS、端口扫描等）\n" +
                "5. 是否有无限循环或资源耗尽风险\n\n" +
                "【输出格式】\n" +
                "JSON 格式：\n{\n  \"is_safe\": true/false,\n  \"risks\": [\"风险 1\", \"风险 2\"],\n  \"description\": \"脚本功能描述\"\n}\n\n请输出 JSON：";

            var messages = new List<OllamaService.ChatMessage>
            {
                new OllamaService.ChatMessage("user", prompt)
            };

            var result = await _ollama.ChatAsync(_model, messages, ct);
            
            bool isSafe = !result.Contains("\"is_safe\": false") && !result.Contains("\"is_safe\":false");
            var risks = new List<string>();
            string description = "自动化脚本";

            if (!isSafe)
            {
                if (result.Contains("\"risks\":"))
                {
                    risks.Add("检测到潜在风险操作");
                }
            }

            return new SafetyCheckResult
            {
                IsSafe = isSafe,
                Risks = risks,
                Description = description
            };
        }
        catch (Exception ex)
        {
            ReportLog("安全检查失败：" + ex.Message, "error");
            return new SafetyCheckResult { IsSafe = true, Risks = new List<string>(), Description = "未知脚本" };
        }
    }

    private async Task<string> ExecutePythonScriptAsync(string script, string task, CancellationToken ct)
    {
        try
        {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();

            // 预先导入 clr 模块，使 Python 能够使用.NET 类库
            // 注意：System.Diagnostics 包含在 System.dll 中，不需要单独引用
            var clrScript = @"
import clr
clr.AddReference('System')
clr.AddReference('System.Windows.Forms')
clr.AddReference('System.Drawing')
from System import String, Array
from System.Diagnostics import Process
from System.IO import File, Directory, Path
from System.Threading import Thread
from System.Windows.Forms import SendKeys, Clipboard
from System.Drawing import Point, Size
";
            // 先执行初始化脚本
            engine.Execute(clrScript, scope);

            await Task.Run(() =>
            {
                try
                {
                    // 执行用户脚本
                    engine.Execute(script, scope);
                }
                catch (Exception ex)
                {
                    ReportLog("脚本执行错误：" + ex.Message, "error");
                    throw;
                }
            }, ct);

            return "脚本执行成功";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportLog("脚本执行失败：" + ex.Message, "error");
            throw;
        }
    }

    private string ExtractCodeFromResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return "";

        int startIndex = response.IndexOf("```python");
        if (startIndex < 0)
            startIndex = response.IndexOf("```");
        
        if (startIndex >= 0)
        {
            startIndex = response.IndexOf("\n", startIndex) + 1;
            int endIndex = response.IndexOf("```", startIndex);
            if (endIndex > startIndex)
            {
                return response.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }

        return response.Trim();
    }

    public async Task<List<SkillInfo>> GetAllSkillsAsync()
    {
        return await _scriptManager.GetAllSkillsAsync();
    }

    public async Task<bool> DeleteSkillAsync(string skillName)
    {
        return await _scriptManager.DeleteScriptAsync(skillName);
    }

    public async Task<string?> GetSkillCodeAsync(string skillName)
    {
        return await _scriptManager.GetScriptAsync(skillName);
    }

    public async Task<bool> UpdateSkillAsync(string skillName, string newCode)
    {
        return await _scriptManager.UpdateScriptAsync(skillName, newCode);
    }

    private void ReportLog(string message, string type)
    {
        OnLog?.Invoke(message, type);
    }

    private void ReportProgress(int progress, string message)
    {
        OnProgress?.Invoke(progress, message);
    }

    public void Cancel()
    {
        _internalCts?.Cancel();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _internalCts?.Cancel();
            _internalCts?.Dispose();
            _disposed = true;
        }
    }
}

public class SafetyCheckResult
{
    public bool IsSafe { get; set; }
    public List<string> Risks { get; set; } = new List<string>();
    public string Description { get; set; } = "";
}

public class SkillInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
}
