using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XIAOHAI.AI.Services;

namespace XIAOHAI.AI;

public partial class AutomationWindow : Window
{
    private readonly OllamaService _ollama;
    private readonly string _model;
    private readonly string _visionModel;
    private readonly AutomationService _automationService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isExecuting = false;
    private Border? _lastAssistantBubble;

    public AutomationWindow() : this(null, null)
    {
    }

    public AutomationWindow(OllamaService? ollama, string? visionModel)
    {
        InitializeComponent();

        var settings = LoadSettings();
        _ollama = ollama ?? new OllamaService(settings?.OllamaUrl ?? "http://localhost:11434");
        _model = settings?.Model ?? "glm-4.7-flash";
        _visionModel = visionModel ?? settings?.VisionModel ?? "llava";
        _automationService = new AutomationService(_ollama, _model, _visionModel);

        _automationService.OnLog += AutomationService_OnLog;
        _automationService.OnProgress += AutomationService_OnProgress;
        _automationService.OnConfirmExecution += AutomationService_OnConfirmExecution;

        Loaded += OnLoaded;
    }

    private Settings? LoadSettings()
    {
        try
        {
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                return System.Text.Json.JsonSerializer.Deserialize<Settings>(json);
            }
        }
        catch { }
        return null;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await CheckConnectionAsync();
        await LoadSkillsAsync();
    }

    private async Task CheckConnectionAsync()
    {
        var isConnected = await _ollama.PingAsync();
        if (isConnected)
        {
            StatusText.Text = "已连接";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        }
        else
        {
            StatusText.Text = "未连接";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }
    }

    private async Task LoadSkillsAsync()
    {
        try
        {
            var skills = await _automationService.GetAllSkillsAsync();
            
            Dispatcher.Invoke(() =>
            {
                SkillListPanel.Children.Clear();
                SkillCountText.Text = skills.Count + "个技能";

                if (skills.Count == 0)
                {
                    // 显示空状态提示
                    var emptyBorder = new Border
                    {
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 26, 28, 37)),
                        Padding = new Thickness(20),
                        Margin = new Thickness(0)
                    };

                    var textBlock = new TextBlock
                    {
                        Text = "暂无技能\n执行自动化任务后会自动保存",
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168)),
                        FontSize = 12,
                        TextAlignment = TextAlignment.Center,
                        LineHeight = 24
                    };

                    emptyBorder.Child = textBlock;
                    SkillListPanel.Children.Add(emptyBorder);
                }
                else
                {
                    // 显示技能列表
                    foreach (var skill in skills)
                    {
                        var skillItem = CreateSkillItem(skill);
                        SkillListPanel.Children.Add(skillItem);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            AddLogMessage("加载技能列表失败：" + ex.Message, "error");
        }
    }

    private Border CreateSkillItem(SkillInfo skill)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 26, 28, 37)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 99, 102, 241)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12),
            Margin = new Thickness(0)
        };

        var stackPanel = new StackPanel();

        // 技能名称
        var nameText = new TextBlock
        {
            Text = "📝 " + skill.Name,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        };
        stackPanel.Children.Add(nameText);

        // 技能描述
        var descText = new TextBlock
        {
            Text = skill.Description,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stackPanel.Children.Add(descText);

        // 使用时间
        var timeText = new TextBlock
        {
            Text = "最后使用：" + skill.ModifiedTime.ToString("yyyy-MM-dd HH:mm"),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168)),
            FontSize = 10,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stackPanel.Children.Add(timeText);

        // 按钮面板
        var buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // 执行按钮
        var executeBtn = new System.Windows.Controls.Button
        {
            Content = "▶ 执行",
            Width = 55,
            Height = 24,
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 11,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = skill.Name
        };
        executeBtn.Click += ExecuteSkillBtn_Click;
        buttonPanel.Children.Add(executeBtn);

        // 编辑按钮
        var editBtn = new System.Windows.Controls.Button
        {
            Content = "✏ 编辑",
            Width = 55,
            Height = 24,
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 11,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = skill.Name
        };
        editBtn.Click += EditSkillBtn_Click;
        buttonPanel.Children.Add(editBtn);

        // 重命名按钮
        var renameBtn = new System.Windows.Controls.Button
        {
            Content = "📝 重命名",
            Width = 65,
            Height = 24,
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 11,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = skill.Name
        };
        renameBtn.Click += RenameSkillBtn_Click;
        buttonPanel.Children.Add(renameBtn);

        // 删除按钮
        var deleteBtn = new System.Windows.Controls.Button
        {
            Content = "🗑 删除",
            Width = 55,
            Height = 24,
            FontSize = 11,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = skill.Name
        };
        deleteBtn.Click += DeleteSkillBtn_Click;
        buttonPanel.Children.Add(deleteBtn);

        stackPanel.Children.Add(buttonPanel);
        border.Child = stackPanel;
        return border;
    }

    private async void ExecuteSkillBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string skillName)
        {
            var skillCode = await _automationService.GetSkillCodeAsync(skillName);
            if (!string.IsNullOrEmpty(skillCode))
            {
                AddLogMessage("执行技能：" + skillName, "progress");
                try
                {
                    await _automationService.ExecuteTaskAsync("执行技能：" + skillName, null, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AddLogMessage("执行失败：" + ex.Message, "error");
                }
            }
        }
    }

    private async void EditSkillBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string skillName)
        {
            var skillCode = await _automationService.GetSkillCodeAsync(skillName);
            if (!string.IsNullOrEmpty(skillCode))
            {
                // TODO: 打开代码编辑器窗口
                AddLogMessage("编辑技能：" + skillName, "info");
                System.Windows.MessageBox.Show("编辑功能开发中...\n\n技能名称：" + skillName + "\n\n代码：\n" + skillCode, "编辑技能", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void RenameSkillBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string skillName)
        {
            // TODO: 打开重命名对话框
            AddLogMessage("重命名技能：" + skillName, "info");
            System.Windows.MessageBox.Show("重命名功能开发中...\n\n当前技能名称：" + skillName, "重命名技能", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void DeleteSkillBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string skillName)
        {
            var result = System.Windows.MessageBox.Show("确定要删除技能 \"" + skillName + "\" 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                var success = await _automationService.DeleteSkillAsync(skillName);
                if (success)
                {
                    AddLogMessage("已删除技能：" + skillName, "success");
                    await LoadSkillsAsync();
                }
                else
                {
                    AddLogMessage("删除技能失败：" + skillName, "error");
                }
            }
        }
    }

    private void AddUserMessage(string message)
    {
        var bubble = CreateMessageBubble(message, isUser: true);
        MessagePanel.Children.Add(bubble);
        ScrollToBottom();
    }

    private Border CreateMessageBubble(string message, bool isUser)
    {
        System.Windows.Media.Brush bg = isUser
            ? (System.Windows.Media.Brush)new LinearGradientBrush(
                  System.Windows.Media.Color.FromArgb(51, 99, 102, 241),
                  System.Windows.Media.Color.FromArgb(38, 139, 92, 246),
                  new System.Windows.Point(0, 1),
                  new System.Windows.Point(1, 0))
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 24, 28, 40));

        var border = new Border
        {
            Background = bg,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 99, 102, 241)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            MaxWidth = 600,
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var textBlock = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };

        border.Child = textBlock;
        return border;
    }

    private void AddAssistantMessage(string message)
    {
        _lastAssistantBubble = CreateMessageBubble(message, isUser: false);
        MessagePanel.Children.Add(_lastAssistantBubble);
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        MessageScroll.ScrollToEnd();
    }

    private void AutomationService_OnLog(string message, string type)
    {
        Dispatcher.Invoke(() =>
        {
            AddLogMessage(message, type);
        });
    }

    private void AutomationService_OnProgress(int progress, string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
        });
    }

    private async Task<bool> AutomationService_OnConfirmExecution(string script, string description, List<string> risks)
    {
        var result = await Dispatcher.InvokeAsync(() =>
        {
            var confirmWindow = new SecurityConfirmationWindow(description, risks, script);
            confirmWindow.Owner = this;
            return confirmWindow.ShowDialog() == true;
        });

        return result;
    }

    private void AddLogMessage(string message, string type)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        
        var logItem = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 26, 28, 37)),
            BorderBrush = GetLogColor(type),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var textBlock = new TextBlock
        {
            Text = "[" + time + "] " + message,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas")
        };

        logItem.Child = textBlock;
        LogPanel.Children.Add(logItem);
        LogScroll.ScrollToEnd();
    }

    private SolidColorBrush GetLogColor(string type)
    {
        return type switch
        {
            "success" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),
            "error" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
            "warning" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
            "progress" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241)),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168))
        };
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteTaskAsync();
    }

    private async void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await ExecuteTaskAsync();
        }
    }

    private async Task ExecuteTaskAsync()
    {
        var task = InputBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(task))
        {
            return;
        }

        if (_isExecuting)
        {
            AddAssistantMessage("任务正在执行中，请稍候...");
            return;
        }

        var isConnected = await _ollama.PingAsync();
        if (!isConnected)
        {
            AddAssistantMessage("❌ 无法连接到 AI 模型，请检查 Ollama 是否启动");
            return;
        }

        // 添加用户消息
        AddUserMessage(task);
        InputBox.Text = "";
        
        _isExecuting = true;
        SendBtn.IsEnabled = false;
        InputBox.IsEnabled = false;

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // 执行任务
            var result = await _automationService.ExecuteTaskAsync(task, null, _cancellationTokenSource.Token);

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AddAssistantMessage("⚠️ 任务已取消");
            }
            else
            {
                AddAssistantMessage("✅ " + result);
            }

            // 刷新技能列表
            await LoadSkillsAsync();
        }
        catch (OperationCanceledException)
        {
            AddAssistantMessage("⚠️ 任务已取消");
        }
        catch (Exception ex)
        {
            AddAssistantMessage("❌ 执行失败：" + ex.Message);
        }
        finally
        {
            _isExecuting = false;
            SendBtn.IsEnabled = true;
            InputBox.IsEnabled = true;
            InputBox.Focus();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            StatusText.Text = "就绪";
        }
    }

    private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
    {
        LogPanel.Children.Clear();
        AddLogMessage("日志已清空", "info");
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _automationService?.Dispose();
        base.OnClosed(e);
    }
}
