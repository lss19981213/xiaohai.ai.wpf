using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XIAOHAI.AI.Services;

namespace XIAOHAI.AI;

public partial class AnswerWindow : Window
{
    private readonly OllamaService _ollama;
    private readonly ImageService _imageService;
    private readonly string _visionModel;

    public AnswerWindow(OllamaService ollama, ImageService imageService, string visionModel)
    {
        InitializeComponent();
        _ollama = ollama;
        _imageService = imageService;
        _visionModel = visionModel;

        // 窗口加载时自动开始分析
        Loaded += AnswerWindow_Loaded;
    }

    private async void AnswerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await AnalyzeScreenAsync();
    }

    private async Task AnalyzeScreenAsync()
    {
        try
        {
            // 获取当前活动窗口的截图
            var activeWindowHandle = GetForegroundWindow();
            if (activeWindowHandle == IntPtr.Zero)
            {
                ShowError("无法获取当前活动窗口");
                return;
            }

            // 获取窗口位置和大小
            if (!GetWindowRect(activeWindowHandle, out RECT rect))
            {
                ShowError("无法获取窗口位置信息");
                return;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                ShowError("窗口尺寸无效");
                return;
            }

            // 创建位图并截取窗口
            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);

            graphics.CopyFromScreen(
                rect.Left,
                rect.Top,
                0,
                0,
                new System.Drawing.Size(width, height),
                CopyPixelOperation.SourceCopy);

            // 保存到临时文件以便调试
            string tempPath = Path.Combine(Path.GetTempPath(), $"screen_capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            bitmap.Save(tempPath);
            Debug.WriteLine($"[屏幕分析] 截图已保存到：{tempPath}, 尺寸：{width}x{height}");

            // 转换为 WPF 图像
            var imageSource = BitmapToImageSource(bitmap);

            // 转换为 Bitmap 用于 AI 识别
            using var aiBitmap = _imageService.ConvertImageSourceToBitmap(imageSource);
            if (aiBitmap == null)
            {
                ShowError("图像转换失败");
                return;
            }

            // 更新 UI 显示加载状态
            UpdateLoadingText("正在截取屏幕...");
            await Task.Delay(500);

            UpdateLoadingText("正在识别窗口内容...");
            await Task.Delay(500);

            UpdateLoadingText("正在分析用户可能遇到的问题...");

            // 使用 AI 视觉模型分析（屏幕问题分析场景）
            var visionResult = await _imageService.AnalyzeImageAsync(aiBitmap, scenario: ImageAnalysisScenario.ScreenProblem);

            if (string.IsNullOrWhiteSpace(visionResult))
            {
                ShowError($"AI 分析失败：{visionResult}");
            }
            else
            {
                // 显示分析结果
                ShowAnswer(visionResult);
            }
            // 清理临时文件
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[屏幕分析] 异常：{ex.Message}\n{ex.StackTrace}");
            ShowError($"分析过程中发生错误：{ex.Message}");
        }
    }

    private void UpdateLoadingText(string text)
    {
        Dispatcher.Invoke(() =>
        {
            if (LoadingText != null)
            {
                LoadingText.Text = text;
            }
        });
    }

    private void ShowAnswer(string answer)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            AnswerPanel.Visibility = Visibility.Visible;

            // 解析 AI 回答，分离问题识别和解答
            var lines = answer.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var questionBuilder = new StringBuilder();
            var answerBuilder = new StringBuilder();
            bool isInAnswerSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Contains("推测") || trimmedLine.Contains("可能") ||
                    trimmedLine.Contains("问题") || trimmedLine.Contains("想要"))
                {
                    isInAnswerSection = true;
                }

                if (isInAnswerSection)
                {
                    answerBuilder.AppendLine(trimmedLine);
                }
                else
                {
                    questionBuilder.AppendLine(trimmedLine);
                }
            }

            // 如果没有明显分段，前两段作为问题，其余作为解答
            if (answerBuilder.Length == 0)
            {
                var allLines = lines;
                for (int i = 0; i < allLines.Length; i++)
                {
                    if (i < 2)
                    {
                        questionBuilder.AppendLine(allLines[i].Trim());
                    }
                    else
                    {
                        answerBuilder.AppendLine(allLines[i].Trim());
                    }
                }
            }

            QuestionText.Text = questionBuilder.Length > 0 ? questionBuilder.ToString() : "正在分析中...";
            AnswerText.Text = answerBuilder.Length > 0 ? answerBuilder.ToString() : answer;
        });
    }

    private void ShowError(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = message;
        });
    }

    private System.Windows.Media.ImageSource BitmapToImageSource(Bitmap bitmap)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // P/Invoke  declarations
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, CopyPixelOperation dwRop);
}
