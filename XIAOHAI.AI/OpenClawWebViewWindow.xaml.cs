using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using IOPath = System.IO.Path;
using WpfColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;

namespace XIAOHAI.AI;

public partial class OpenClawWebViewWindow : Window
{
    private readonly string _openClawUrl;

    public OpenClawWebViewWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        _openClawUrl = LoadOpenClawUrl();
    }

    private string LoadOpenClawUrl()
    {
        try
        {
            var configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null && !string.IsNullOrEmpty(settings.OpenClawUrl))
                {
                    return settings.OpenClawUrl;
                }
            }
        }
        catch { }

        return "http://127.0.0.1:18789";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async(null);
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
            
            WebView.Source = new Uri(_openClawUrl);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"初始化WebView2失败: {ex.Message}\n\n请确保已安装Microsoft Edge WebView2运行时。\n下载地址: https://go.microsoft.com/fwlink/p/?LinkId=2124703", 
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = "正在加载...";
            StatusDot.Fill = new SolidColorBrush(WpfColor.FromRgb(251, 191, 36));
            UrlText.Text = e.Uri;
        });
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = e.IsSuccess ? "已连接" : "加载失败";
            StatusDot.Fill = new SolidColorBrush(e.IsSuccess ? WpfColor.FromRgb(34, 197, 94) : WpfColor.FromRgb(239, 68, 68));
            
            UpdateNavigationButtons();
        });
    }

    private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UrlText.Text = WebView.Source?.ToString() ?? "";
            UpdateNavigationButtons();
        });
    }

    private void UpdateNavigationButtons()
    {
        if (WebView.CoreWebView2 != null)
        {
            BackBtn.IsEnabled = WebView.CoreWebView2.CanGoBack;
            ForwardBtn.IsEnabled = WebView.CoreWebView2.CanGoForward;
        }
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoBack == true)
        {
            WebView.CoreWebView2.GoBack();
        }
    }

    private void ForwardBtn_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoForward == true)
        {
            WebView.CoreWebView2.GoForward();
        }
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        WebView.Reload();
    }

    private void HomeBtn_Click(object sender, RoutedEventArgs e)
    {
        WebView.Source = new Uri(_openClawUrl);
    }

    private void OpenInBrowserBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentUrl = WebView.Source?.ToString();
            if (!string.IsNullOrEmpty(currentUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = currentUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"在浏览器中打开失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        WebView?.Dispose();
    }
}
