using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using XIAOHAI.AI.Services;
using WinForms = System.Windows.Forms;
using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace XIAOHAI.AI;

public partial class UpdateWindow : Window
{
    private readonly UpdateService _updateService;
    private UpdateCheckResult? _updateResult;
    private bool _isDownloading = false;
    private readonly string _downloadFolder;

    public UpdateWindow(UpdateService updateService)
    {
        InitializeComponent();
        _updateService = updateService;
        _downloadFolder = Path.Combine(Path.GetTempPath(), "XIAOHAI.AI.Updates");

        // 确保下载目录存在
        if (!Directory.Exists(_downloadFolder))
        {
            Directory.CreateDirectory(_downloadFolder);
        }

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        UpdateBtn.IsEnabled = false;
        SubtitleText.Text = "正在检查更新...";

        _updateResult = await _updateService.CheckForUpdatesAsync();

        if (!string.IsNullOrEmpty(_updateResult.Error))
        {
            SubtitleText.Text = "检查失败";
            ReleaseNotesText.Text = _updateResult.Error;
            return;
        }

        CurrentVersionText.Text = _updateResult.CurrentVersion;

        if (_updateResult.HasUpdate)
        {
            // 有新版本
            TitleText.Text = "📦 发现新版本";
            TitleText.Foreground = FindResource("Success") as System.Windows.Media.SolidColorBrush;
            SubtitleText.Text = "发现新版本，是否立即更新？";
            LatestVersionText.Text = "v" + _updateResult.LatestVersion;
            ReleaseDateText.Text = "发布日期：" + FormatDate(_updateResult.ReleaseDate);
            ReleaseNotesText.Text = _updateResult.ReleaseNotes ?? "暂无详细说明";
            UpdateBtn.IsEnabled = true;
            OpenUrlBtn.Visibility = Visibility.Visible;
        }
        else
        {
            // 已是最新版本
            TitleText.Text = "✅ 已是最新版本";
            TitleText.Foreground = FindResource("Success") as System.Windows.Media.SolidColorBrush;
            SubtitleText.Text = "当前已是最新版本，无需更新";
            LatestVersionText.Text = "v" + _updateResult.CurrentVersion;
            ReleaseDateText.Text = "";
            ReleaseNotesText.Text = "🎉 您正在使用最新版本，享受最新功能！";
            UpdateBtn.Visibility = Visibility.Collapsed;
            OpenUrlBtn.Visibility = Visibility.Collapsed;
        }
    }

    private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading || _updateResult == null || string.IsNullOrEmpty(_updateResult.DownloadUrl))
            return;

        _isDownloading = true;
        UpdateBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        DownloadProgressCard.Visibility = Visibility.Visible;

        var progress = new Progress<int>(percent =>
        {
            DownloadProgressBar.Value = percent;
            DownloadProgressText.Text = percent + "%";
            DownloadStatusText.Text = "正在下载...";
        });

        var fileName = $"XIAOHAI.AI_{_updateResult.LatestVersion}.exe";
        var savePath = Path.Combine(_downloadFolder, fileName);

        DownloadStatusText.Text = "正在连接下载服务器...";

        var result = await _updateService.DownloadUpdateAsync(_updateResult.DownloadUrl, savePath, progress);

        if (result.Success)
        {
            DownloadStatusText.Text = "✅ 下载完成，准备安装...";
            DownloadProgressText.Text = "完成";

            // 延迟一下然后启动安装程序
            await Task.Delay(1000);
            StartInstaller(savePath);
        }
        else
        {
            DownloadStatusText.Text = "❌ " + result.Message;
            UpdateBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            _isDownloading = false;
        }
    }

    private void StartInstaller(string installerPath)
    {
        try
        {
            // 启动安装程序
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);

            // 关闭当前应用
            WpfApp.Current.Shutdown();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show("启动安装程序失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            _isDownloading = false;
        }
    }

    private void OpenUrlBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_updateResult != null && !string.IsNullOrEmpty(_updateResult.DownloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateResult.DownloadUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show("打开浏览器失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private string FormatDate(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return "未知";

        try
        {
            var date = DateTime.Parse(dateString);
            return date.ToString("yyyy 年 MM 月 dd 日");
        }
        catch
        {
            return dateString;
        }
    }
}
