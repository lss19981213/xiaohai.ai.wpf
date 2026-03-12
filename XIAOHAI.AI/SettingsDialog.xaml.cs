using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XIAOHAI.AI.Services;

namespace XIAOHAI.AI;

public partial class SettingsDialog : Window
{
    private readonly OllamaService _ollama;
    private const string ConfigFileName = "settings.json";

    public SettingsDialog(OllamaService ollama)
    {
        InitializeComponent();
        _ollama = ollama;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // 先刷新模型列表
            await RefreshModelListAsync();

            // 然后加载保存的设置
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    OllamaUrlTextBox.Text = settings.OllamaUrl;
                    SelectModelInComboBox(settings.Model);
                    SelectVisionModelInComboBox(settings.VisionModel);

                    if (!string.IsNullOrEmpty(settings.BaiduApiKey))
                    {
                        BaiduApiKeyPasswordBox.Password = settings.BaiduApiKey;
                    }
                    if (!string.IsNullOrEmpty(settings.BaiduUserId))
                    {
                        BaiduUserIdTextBox.Text = settings.BaiduUserId;
                    }

                    if (!string.IsNullOrEmpty(settings.OpenClawUrl))
                    {
                        OpenClawUrlTextBox.Text = settings.OpenClawUrl;
                    }
                }
            }
            else
            {
                // 如果没有配置文件，使用默认值
                OllamaUrlTextBox.Text = "http://localhost:11434";
                SelectModelInComboBox("glm-4.7-flash");
                SelectVisionModelInComboBox("llava");
            }
        }
        catch
        {
            OllamaUrlTextBox.Text = "http://localhost:11434";
            SelectModelInComboBox("glm-4.7-flash");
            SelectVisionModelInComboBox("llava");
        }
    }

    private async Task RefreshModelListAsync()
    {
        try
        {
            var models = await _ollama.ListModelsAsync(OllamaUrlTextBox.Text.Trim());

            ModelComboBox.Items.Clear();
            VisionModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                ModelComboBox.Items.Add(new ComboBoxItem { Content = model });
                VisionModelComboBox.Items.Add(new ComboBoxItem { Content = model });
            }
        }
        catch
        {
            // 如果刷新失败，添加默认模型
            ModelComboBox.Items.Clear();
            VisionModelComboBox.Items.Clear();
            ModelComboBox.Items.Add(new ComboBoxItem { Content = "glm-4.7-flash" });
            VisionModelComboBox.Items.Add(new ComboBoxItem { Content = "llava" });
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new Settings
            {
                OllamaUrl = OllamaUrlTextBox.Text.Trim(),
                Model = ModelComboBox.Text,
                VisionModel = VisionModelComboBox.Text,
                BaiduApiKey = !string.IsNullOrEmpty(BaiduApiKeyPasswordBox.Password) ? BaiduApiKeyPasswordBox.Password : null,
                BaiduUserId = !string.IsNullOrEmpty(BaiduUserIdTextBox.Text) ? BaiduUserIdTextBox.Text.Trim() : null,
                OpenClawUrl = OpenClawUrlTextBox.Text.Trim()
            };

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectModelInComboBox(string model)
    {
        foreach (ComboBoxItem item in ModelComboBox.Items)
        {
            if (item.Content.ToString() == model)
            {
                item.IsSelected = true;
                break;
            }
        }
    }

    private void SelectVisionModelInComboBox(string model)
    {
        foreach (ComboBoxItem item in VisionModelComboBox.Items)
        {
            if (item.Content.ToString() == model)
            {
                item.IsSelected = true;
                break;
            }
        }
    }

    private async void RefreshModelsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshModelsBtn.IsEnabled = false;
            RefreshModelsBtn.Content = "刷新中...";

            var models = await _ollama.ListModelsAsync(OllamaUrlTextBox.Text.Trim());

            ModelComboBox.Items.Clear();
            VisionModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                ModelComboBox.Items.Add(new ComboBoxItem { Content = model });
                VisionModelComboBox.Items.Add(new ComboBoxItem { Content = model });
            }

            if (ModelComboBox.Items.Count > 0)
            {
                ((ComboBoxItem)ModelComboBox.Items[0]).IsSelected = true;
                ((ComboBoxItem)VisionModelComboBox.Items[0]).IsSelected = true;
            }

            System.Windows.MessageBox.Show($"找到 {models.Count} 个可用模型", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"获取模型列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshModelsBtn.IsEnabled = true;
            RefreshModelsBtn.Content = "🔄 刷新可用模型";
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OllamaUrlTextBox.Text))
        {
            System.Windows.MessageBox.Show("请输入 Ollama 服务器地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettings();
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void TestConnectionBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TestConnectionBtn.IsEnabled = false;
            TestConnectionBtn.Content = "测试中...";

            var testOllama = new OllamaService(OllamaUrlTextBox.Text.Trim());
            var isOnline = await testOllama.PingAsync();

            if (isOnline)
            {
                TestConnectionBtn.Content = "✅ 连接成功";
                TestConnectionBtn.Foreground = new SolidColorBrush(Colors.Green);
                System.Windows.MessageBox.Show("✅ 连接成功！\n\nOllama 服务已连接，请确保已安装所需模型。", "连接测试", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TestConnectionBtn.Content = "❌ 连接失败";
                TestConnectionBtn.Foreground = new SolidColorBrush(Colors.Red);
                System.Windows.MessageBox.Show("❌ 连接失败！\n\n请检查 Ollama 服务是否已启动，地址是否正确。", "连接测试", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            TestConnectionBtn.Content = "❌ 测试失败";
            TestConnectionBtn.Foreground = new SolidColorBrush(Colors.Red);
            System.Windows.MessageBox.Show($"测试失败： {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            await Task.Delay(2000);
            TestConnectionBtn.IsEnabled = true;
            TestConnectionBtn.Content = "🔌 测试连接";
            TestConnectionBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 240));
        }
    }
}