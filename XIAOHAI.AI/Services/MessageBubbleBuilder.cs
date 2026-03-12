using NPOI.XWPF.UserModel;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Drawing = System.Drawing;
using DrawingBrush = System.Drawing.Brush;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using IOPath = System.IO.Path;

namespace XIAOHAI.AI.Services;

public static class MessageBubbleBuilder
{
    private static readonly string AiAvatarPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ziyuan", "AIlogo.png");

    public static FrameworkElement CreateAvatar(bool isUser, string userAvatarPath, bool isThinking, ref Ellipse thinkingAvatar)
    {
        const int size = 36;
        var ellipse = new Ellipse { Width = size, Height = size };

        if (isUser)
        {
            SetupUserAvatar(ellipse, userAvatarPath);
        }
        else
        {
            SetupAiAvatar(ellipse, isThinking, ref thinkingAvatar);
        }

        return ellipse;
    }

    private static void SetupUserAvatar(Ellipse ellipse, string userAvatarPath)
    {
        if (!string.IsNullOrEmpty(userAvatarPath) && File.Exists(userAvatarPath))
        {
            try
            {
                var brush = new ImageBrush(new BitmapImage(new Uri(userAvatarPath))) { Stretch = Stretch.UniformToFill };
                ellipse.Fill = brush;
            }
            catch
            {
                SetDefaultUserAvatar(ellipse);
            }
        }
        else
        {
            SetDefaultUserAvatar(ellipse);
        }
    }

    private static void SetDefaultUserAvatar(Ellipse ellipse)
    {
        ellipse.Fill = new LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(99, 102, 241), System.Windows.Media.Color.FromRgb(139, 92, 246),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
    }

    private static void SetupAiAvatar(Ellipse ellipse, bool isThinking, ref Ellipse thinkingAvatar)
    {
        if (File.Exists(AiAvatarPath))
        {
            try
            {
                var brush = new ImageBrush(new BitmapImage(new Uri(AiAvatarPath))) { Stretch = Stretch.UniformToFill };
                ellipse.Fill = brush;
            }
            catch
            {
                ellipse.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
            }
        }
        else
        {
            ellipse.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
        }

        if (isThinking)
        {
            var glowEffect = new DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(99, 102, 241),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            ellipse.Effect = glowEffect;
            thinkingAvatar = ellipse;
        }
    }

