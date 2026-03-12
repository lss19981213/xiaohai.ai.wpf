using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XIAOHAI.AI.Plugins;

/// <summary>
/// 示例插件 - 快速计算器
/// 当用户输入数学表达式时，直接计算结果
/// </summary>
public class CalculatorPlugin : IPlugin
{
    private bool _isEnabled = true;

    public string Name => "快速计算器";
    public string Description => "智能识别数学表达式并快速计算结果";
    public string Version => "1.0.0";
    public string Author => "小海 AI";
    public string Icon => "🔢";
    public bool IsEnabled => _isEnabled;

    public Task InitializeAsync()
    {
        Console.WriteLine("[CalculatorPlugin] 初始化完成");
        return Task.CompletedTask;
    }

    public System.Windows.Controls.UserControl? GetSettingsView() => null;

    public async Task<PluginResponse?> ProcessMessageAsync(PluginContext context)
    {
        var message = context.Message.Trim();
        
        // 检查是否是数学表达式
        if (IsMathExpression(message))
        {
            try
            {
                var result = Calculate(message);
                return new PluginResponse
                {
                    Intercept = false, // 不拦截，让 AI 也回答
                    Content = $"💡 计算结果：**{message} = {result}**",
                    Action = "show_calculation"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CalculatorPlugin] 计算失败：{ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// 判断是否是数学表达式
    /// </summary>
    private bool IsMathExpression(string input)
    {
        // 简单的数学表达式模式
        var pattern = @"^[\d\s\+\-\*\/\.\(\)\^\%]+$";
        return Regex.IsMatch(input, pattern) && input.Any(char.IsDigit);
    }

    /// <summary>
    /// 计算数学表达式
    /// </summary>
    private double Calculate(string expression)
    {
        // 简单的计算器实现
        return EvaluateExpression(expression);
    }

    private double EvaluateExpression(string expr)
    {
        // 移除空格
        expr = expr.Replace(" ", "");
        
        // 处理括号
        while (expr.Contains('('))
        {
            var start = expr.LastIndexOf('(');
            var end = expr.IndexOf(')', start);
            var inner = expr.Substring(start + 1, end - start - 1);
            var result = EvaluateSimple(inner);
            expr = expr.Substring(0, start) + result + expr.Substring(end + 1);
        }
        
        return EvaluateSimple(expr);
    }

    private double EvaluateSimple(string expr)
    {
        // 处理加减法
        var plusIndex = expr.LastIndexOf('+');
        var minusIndex = expr.LastIndexOf('-', expr.Length - 1);
        
        if (plusIndex > 0)
        {
            var left = EvaluateSimple(expr.Substring(0, plusIndex));
            var right = EvaluateSimple(expr.Substring(plusIndex + 1));
            return left + right;
        }
        
        if (minusIndex > 0)
        {
            var left = EvaluateSimple(expr.Substring(0, minusIndex));
            var right = EvaluateSimple(expr.Substring(minusIndex + 1));
            return left - right;
        }
        
        // 处理乘除法
        var mulIndex = expr.LastIndexOf('*');
        var divIndex = expr.LastIndexOf('/');
        
        if (mulIndex > 0)
        {
            var left = EvaluateSimple(expr.Substring(0, mulIndex));
            var right = EvaluateSimple(expr.Substring(mulIndex + 1));
            return left * right;
        }
        
        if (divIndex > 0)
        {
            var left = EvaluateSimple(expr.Substring(0, divIndex));
            var right = EvaluateSimple(expr.Substring(divIndex + 1));
            return left / right;
        }
        
        // 处理幂运算
        var powIndex = expr.IndexOf('^');
        if (powIndex > 0)
        {
            var left = EvaluateSimple(expr.Substring(0, powIndex));
            var right = EvaluateSimple(expr.Substring(powIndex + 1));
            return Math.Pow(left, right);
        }
        
        // 处理百分比
        if (expr.EndsWith('%'))
        {
            return EvaluateSimple(expr.Substring(0, expr.Length - 1)) / 100;
        }
        
        // 解析数字
        if (double.TryParse(expr, out var number))
        {
            return number;
        }
        
        throw new ArgumentException($"无法解析表达式：{expr}");
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        Console.WriteLine($"[CalculatorPlugin] 插件已{(enabled ? "启用" : "禁用")}");
    }

    public Task ShutdownAsync()
    {
        Console.WriteLine("[CalculatorPlugin] 插件已关闭");
        return Task.CompletedTask;
    }
}
