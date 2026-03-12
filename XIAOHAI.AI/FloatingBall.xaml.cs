using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace XIAOHAI.AI;

public partial class FloatingBall : Window
{
    private bool _isDragging;
    private WpfPoint _startPoint;
    private readonly MainWindow _mainWindow;
    private DispatcherTimer? _hideInfoTimer;
    private readonly string[] _greetings = new[]
    {
        "小海在这里哦！",
        "嗨～今天过得怎么样？",
        "需要我帮忙吗？",
        "遇到什么问题了？",
        "小海随时待命！",
        "有什么想问小海的吗？",
        "小海可以帮你分析屏幕内容哦！"
    };
    private readonly Random _random = new();
    private System.Windows.Controls.ContextMenu? _contextMenu;
    private AnswerWindow? _currentAnswerWindow;

    public FloatingBall(MainWindow mainWindow)
    {
        System.Diagnostics.Debug.WriteLine($"[悬浮球] 开始创建...");
        InitializeComponent();
        _mainWindow = mainWindow;

        // 设置初始位置为屏幕右下角
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = screenWidth - Width - 20;
        Top = screenHeight - Height - 100;

        // 初始化右键菜单
        InitializeContextMenu();

        System.Diagnostics.Debug.WriteLine($"[悬浮球] 创建完成，位置：{Left}, {Top}");
    }

    private void InitializeContextMenu()
    {
        _contextMenu = new System.Windows.Controls.ContextMenu();

        // 查看当前问题
        var viewProblemItem = new System.Windows.Controls.MenuItem
        {
            Header = "🔍 查看当前问题",
            FontSize = 12,
            Padding = new Thickness(12, 8, 12, 8)
        };
        viewProblemItem.Click += ViewProblemItem_Click;
        _contextMenu.Items.Add(viewProblemItem);

        _contextMenu.Items.Add(new System.Windows.Controls.Separator());

        // 打开主窗口
        var openMainItem = new System.Windows.Controls.MenuItem
        {
            Header = "📱 打开主窗口",
            FontSize = 12,
            Padding = new Thickness(12, 8, 12, 8)
        };
        openMainItem.Click += (s, e) => _mainWindow.ShowMainWindow();
        _contextMenu.Items.Add(openMainItem);

        // 隐藏悬浮球
        var hideItem = new System.Windows.Controls.MenuItem
        {
            Header = "🔽 隐藏悬浮球",
            FontSize = 12,
            Padding = new Thickness(12, 8, 12, 8)
        };
        hideItem.Click += HideItem_Click;
        _contextMenu.Items.Add(hideItem);

        ContextMenu = _contextMenu;
    }

    private void ShowInfoBubble(string message)
    {
        InfoText.Text = message;
        InfoBubble.Visibility = Visibility.Visible;

        // 淡入动画
        var fadeInAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300)
        };
        InfoBubble.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);

        // 3 秒后自动隐藏
        _hideInfoTimer?.Stop();
        _hideInfoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hideInfoTimer.Tick += (s, e) =>
        {
            HideInfoBubble();
            _hideInfoTimer?.Stop();
        };
        _hideInfoTimer.Start();
    }

    private void ShowGreetingBubble()
    {
        var randomIndex = _random.Next(_greetings.Length);
        ShowInfoBubble(_greetings[randomIndex]);
    }

    private void HideInfoBubble()
    {
        var fadeOutAnimation = new DoubleAnimation
        {
            From = InfoBubble.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300)
        };
        fadeOutAnimation.Completed += (s, e) =>
        {
            InfoBubble.Visibility = Visibility.Collapsed;
        };
        InfoBubble.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _startPoint = e.GetPosition(this);
        CaptureMouse();
        ShowGreetingBubble();
    }

    private void Window_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        ShowGreetingBubble();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void Window_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _startPoint;

        Left += delta.X;
        Top += delta.Y;

        // 限制在屏幕内
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        if (Left < 0) Left = 0;
        if (Top < 0) Top = 0;
        if (Left + Width > screenWidth) Left = screenWidth - Width;
        if (Top + Height > screenHeight) Top = screenHeight - Height;
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _mainWindow.ShowMainWindow();
    }

    private async void ViewProblemItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 关闭之前打开的回答窗口
            if (_currentAnswerWindow != null)
            {
                try
                {
                    _currentAnswerWindow.Close();
                }
                catch { }
                _currentAnswerWindow = null;
            }

            // 创建并显示回答窗口
            var ollama = _mainWindow.GetOllamaService();
            var visionModel = _mainWindow.GetVisionModel();
            var imageService = new XIAOHAI.AI.Services.ImageService(ollama, visionModel);
            _currentAnswerWindow = new AnswerWindow(ollama, imageService, visionModel);
            
            // 设置窗口位置为悬浮球上方
            var ballLeft = Left;
            var ballTop = Top;
            
            _currentAnswerWindow.Left = ballLeft - (_currentAnswerWindow.Width - Width) / 2;
            _currentAnswerWindow.Top = ballTop - _currentAnswerWindow.Height - 10;
            
            // 确保窗口在屏幕范围内
            if (_currentAnswerWindow.Top < 0)
            {
                _currentAnswerWindow.Top = 10;
            }
            
            if (_currentAnswerWindow.Left < 0)
            {
                _currentAnswerWindow.Left = 10;
            }
            
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            if (_currentAnswerWindow.Left + _currentAnswerWindow.Width > screenWidth)
            {
                _currentAnswerWindow.Left = screenWidth - _currentAnswerWindow.Width - 10;
            }
            
            System.Diagnostics.Debug.WriteLine($"[悬浮球] 显示回答窗口，位置：{_currentAnswerWindow.Left}, {_currentAnswerWindow.Top}");
            
            _currentAnswerWindow.Show();
            
            // 显示提示信息
            ShowInfoBubble("正在分析屏幕内容...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[悬浮球] 打开回答窗口失败：{ex.Message}");
            ShowInfoBubble($"打开失败：{ex.Message}");
        }
    }

    private void HideItem_Click(object sender, RoutedEventArgs e)
    {
        // 隐藏悬浮球
        Hide();
        
        System.Diagnostics.Debug.WriteLine("[悬浮球] 已隐藏，可通过主窗口重新打开");
    }
}
