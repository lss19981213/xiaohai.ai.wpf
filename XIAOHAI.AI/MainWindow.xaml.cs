using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using XIAOHAI.AI.Services;
using Drawing = System.Drawing;
using IOPath = System.IO.Path;
using WinForms = System.Windows.Forms;

namespace XIAOHAI.AI;

public partial class MainWindow : Window
{
    private string _currentModel = "glm-4.7-flash";
    private string _visionModel = "llava";
    private bool _isSearchEnabled = false;
    private string _baiduApiKey = "";
    private string _baiduUserId = "";

    private readonly OllamaService _ollama = new();
    private readonly SearchService _searchService = new();
    private readonly ImageService _imageService;
    private KnowledgeBaseService _knowledgeBaseService;
    private readonly ChatHistoryService _chatHistoryService = new("chat_history_main.json");
    private readonly List<ChatMessageRecord> _chatHistory = [];
    private bool _isSemanticSearchEnabled = true;
    private List<UIElement> _originalMessageElements = new();
    private readonly List<OllamaService.ChatMessage> _messages = [];
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _typeTimer;
    private readonly DispatcherTimer _breathingTimer;
    private bool _isGenerating;
    private string _streamingContent = "";
    private int _displayedLength;
    private string _fullBuffer = "";
    private string _thinkingText = "";
    private string _answerText = "";
    private readonly object _bufferLock = new();
    private Border? _currentAssistantBubble;
    private string? _userAvatarPath;
    private string? _selectedFilePath;
    private string? _selectedFileContent;
    private string? _ocrTextFromImage;
    private Ellipse? _aiThinkingAvatar;
    private Border? _aiThinkingBubble;
    private double _breathingOpacity = 1.0;
    private bool _isBreathingUp = false;

    private readonly Dictionary<string, string> _quickReplies = new()
    {
        ["总结"] = "请帮我总结以上内容",
        ["翻译"] = "请将以上内容翻译成英文",
        ["代码"] = "请帮我优化这段代码",
        ["解释"] = "请详细解释以上内容",
        ["格式化"] = "请帮我格式化以上内容",
        ["检查"] = "请检查以上内容是否有错误",
        ["优化"] = "请优化以上代码的性能",
        ["改进"] = "请改进以上代码的可读性",
        ["重写"] = "请重写以上代码，使其更简洁",
        ["重构"] = "请重构以上代码",
        ["分析"] = "请分析以上代码的逻辑",
        ["测试"] = "请帮我写测试用例",
        ["文档"] = "请为以上代码添加文档注释",
        ["示例"] = "请给出使用示例",
        ["对比"] = "请对比以上两种方案的优缺点",
        ["评估"] = "请评估以上方案的可行性"
    };

    private readonly List<string> _quickReplyTemplates = new()
    {
        "总结", "请帮我总结以上内容",
        "翻译", "请将以上内容翻译成英文",
        "代码", "请帮我优化这段代码",
        "解释", "请详细解释以上内容",
        "格式化", "请帮我格式化以上内容",
        "检查", "请检查以上内容是否有错误",
        "优化", "请优化以上代码的性能",
        "改进", "请改进以上代码的可读性",
        "重写", "请重写以上代码，使其更简洁",
        "重构", "请重构以上代码",
        "分析", "请分析以上代码的逻辑",
        "测试", "请帮我写测试用例",
        "文档", "请为以上代码添加文档注释",
        "示例", "请给出使用示例",
        "对比", "请对比以上两种方案的优缺点",
        "评估", "请评估以上方案的可行性"
    };

    private Random _waitingRandom = new();
    private List<string> _waitingStrings = new() { ".", "..", "..." };

    private WinForms.NotifyIcon? _notifyIcon;
    private FloatingBall? _floatingBall;
    private bool _isFloatingBallVisible = false;
    private bool _isClosingHandled = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += Window_Closing;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _statusTimer.Tick += (_, _) => _ = RefreshStatusAsync();