    public static Border CreateMessageBubble(string role, string content, bool isPlaceholder, ref Ellipse thinkingAvatar, string userAvatarPath, ref StackPanel buttonPanel)
    {
        var isUser = role == "user";
        var bg = CreateBubbleBackground(isUser);
        var borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 99, 102, 241));

        UIElement inner = isPlaceholder
            ? CreatePlaceholderContent(content, ref buttonPanel)
            : CreateTextContent(content);

        var bubble = new Border
        {
            Background = bg,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = inner,
            MaxWidth = 480
        };

        return bubble;
    }

    private static System.Windows.Media.Brush CreateBubbleBackground(bool isUser)
    {
        return isUser
            ? new LinearGradientBrush(
                System.Windows.Media.Color.FromArgb(51, 99, 102, 241),
                System.Windows.Media.Color.FromArgb(38, 139, 92, 246),
                new System.Windows.Point(0, 1),
                new System.Windows.Point(1, 0))
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 24, 28, 40));
    }

    private static UIElement CreateTextContent(string content)
    {
        return new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            FontSize = 14,
            LineHeight = 22,
            Margin = new Thickness(12, 10, 12, 10)
        };
    }

    private static UIElement CreatePlaceholderContent(string content, ref StackPanel buttonPanel)
    {
        var thinkingExpander = new Expander
        {
            Header = "思考过程",
            IsExpanded = false,
            Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168)),
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
                FontSize = 13,
                LineHeight = 20
            }
        };

        var answerBlock = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            FontSize = 14,
            LineHeight = 22
        };

        buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
            Name = "ButtonPanel",
            Visibility = Visibility.Collapsed
        };

        var exportBtn = CreateExportButton(answerBlock);
        var copyBtn = CreateCopyButton(answerBlock);

        buttonPanel.Children.Add(exportBtn);
        buttonPanel.Children.Add(copyBtn);

        var sp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        sp.Children.Add(thinkingExpander);
        sp.Children.Add(answerBlock);
        sp.Children.Add(buttonPanel);

        return sp;
    }

    private static System.Windows.Controls.Button CreateCopyButton(TextBlock answerBlock)
    {
        var copyBtn = new System.Windows.Controls.Button
        {
            Content = "复制",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 99, 102, 241)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        copyBtn.Click += (s, _) =>
        {
            try
            {
                var textToCopy = answerBlock?.Text ?? "";
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    System.Windows.Clipboard.SetText(textToCopy);
                    (s as System.Windows.Controls.Button)!.Content = "已复制到剪贴板";
                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    t.Tick += (_, _) => { (s as System.Windows.Controls.Button)!.Content = "复制"; t.Stop(); };
                    t.Start();
                }
            }
            catch { }
        };

        return copyBtn;
    }

    private static System.Windows.Controls.Button CreateExportButton(TextBlock answerBlock)
    {
        var exportBtn = new System.Windows.Controls.Button
        {
            Content = "导出DOC",
            Margin = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 99, 102, 241)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        exportBtn.Click += (s, _) =>
        {
            try
            {
                var textToExport = answerBlock?.Text ?? "";
                if (string.IsNullOrEmpty(textToExport))
                    return;

                // 获取Windows桌面路径
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"AI回答_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                string filePath = IOPath.Combine(desktopPath, fileName);

                // 使用NPOI创建Word文档
                CreateWordDocument(filePath, textToExport);

                // 显示成功提示
                (s as System.Windows.Controls.Button)!.Content = "导出成功";
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) => { (s as System.Windows.Controls.Button)!.Content = "导出DOC"; t.Stop(); };
                t.Start();

                // 显示消息框提示用户
                System.Windows.MessageBox.Show($"文档已成功导出到桌面：{fileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        return exportBtn;
    }

    private static void CreateWordDocument(string filePath, string text)
    {
        // 创建Word文档
        using var document = new XWPFDocument();
        var paragraph = document.CreateParagraph();
        paragraph.SpacingAfter = 200; // 段后间距

        // 处理文本内容，按段落分割
        string[] paragraphs = text.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string paraText in paragraphs)
        {
            string trimmedText = paraText.Trim();
            if (!string.IsNullOrEmpty(trimmedText))
            {
                // 创建段落
                var run = paragraph.CreateRun();
                run.SetText(trimmedText);
                run.FontFamily = "宋体";
                run.FontSize = 12;
                run.IsBold = false;

                // 如果段落是标题（以数字和点开头，如"1."），设置为粗体
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmedText, @"^\d+\.\s+"))
                {
                    run.IsBold = true;
                    run.FontSize = 14;
                }

                // 添加段落
                paragraph = document.CreateParagraph();
                paragraph.SpacingAfter = 200; // 段后间距
            }
        }

        // 保存文档
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        document.Write(stream);
    }

    public static StackPanel CreateMessageRow(Border bubble, FrameworkElement avatar, bool isUser)
    {
        avatar.Margin = new Thickness(isUser ? 10 : 0, 0, isUser ? 0 : 10, 0);

        var row = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16),
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
        };

        if (isUser)
        {
            row.Children.Add(bubble);
            row.Children.Add(avatar);
        }
        else
        {
            row.Children.Add(avatar);
            row.Children.Add(bubble);
        }

        return row;
    }

    public static UIElement CreateSearchResultBubble(string content)
    {
        var thinkingExpander = new Expander
        {
            Header = "🔍 网络搜索结果",
            IsExpanded = false,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168)),
            Content = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
                FontSize = 13,
                LineHeight = 20
            }
        };

        var copyBtn = new System.Windows.Controls.Button
        {
            Content = "复制搜索结果",
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 99, 102, 241)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed
        };

        copyBtn.Click += (s, _) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(content);
                (s as System.Windows.Controls.Button)!.Content = "已复制";
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) => { (s as System.Windows.Controls.Button)!.Content = "复制搜索结果"; t.Stop(); };
                t.Start();
            }
            catch { }
        };

        var sp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        sp.Children.Add(thinkingExpander);
        sp.Children.Add(copyBtn);

        return sp;
    }

    public static UIElement CreateCommandResultBubble(string content)
    {
        var thinkingExpander = new Expander
        {
            Header = "⚙ 系统操作结果",
            IsExpanded = false,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168)),
            Content = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
                FontSize = 13,
                LineHeight = 20
            }
        };

        var copyBtn = new System.Windows.Controls.Button
        {
            Content = "复制操作结果",
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 99, 102, 241)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed
        };

        copyBtn.Click += (s, _) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(content);
                (s as System.Windows.Controls.Button)!.Content = "已复制";
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) => { (s as System.Windows.Controls.Button)!.Content = "复制操作结果"; t.Stop(); };
                t.Start();
            }
            catch { }
        };

        var sp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        sp.Children.Add(thinkingExpander);
        sp.Children.Add(copyBtn);

        return sp;
    }
}