        _typeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2) };
        _typeTimer.Tick += TypeTimer_Tick;

        _breathingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _breathingTimer.Tick += BreathingTimer_Tick;

        LoadSettings();

        _searchService = new SearchService(_baiduApiKey, _baiduUserId);
        _imageService = new ImageService(_ollama, _visionModel);
        _knowledgeBaseService = new KnowledgeBaseService(_ollama);

        SendBtn.IsEnabled = false;

        InitializeNotifyIcon();
        LoadChatHistory();
    }

    private void LoadSettings()
    {
        try
        {
            var configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    _ollama.UpdateBaseUrl(settings.OllamaUrl);
                    _currentModel = settings.Model;
                    _visionModel = settings.VisionModel ?? "llava";
                    _baiduApiKey = settings.BaiduApiKey ?? "";
                    _baiduUserId = settings.BaiduUserId ?? "";
                }
            }
        }
        catch
        {
            _currentModel = "glm-4.7-flash";
            _visionModel = "llava";
        }

        if (ModelText != null)
            ModelText.Text = _currentModel;
        if (VisionModelText != null)
            VisionModelText.Text = _visionModel;
    }

    private void TypeTimer_Tick(object? sender, EventArgs e)
    {
        if (_displayedLength >= _fullBuffer.Length)
        {
            _typeTimer.Stop();
            _currentAssistantBubble = null;
            return;
        }
        var remaining = _fullBuffer.Length - _displayedLength;
        var charsToShow = Math.Min(remaining, 5);
        _displayedLength += charsToShow;
        UpdateDisplayBubble();
        ScrollToBottom();
    }

    private void BreathingTimer_Tick(object? sender, EventArgs e)
    {
        if (_isBreathingUp)
        {
            _breathingOpacity += 0.05;
            if (_breathingOpacity >= 1.0) { _breathingOpacity = 1.0; _isBreathingUp = false; }
        }
        else
        {
            _breathingOpacity -= 0.05;
            if (_breathingOpacity <= 0.1) { _breathingOpacity = 0.1; _isBreathingUp = true; }
        }

        if (_aiThinkingAvatar != null) _aiThinkingAvatar.Opacity = _breathingOpacity;
        if (_aiThinkingBubble != null) _aiThinkingBubble.Opacity = _breathingOpacity;
    }

    private void UpdateDisplayBubble()
    {
        if (_currentAssistantBubble?.Child is not StackPanel sp || sp.Children.Count < 2) return;
        var thinkingExpander = sp.Children[0] as Expander;
        var answerBlock = sp.Children[1] as TextBlock;
        if (thinkingExpander == null || answerBlock == null) return;

        if (_thinkingText.Length > 0)
        {
            thinkingExpander.Visibility = Visibility.Visible;
            if (thinkingExpander.Content is TextBlock tb) tb.Text = _thinkingText;
        }
        else
        {
            thinkingExpander.Visibility = Visibility.Collapsed;
        }

        var sepLen = _thinkingText.Length > 0 ? _thinkingText.Length + 2 : 0;
        var answerDisplayLen = Math.Max(0, _displayedLength - sepLen);

        string show = _fullBuffer.Length == 0
            ? $"正在思考中{_waitingStrings[_waitingRandom.Next(0, 2)]}"
            : answerDisplayLen <= 0
                ? $"正在总结中{_waitingStrings[_waitingRandom.Next(0, 2)]}"
                : _answerText[..Math.Min(answerDisplayLen, _answerText.Length)];
        answerBlock.Text = show;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
        _statusTimer.Start();
        
        // 自动检查更新
        await AutoCheckUpdateAsync();
    }

    private async Task AutoCheckUpdateAsync()
    {
        try
        {
            var configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                
                if (settings != null && settings.AutoCheckUpdate)
                {
                    var updateService = new UpdateService(settings.GiteeOwner, settings.GiteeRepo, settings.AppVersion);
                    var result = await updateService.CheckForUpdatesAsync();
                    
                    if (result.HasUpdate)
                    {
                        // 有新版本，显示提示
                        var updateWindow = new UpdateWindow(updateService);
                        updateWindow.Owner = this;
                        updateWindow.Show();
                    }
                }
            }
        }
        catch
        {
            // 自动检查失败时不显示错误，避免打扰用户
        }
    }

    private async Task RefreshStatusAsync()
    {
        var online = await _ollama.PingAsync();
        StatusDot.Fill = online ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        StatusText.Text = online ? "已连接" : "未连接";
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();

    private async void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SendMessageAsync();
        }
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SendBtn.IsEnabled = !string.IsNullOrWhiteSpace(new TextRange(InputBox.Document.ContentStart, InputBox.Document.ContentEnd).Text);
    }

    private async void InputBox_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste) return;

        Drawing.Bitmap bitmap = null;
        try
        {
            if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image != null) bitmap = _imageService.ConvertImageSourceToBitmap(image);
            }

            if (bitmap == null)
            {
                var data = System.Windows.Clipboard.GetDataObject();
                if (data != null)
                {
                    if (data.GetDataPresent("System.Drawing.Bitmap"))
                    {
                        var bmp = data.GetData("System.Drawing.Bitmap") as Drawing.Bitmap;
                        if (bmp != null) bitmap = new Drawing.Bitmap(bmp);
                    }
                    if (bitmap == null && data.GetDataPresent("PNG"))
                    {
                        var pngStream = data.GetData("PNG") as Stream;
                        if (pngStream != null) bitmap = new Drawing.Bitmap(pngStream);
                    }
                    if (bitmap == null && data.GetDataPresent(System.Windows.DataFormats.Dib))
                    {
                        var dibStream = data.GetData(System.Windows.DataFormats.Dib) as Stream;
                        if (dibStream != null) bitmap = _imageService.ConvertDibToBitmap(dibStream);
                    }
                }
            }

            if (bitmap == null && System.Windows.Clipboard.ContainsFileDropList())
            {
                var fileDropList = System.Windows.Clipboard.GetFileDropList();
                if (fileDropList.Count > 0 && FileExtractor.IsImageFile(fileDropList[0]))
                {
                    bitmap = new Drawing.Bitmap(fileDropList[0]);
                }
            }

            if (bitmap != null)
            {
                _ = ProcessImageWithVisionAsync(bitmap);
                e.Handled = true;
            }
            else if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    InputBox.Document.ContentEnd.InsertTextInRun(text);
                    InputBox.CaretPosition = InputBox.Document.ContentEnd;
                }
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            ShowOCRResult($"获取图片失败: {ex.Message}", false);
        }
    }

    private async Task ProcessImageWithVisionAsync(Drawing.Bitmap bitmap)
    {
        try
        {
            ShowOCRResult("正在使用AI视觉模型分析图片...", true);
            var visionResult = await _imageService.AnalyzeImageAsync(bitmap);

            if (!string.IsNullOrWhiteSpace(visionResult) && !visionResult.Contains("失败") && !visionResult.Contains("异常"))
            {
                _ocrTextFromImage = visionResult;
                var displayText = visionResult.Length > 50 ? visionResult.Substring(0, 50) + "..." : visionResult;
                ShowOCRResult($"AI识别成功 ({visionResult.Length}字符): {displayText}", true);
            }
            else
            {
                _ocrTextFromImage = null;
                ShowOCRResult(visionResult, false);
            }
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private void ShowOCRResult(string message, bool isSuccess)
    {
        Dispatcher.Invoke(() =>
        {
            OcrResultText.Text = isSuccess ? $"✅ {message}" : $"❌ {message}";
            OcrResultBorder.Visibility = Visibility.Visible;
            OcrResultText.Foreground = new SolidColorBrush(isSuccess ? System.Windows.Media.Color.FromRgb(74, 222, 128) : System.Windows.Media.Color.FromRgb(239, 68, 68));
        });
    }

    private async Task SendMessageAsync()
    {
        if (StatusText.Text != "已连接")
        {
            System.Windows.MessageBox.Show("还未连接到模型库，请稍后再试试", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var text = new TextRange(InputBox.Document.ContentStart, InputBox.Document.ContentEnd).Text?.Trim() ?? "";
        if ((string.IsNullOrEmpty(text) && string.IsNullOrEmpty(_ocrTextFromImage)) || _isGenerating) return;

        if (!await _ollama.PingAsync())
        {
            System.Windows.MessageBox.Show($"无法连接 Ollama，请确保已启动并已拉取模型：ollama run {_currentModel}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        InputBox.Document.Blocks.Clear();
        InputBox.Document.Blocks.Add(new Paragraph());
        SendBtn.IsEnabled = false;
        WelcomePanel.Visibility = Visibility.Collapsed;

        string knowledgeContent = "";
        if (_isSemanticSearchEnabled)
        {
            knowledgeContent = await _knowledgeBaseService.SearchRelevantContentAsync(text, topK: 5, threshold: 0.25);
        }
        else
        {
            knowledgeContent = await _knowledgeBaseService.LoadContentAsync();
        }

        string searchResults = "";
        if (_isSearchEnabled)
        {
            var searchBubble = MessageBubbleBuilder.CreateSearchResultBubble("正在联网搜索...");
            AddMessageBubbleWithCustomContent("assistant", searchBubble, isPlaceholder: true);
            var searchRow = (StackPanel)MessagePanel.Children[^1];
            var searchBlock = (Border)searchRow.Children[1];

            if (_aiThinkingBubble == null) _aiThinkingBubble = searchBlock;

            try
            {
                searchResults = await _searchService.SearchWebAsync(text);
                searchBlock.Child = MessageBubbleBuilder.CreateSearchResultBubble(searchResults);
                searchBlock.Tag = searchResults;
                _messages.Add(new OllamaService.ChatMessage("system", $"以下是关于'{text}'的网络搜索结果：\n{searchResults}"));
            }
            catch (Exception ex)
            {
                searchBlock.Child = MessageBubbleBuilder.CreateSearchResultBubble("网络搜索失败: " + ex.Message);
            }
        }

        string userMessage = text;
        string displayMessage = text;

        if (!string.IsNullOrEmpty(_selectedFileContent) && !string.IsNullOrEmpty(_selectedFilePath))
        {
            var fileName = IOPath.GetFileName(_selectedFilePath);
            userMessage = $"用户问题: {text}\n\n附加文件内容 ({fileName}):\n{_selectedFileContent}";
            displayMessage = $"[已附加文件: {fileName}]\n{text}";
            _selectedFilePath = null;
            _selectedFileContent = null;
            SelectedFileBorder.Visibility = Visibility.Collapsed;
            SelectedFileNameText.Text = "";
        }

        if (!string.IsNullOrEmpty(_ocrTextFromImage))
        {
            userMessage = string.IsNullOrEmpty(text)
                ? $"图片AI识别内容:\n{_ocrTextFromImage}"
                : $"用户问题: {text}\n\n图片AI识别内容:\n{_ocrTextFromImage}";
            displayMessage = string.IsNullOrEmpty(text)
                ? $"[图片AI识别]: {_ocrTextFromImage}"
                : $"[已附加图片AI识别]\n{text}";
            _ocrTextFromImage = null;
        }

        if (!string.IsNullOrEmpty(knowledgeContent))
        {
            _messages.Add(new OllamaService.ChatMessage("system", $"以下是从知识库中检索到的相关信息，供您参考：\n{knowledgeContent}"));
        }

        _messages.Add(new OllamaService.ChatMessage("user", userMessage));
        AddMessageBubble("user", displayMessage);

        _chatHistory.Add(new ChatMessageRecord { Role = "user", Content = displayMessage, Timestamp = DateTime.Now });

        const int maxMessages = 20;
        if (_messages.Count > maxMessages)
        {
            var recentMessages = _messages.TakeLast(maxMessages).ToList();
            _messages.Clear();
            _messages.AddRange(recentMessages);
        }

        _isGenerating = true;
        SendBtn.IsEnabled = false;
        _streamingContent = "";
        _fullBuffer = "";
        _thinkingText = "";
        _answerText = "";
        _displayedLength = 0;

        AddMessageBubble("assistant", "正在思考中...", isPlaceholder: true);
        _breathingTimer.Start();

        var row = (StackPanel)MessagePanel.Children[^1];
        var assistantBlock = (Border)row.Children[1];
        _currentAssistantBubble = assistantBlock;
        _aiThinkingBubble = assistantBlock;

        var lastWasThinking = false;
        var progress = new Progress<OllamaService.StreamChunk>(chunk =>
        {
            lock (_bufferLock)
            {
                if (!string.IsNullOrEmpty(chunk.ThinkingDelta))
                {
                    _thinkingText += chunk.ThinkingDelta;
                    _fullBuffer += chunk.ThinkingDelta;
                    lastWasThinking = true;
                }
                if (!string.IsNullOrEmpty(chunk.ContentDelta))
                {
                    if (lastWasThinking && _fullBuffer.Length > 0) { _fullBuffer += "\n\n"; lastWasThinking = false; }
                    _answerText += chunk.ContentDelta;
                    _fullBuffer += chunk.ContentDelta;
                }
                _streamingContent = _fullBuffer;
                Dispatcher.InvokeAsync(() => { if (!_typeTimer.IsEnabled && _fullBuffer.Length > 0) _typeTimer.Start(); });
            }
        });

        try
        {
            await _ollama.ChatStreamAsync(_currentModel, _messages, progress);
            _messages.Add(new OllamaService.ChatMessage("assistant", _streamingContent));
            _chatHistory.Add(new ChatMessageRecord { Role = "assistant", Content = _streamingContent, Timestamp = DateTime.Now });
            SaveChatHistory();
        }
        catch (Exception ex)
        {
            _fullBuffer = "❌ 请求失败: " + ex.Message;
            _displayedLength = 0;
            _typeTimer.Stop();
            UpdateDisplayBubble();
        }
        finally
        {
            assistantBlock.Tag = _streamingContent;
            if (_displayedLength < _fullBuffer.Length && !_typeTimer.IsEnabled) _typeTimer.Start();
            _isGenerating = false;
            SendBtn.IsEnabled = true;
            _breathingTimer.Stop();

            if (_aiThinkingAvatar != null) { _aiThinkingAvatar.Opacity = 1.0; _aiThinkingAvatar.Effect = null; }
            if (_aiThinkingBubble != null) { _aiThinkingBubble.Opacity = 1.0; _aiThinkingBubble = null; }

            if (_currentButtonPanel != null)
            {
                _currentButtonPanel.Visibility = Visibility.Visible;
                _currentButtonPanel = null;
            }

            ScrollToBottom();
        }
    }

    private StackPanel? _currentButtonPanel;

    private void AddMessageBubble(string role, string content, bool isPlaceholder = false)
    {
        var isUser = role == "user";
        Ellipse thinkingAvatar = null;
        StackPanel buttonPanel = null;
        var avatar = MessageBubbleBuilder.CreateAvatar(isUser, _userAvatarPath, isPlaceholder && !isUser, ref thinkingAvatar);
        var bubble = MessageBubbleBuilder.CreateMessageBubble(role, content, isPlaceholder, ref thinkingAvatar, _userAvatarPath, ref buttonPanel);
        var row = MessageBubbleBuilder.CreateMessageRow(bubble, avatar, isUser);

        if (!isUser && isPlaceholder)
        {
            _aiThinkingAvatar = thinkingAvatar;
            _currentButtonPanel = buttonPanel;
        }

        MessagePanel.Children.Add(row);
        ScrollToBottom();
    }

    private void AddMessageBubbleWithCustomContent(string role, UIElement customContent, bool isPlaceholder = false)
    {
        var isUser = role == "user";
        Ellipse thinkingAvatar = null;
        var avatar = MessageBubbleBuilder.CreateAvatar(isUser, _userAvatarPath, isPlaceholder && !isUser, ref thinkingAvatar);

        var bg = isUser
            ? (System.Windows.Media.Brush)new LinearGradientBrush(System.Windows.Media.Color.FromArgb(51, 99, 102, 241), System.Windows.Media.Color.FromArgb(38, 139, 92, 246), new System.Windows.Point(0, 1), new System.Windows.Point(1, 0))
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 24, 28, 40));

        var bubble = new Border
        {
            Background = bg,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 99, 102, 241)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = customContent,
            MaxWidth = 480
        };

        var row = MessageBubbleBuilder.CreateMessageRow(bubble, avatar, isUser);
        if (!isUser && isPlaceholder) _aiThinkingAvatar = thinkingAvatar;

        MessagePanel.Children.Add(row);
        ScrollToBottom();
    }

    private void ScrollToBottom() => MessageScroll.ScrollToEnd();

    private void LoadChatHistory()
    {
        var history = _chatHistoryService.LoadHistory();
        if (history.Count == 0) return;

        WelcomePanel.Visibility = Visibility.Collapsed;
        _chatHistory.Clear();
        _chatHistory.AddRange(history);

        foreach (var record in history)
        {
            AddMessageBubbleFromHistory(record);
        }
    }

    private void AddMessageBubbleFromHistory(ChatMessageRecord record)
    {
        var isUser = record.Role == "user";
        Ellipse thinkingAvatar = null;
        StackPanel buttonPanel = null;
        var avatar = MessageBubbleBuilder.CreateAvatar(isUser, _userAvatarPath, false, ref thinkingAvatar);
        var bubble = MessageBubbleBuilder.CreateMessageBubble(record.Role, record.Content, false, ref thinkingAvatar, _userAvatarPath, ref buttonPanel);
        var row = MessageBubbleBuilder.CreateMessageRow(bubble, avatar, isUser);
        MessagePanel.Children.Add(row);
    }

    private void SaveChatHistory()
    {
        _chatHistoryService.SaveHistory(_chatHistory);
    }

    private void ClearMemoryBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;

        var result = System.Windows.MessageBox.Show(
            "确定要清除所有聊天记忆吗？此操作不可恢复。",
            "清除记忆",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _messages.Clear();
            _chatHistory.Clear();
            _chatHistoryService.ClearHistory();
            MessagePanel.Children.Clear();
            WelcomePanel.Visibility = Visibility.Visible;
            System.Windows.MessageBox.Show("聊天记忆已清除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SearchToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        _isSearchEnabled = !_isSearchEnabled;
        if (sender is System.Windows.Controls.Button button)
        {
            button.Foreground = new SolidColorBrush(_isSearchEnabled ? System.Windows.Media.Color.FromRgb(99, 102, 241) : System.Windows.Media.Color.FromRgb(139, 143, 168));
            button.ToolTip = _isSearchEnabled ? "已启用联网搜索" : "启用联网搜索";
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        // 先验证密码
        var passwordDialog = new PasswordDialog();
        passwordDialog.Owner = this;

        if (passwordDialog.ShowDialog() != true)
        {
            return; // 用户取消或密码错误
        }

        if (passwordDialog.Password != "admin888")
        {
            System.Windows.MessageBox.Show("密码错误，无法访问设置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 密码正确，打开设置窗口
        var settingsDialog = new SettingsDialog(_ollama) { Owner = this };
        if (settingsDialog.ShowDialog() == true)
        {
            LoadSettings();
            _ = RefreshStatusAsync();
            System.Windows.MessageBox.Show("设置已保存并应用", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TutorialBtn_Click(object sender, RoutedEventArgs e)
    {
        var tutorialWindow = new TutorialWindow() { Owner = this };
        tutorialWindow.ShowDialog();
    }

    private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                
                if (settings != null)
                {
                    var updateService = new UpdateService(settings.GiteeOwner, settings.GiteeRepo, settings.AppVersion);
                    var updateWindow = new UpdateWindow(updateService) { Owner = this };
                    updateWindow.Show();
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("检查更新失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void KnowledgeBaseBtn_Click(object sender, RoutedEventArgs e)
    {
        var knowledgeBaseWindow = new KnowledgeBaseWindow() { Owner = this };
        knowledgeBaseWindow.ShowDialog();
        _knowledgeBaseService.ClearCache();
    }

    private void OpenClawBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openClawWindow = new OpenClawWebViewWindow() { Owner = this };
            openClawWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"打开OpenClaw WebUI失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OCRBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ocrWindow = new OCRWindow() { Owner = this };
            ocrWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"打开视觉识别窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AutomationBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var automationWindow = new AutomationWindow(_ollama, _visionModel) { Owner = this };
            automationWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"打开自动化窗口失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "文本文件|*.txt;*.ini;*.conf;*.reg;*.log;*.bat;*.cmd;*.c;*.cpp;*.h;*.cs;*.java;*.py;*.html;*.htm;*.css;*.js;*.sql;*.json;*.xml;*.md;*.yml;*.yaml;*.csv|" +
                     "办公文档|*.docx;*.doc;*.xls;*.xlsx;*.pdf|" +
                     "所有文件|*.*",
            Title = "选择要添加的文件",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var fileContent = FileExtractor.ExtractToPlainText(openFileDialog.FileName);
                _selectedFilePath = openFileDialog.FileName;
                _selectedFileContent = FileExtractor.CleanText(fileContent);
                SelectedFileNameText.Text = IOPath.GetFileName(openFileDialog.FileName);
                SelectedFileBorder.Visibility = Visibility.Visible;

                // 显示文件加载成功提示
                var contentLength = _selectedFileContent?.Length ?? 0;
                OcrResultText.Text = $"✅ 文件加载成功：{IOPath.GetFileName(openFileDialog.FileName)} ({contentLength} 字符)";
                OcrResultBorder.Visibility = Visibility.Visible;
                OcrResultText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128));
            }
            catch (Exception ex)
            {
                OcrResultText.Text = $"❌ 读取文件失败：{ex.Message}";
                OcrResultBorder.Visibility = Visibility.Visible;
                OcrResultText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            }
        }
    }

    private void ClearOcrResultBtn_Click(object sender, RoutedEventArgs e)
    {
        OcrResultBorder.Visibility = Visibility.Collapsed;
        OcrResultText.Text = "";
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var searchText = SearchTextBox.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                ClearSearchBtn_Click(sender, e);
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    SearchMessages(searchText);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"搜索错误: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"搜索文本改变错误: {ex.Message}");
        }
    }

    private void SearchMessages(string keyword)
    {
        try
        {
            if (string.IsNullOrEmpty(keyword))
            {
                RestoreOriginalMessages();
                return;
            }

            if (_originalMessageElements.Count == 0)
            {
                SaveOriginalMessages();
            }

            keyword = keyword.ToLower();
            var matchingIndices = new HashSet<int>();

            for (int i = 0; i < _chatHistory.Count; i++)
            {
                if (_chatHistory[i].Content.ToLower().Contains(keyword))
                {
                    matchingIndices.Add(i);
                }
            }

            int elementIndex = 0;
            foreach (var element in MessagePanel.Children)
            {
                if (element is StackPanel row)
                {
                    var isMatch = matchingIndices.Contains(elementIndex);
                    row.Visibility = isMatch ? Visibility.Visible : Visibility.Collapsed;
                    elementIndex++;
                }
            }

            if (matchingIndices.Count == 0)
            {
                UpdateStatus($"未找到包含「{keyword}」的消息");
            }
            else
            {
                UpdateStatus($"找到 {matchingIndices.Count} 条匹配的消息");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"搜索错误: {ex.Message}");
            UpdateStatus($"搜索失败: {ex.Message}");
        }
    }

    private void SaveOriginalMessages()
    {
        try
        {
            _originalMessageElements.Clear();
            foreach (UIElement child in MessagePanel.Children)
            {
                _originalMessageElements.Add(child);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存消息错误: {ex.Message}");
        }
    }

    private void RestoreOriginalMessages()
    {
        try
        {
            MessagePanel.Children.Clear();
            foreach (var element in _originalMessageElements)
            {
                if (element is UIElement uiElement)
                {
                    uiElement.Visibility = Visibility.Visible;
                    MessagePanel.Children.Add(uiElement);
                }
            }
            _originalMessageElements.Clear();
            UpdateStatus("已清除搜索");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"恢复消息错误: {ex.Message}");
        }
    }

    private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SearchTextBox.Text = "";
            RestoreOriginalMessages();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清除搜索错误: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        try
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新状态错误: {ex.Message}");
        }
    }

    private void ExportChatBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_chatHistory.Count == 0)
        {
            System.Windows.MessageBox.Show("当前没有可导出的聊天记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Markdown 文件|*.md|文本文件|*.txt|JSON 文件|*.json",
            Title = "导出聊天记录",
            FileName = $"chat_export_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var format = IOPath.GetExtension(saveFileDialog.FileName).ToLower();
                var content = format switch
                {
                    ".md" => ExportToMarkdown(),
                    ".txt" => ExportToText(),
                    ".json" => ExportToJson(),
                    _ => ExportToMarkdown()
                };

                File.WriteAllText(saveFileDialog.FileName, content, System.Text.Encoding.UTF8);
                System.Windows.MessageBox.Show($"聊天记录已成功导出到：\n{saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string ExportToMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 小海 · 智能对话记录");
        sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"消息总数：{_chatHistory.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var record in _chatHistory)
        {
            var role = record.Role == "user" ? "👤 用户" : "🤖 小海";
            sb.AppendLine($"## {role}");
            sb.AppendLine($"**时间**：{record.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine(record.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ExportToText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("小海 · 智能对话记录");
        sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"消息总数：{_chatHistory.Count}");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        foreach (var record in _chatHistory)
        {
            var role = record.Role == "user" ? "[用户]" : "[小海]";
            sb.AppendLine($"{role} {record.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(record.Content);
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ExportToJson()
    {
        var exportData = new
        {
            exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            messageCount = _chatHistory.Count,
            messages = _chatHistory.Select(r => new
            {
                role = r.Role,
                content = r.Content,
                timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
            })
        };

        return System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private void RemoveFileBtn_Click(object sender, RoutedEventArgs e)
    {
        _selectedFilePath = null;
        _selectedFileContent = null;
        SelectedFileBorder.Visibility = Visibility.Collapsed;
        SelectedFileNameText.Text = "";
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = new Drawing.Icon(IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ziyuan", "bitbug_favicon.ico")),
            Text = "小海 · 智能对话",
            Visible = true
        };

        var contextMenu = new WinForms.ContextMenuStrip();
        var showItem = new WinForms.ToolStripMenuItem("打开小海");
        showItem.Click += (s, e) => ShowMainWindow();

        var exitItem = new WinForms.ToolStripMenuItem("关闭程序");
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.AddRange(new[] { showItem, exitItem });
        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 防止重复执行
        if (_isClosingHandled)
        {
            e.Cancel = true;
            return;
        }

        _isClosingHandled = true;
        e.Cancel = true;

        // 如果悬浮球已显示，则完全隐藏；否则显示悬浮球
        if (_isFloatingBallVisible)
        {
            _floatingBall?.Hide();
            Hide();
        }
        else
        {
            // 先显示悬浮球，再隐藏主窗口，避免悬浮球无法显示
            ShowFloatingBall();

            // 延迟一小段时间再隐藏主窗口，确保悬浮球已经显示
            System.Windows.Threading.DispatcherTimer? timer = null;
            timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            timer.Tick += (s, args) =>
            {
                timer?.Stop();
                Hide();
            };
            timer.Start();
        }
    }

    private void ShowFloatingBall()
    {
        try
        {
            if (_floatingBall == null)
            {
                _floatingBall = new FloatingBall(this);
                System.Diagnostics.Debug.WriteLine($"[悬浮球] 创建成功");
            }

            _floatingBall.Show();
            _isFloatingBallVisible = true;
            System.Diagnostics.Debug.WriteLine($"[悬浮球] 已显示，位置：{_floatingBall.Left}, {_floatingBall.Top}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[悬浮球] 显示失败：{ex.Message}");
            System.Windows.MessageBox.Show($"悬浮球显示失败：{ex.Message}", "错误");
        }
    }

    public void ShowMainWindow()
    {
        // 如果悬浮球显示，先隐藏悬浮球
        if (_isFloatingBallVisible && _floatingBall != null)
        {
            _floatingBall.Hide();
            _isFloatingBallVisible = false;
        }

        // 重置关闭处理标志
        _isClosingHandled = false;

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _floatingBall?.Close();
        _notifyIcon?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    public OllamaService GetOllamaService() => _ollama;

    public string GetVisionModel() => _visionModel;

    private void QuickReplyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void QuickReplyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string template)
        {
            InputBox.Document.Blocks.Clear();
            InputBox.Document.Blocks.Add(new Paragraph(new Run(template)));
            InputBox.Focus();
        }
    }

    private void ManageQuickReplies_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("快捷回复管理功能即将上线！\n\n当前支持的快捷回复：\n• 总结\n• 翻译\n• 代码优化\n• 解释\n• 格式化\n• 检查\n• 优化\n• 改进\n• 重写\n• 重构\n• 分析\n• 测试\n• 文档\n• 示例\n• 对比\n• 评估", "快捷回复管理", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
