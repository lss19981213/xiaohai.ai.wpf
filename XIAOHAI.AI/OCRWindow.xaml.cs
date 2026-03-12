using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XIAOHAI.AI.Services;

namespace XIAOHAI.AI;

public class ExtractedField
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public class FormFieldInfo
{
    public int Index { get; set; }
    public string Label { get; set; } = "";
    public string FieldType { get; set; } = "text";
    public bool IsRequired { get; set; }
    public string Name { get; set; } = "";
    public string Id { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public string Selector { get; set; } = "";
    public string InputId { get; set; } = "";
}

public class FormFieldMatch
{
    public int FieldIndex { get; set; }
    public string FieldLabel { get; set; } = "";
    public string Value { get; set; } = "";
}

public partial class OCRWindow : Window
{
    private string? _selectedImagePath;
    private List<ExtractedField> _extractedFields = new();
    private List<FormFieldInfo> _formFieldInfos = new();
    private List<FormFieldMatch> _fieldMatches = new();
    private readonly DispatcherTimer _logTimer;
    private string _logBuffer = "";
    private bool _isProcessing = false;
    private bool _websiteLoaded = false;
    private bool _imageSelected = false;
    private double _imageZoom = 1.0;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 3.0;

    private string _ollamaUrl = "http://localhost:11434";
    private string _visionModel = "llava";
    private string _chatModel = "glm-4.7-flash";

    private readonly OllamaService _ollama;
    private readonly ImageService _imageService;

    private readonly SolidColorBrush _stepIncomplete = new SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 65, 81));
    private readonly SolidColorBrush _stepComplete = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
    private readonly SolidColorBrush _stepActive = new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));

    public OCRWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;

        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _logTimer.Tick += LogTimer_Tick;
        _logTimer.Start();

        _ollama = new OllamaService();
        LoadSettings();
        _ollama.UpdateBaseUrl(_ollamaUrl);
        _imageService = new ImageService(_ollama, _visionModel);
    }

    private void LoadSettings()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    _ollamaUrl = settings.OllamaUrl ?? "http://localhost:11434";
                    _visionModel = settings.VisionModel ?? "llava";
                    _chatModel = settings.Model ?? "glm-4.7-flash";
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"加载设置失败：{ex.Message}");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppendLog("视觉识别与自动填表工具已启动");
        AppendLog($"视觉模型: {_visionModel}");
        AppendLog($"对话模型: {_chatModel}");
        AppendLog("请按步骤操作：①打开网站 → ②上传图片 → ③智能识别 → ④确认填写");

        try
        {
            await WebView.EnsureCoreWebView2Async();
            AppendLog("WebView2 初始化完成");
        }
        catch (Exception ex)
        {
            AppendLog($"WebView2 初始化失败: {ex.Message}");
        }
    }

    private void LogTimer_Tick(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_logBuffer))
        {
            LogTextBox.AppendText(_logBuffer);
            LogTextBox.ScrollToEnd();
            _logBuffer = "";
        }
    }

    private void AppendLog(string message)
    {
        _logBuffer += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private void UpdateStepIndicator(int step, bool completed)
    {
        var border = step switch
        {
            1 => Step1Indicator,
            2 => Step2Indicator,
            3 => Step3Indicator,
            4 => Step4Indicator,
            _ => null
        };

        if (border != null)
        {
            border.Background = completed ? _stepComplete : _stepIncomplete;
            if (border.Child is TextBlock textBlock)
            {
                textBlock.Foreground = completed
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 143, 168));
            }
        }
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateProgressDetail(string message)
    {
        ProgressDetailText.Text = message;
    }

    private void ShowProgress(bool show)
    {
        ProgressIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ProgressDetailPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadImagePreview(string imagePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath);
            bitmap.EndInit();
            bitmap.Freeze();

            ImagePreview.Source = bitmap;
            _imageZoom = 1.0;
            ApplyImageZoom();
            
            AppendLog($"图片预览已加载: {Path.GetFileName(imagePath)}");
        }
        catch (Exception ex)
        {
            AppendLog($"加载图片预览失败: {ex.Message}");
        }
    }

    private void ApplyImageZoom()
    {
        if (ImagePreview.Source != null)
        {
            var scaleTransform = new ScaleTransform(_imageZoom, _imageZoom);
            ImagePreview.RenderTransform = scaleTransform;
            ImagePreview.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        }
    }

    private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_imageZoom < MaxZoom)
        {
            _imageZoom += 0.25;
            ApplyImageZoom();
            AppendLog($"图片缩放: {_imageZoom * 100:F0}%");
        }
    }

    private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_imageZoom > MinZoom)
        {
            _imageZoom -= 0.25;
            ApplyImageZoom();
            AppendLog($"图片缩放: {_imageZoom * 100:F0}%");
        }
    }

    private void ResetZoomBtn_Click(object sender, RoutedEventArgs e)
    {
        _imageZoom = 1.0;
        ApplyImageZoom();
        AppendLog("图片缩放已重置");
    }

    private void ClearResultsBtn_Click(object sender, RoutedEventArgs e)
    {
        _extractedFields.Clear();
        _fieldMatches.Clear();
        ExtractedFieldsList.ItemsSource = null;
        AppendLog("已清空识别结果");
    }

    private void ShowError(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        AppendLog($"❌ {title}: {message}");
    }

    private void ShowWarning(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        AppendLog($"⚠️ {title}: {message}");
    }

    private void ShowInfo(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        AppendLog($"ℹ️ {title}: {message}");
    }

    private void CheckCanRecognize()
    {
        RecognizeAndFillBtn.IsEnabled = _websiteLoaded && _imageSelected;
    }

    private void UrlTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            GoBtn_Click(sender, e);
        }
    }

    private async void GoBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing) return;

        var url = UrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url) || url == "https://")
        {
            AppendLog("请输入有效的网址");
            return;
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
            UrlTextBox.Text = url;
        }

        _isProcessing = true;
        GoBtn.IsEnabled = false;

        try
        {
            AppendLog($"正在打开: {url}");
            WebViewStatusText.Text = "加载中...";

            WebView.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            AppendLog($"打开网址失败: {ex.Message}");
            WebViewStatusText.Text = "加载失败";
        }
        finally
        {
            _isProcessing = false;
            GoBtn.IsEnabled = true;
        }
    }

    private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _websiteLoaded = true;
            UpdateStepIndicator(1, true);
            UpdateStatus("网站已加载，请上传图片");
            WebViewStatusText.Text = "✓ 已加载";
            AppendLog($"网页加载完成: {WebView.Source}");
            CheckCanRecognize();
        }
        else
        {
            AppendLog($"网页加载失败");
            WebViewStatusText.Text = "加载失败";
        }
    }

    private void SelectImageBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing)
        {
            ShowWarning("处理中", "正在处理中，请稍候...");
            return;
        }

        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif|所有文件|*.*",
                Title = "选择要识别的图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedImagePath = openFileDialog.FileName;
                var fileName = Path.GetFileName(_selectedImagePath);
                ImageStatusText.Text = fileName;
                _imageSelected = true;
                UpdateStepIndicator(2, true);
                UpdateStatus("图片已选择，点击「智能识别」开始");
                AppendLog($"已选择图片: {fileName}");
                
                LoadImagePreview(_selectedImagePath);
                CheckCanRecognize();
            }
        }
        catch (Exception ex)
        {
            ShowError("选择图片失败", ex.Message);
        }
    }

    private async void RecognizeAndFillBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing)
        {
            ShowWarning("处理中", "正在处理中，请稍候...");
            return;
        }

        if (!_websiteLoaded)
        {
            ShowWarning("未打开网站", "请先打开需要填写的网站");
            return;
        }

        if (string.IsNullOrEmpty(_selectedImagePath))
        {
            ShowWarning("未选择图片", "请先选择要识别的图片");
            return;
        }

        _isProcessing = true;
        RecognizeAndFillBtn.IsEnabled = false;
        ConfirmFillBtn.Visibility = Visibility.Collapsed;
        ConfirmFillBtn.IsEnabled = false;
        ShowProgress(true);
        UpdateProgressDetail("准备开始识别...");
        UpdateStepIndicator(3, false);
        Step3Indicator.Background = _stepActive;

        try
        {
            AppendLog("========== 开始智能识别 ==========");

            UpdateProgressDetail("步骤 1/3: 正在识别图片内容...");
            await Task.Delay(100);
            
            AppendLog("步骤1: OCR识别图片内容...");
            var extractedFields = await RecognizeImageAsync();
            if (extractedFields.Count == 0)
            {
                ShowError("识别失败", "图片识别失败或未提取到有效字段，请检查图片质量或更换其他图片");
                return;
            }
            _extractedFields = extractedFields;
            ExtractedFieldsList.ItemsSource = _extractedFields;
            AppendLog($"从图片中提取了 {_extractedFields.Count} 个字段");
            UpdateProgressDetail($"步骤 1/3: 已识别 {_extractedFields.Count} 个字段");

            UpdateProgressDetail("步骤 2/3: 正在分析网页表单元素...");
            await Task.Delay(100);
            
            AppendLog("步骤2: 分析网页表单元素...");
            var formFields = await FindFormFieldsAsync();
            if (formFields.Count == 0)
            {
                ShowWarning("未检测到表单", "当前页面未检测到表单字段，请确保页面已完全加载");
                return;
            }
            _formFieldInfos = formFields;
            AppendLog($"检测到 {_formFieldInfos.Count} 个表单字段");
            foreach (var field in _formFieldInfos)
            {
                AppendLog($"  - {field.Label} ({field.FieldType}) {(field.IsRequired ? "[必填]" : "")}");
            }
            UpdateProgressDetail($"步骤 2/3: 已检测到 {_formFieldInfos.Count} 个表单字段");

            UpdateProgressDetail("步骤 3/3: 正在进行 AI 智能匹配...");
            await Task.Delay(100);
            
            AppendLog("步骤3: AI智能匹配字段...");
            var matches = await MatchFieldsAsync();
            if (matches.Count == 0)
            {
                ShowWarning("匹配失败", "未能匹配任何字段，请检查图片内容是否与表单字段对应");
                return;
            }
            _fieldMatches = matches;

            AppendLog($"========== 识别完成 ==========");
            AppendLog($"已匹配 {_fieldMatches.Count} 个字段，请确认后点击「确认填写」");

            foreach (var match in _fieldMatches)
            {
                AppendLog($"  📌 {match.FieldLabel} → {match.Value}");
            }

            UpdateStepIndicator(3, true);
            UpdateStatus($"识别完成！匹配 {_fieldMatches.Count} 个字段，请确认后填写");
            UpdateProgressDetail($"识别完成！已匹配 {_fieldMatches.Count} 个字段");

            RecognizeAndFillBtn.Visibility = Visibility.Collapsed;
            ConfirmFillBtn.Visibility = Visibility.Visible;
            ConfirmFillBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowError("处理失败", $"处理过程中发生错误：{ex.Message}\n\n请检查网络连接和 Ollama 服务状态");
            UpdateStepIndicator(3, false);
            UpdateProgressDetail("处理失败，请重试");
        }
        finally
        {
            _isProcessing = false;
            RecognizeAndFillBtn.IsEnabled = true;
            ShowProgress(false);
            CheckCanRecognize();
        }
    }

    private async Task<List<FormFieldInfo>> FindFormFieldsAsync()
    {
        var fields = new List<FormFieldInfo>();

        try
        {
            var script = @"
(function() {
    var fields = [];
    var inputs = document.querySelectorAll('input, textarea, select');
    var fieldIndex = 0;
    
    function isVisible(element) {
        if (!element) return false;
        var style = window.getComputedStyle(element);
        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') {
            return false;
        }
        var rect = element.getBoundingClientRect();
        if (rect.width === 0 && rect.height === 0) return false;
        return true;
    }
    
    function findLabelInContainer(container, input) {
        if (!container) return '';
        
        var labelElements = container.querySelectorAll('label, .label, .field-label, .form-label, .caption, .title');
        for (var i = 0; i < labelElements.length; i++) {
            var el = labelElements[i];
            if (el.contains(input)) continue;
            var text = el.textContent.trim();
            if (text && text.length > 0 && text.length < 50) {
                return text;
            }
        }
        
        var spans = container.querySelectorAll('span, div');
        for (var i = 0; i < spans.length; i++) {
            var el = spans[i];
            if (el.contains(input)) continue;
            var text = el.textContent.trim();
            if (text && text.length > 0 && text.length < 30) {
                var childInputs = el.querySelectorAll('input, textarea, select');
                if (childInputs.length === 0) {
                    return text;
                }
            }
        }
        
        return '';
    }
    
    function getLabelFromDOMStructure(input) {
        var parent = input.parentElement;
        var visited = new Set();
        
        for (var depth = 0; depth < 8 && parent; depth++) {
            if (visited.has(parent)) break;
            visited.add(parent);
            
            var prevSibling = parent.previousElementSibling;
            while (prevSibling && !visited.has(prevSibling)) {
                var text = prevSibling.textContent.trim();
                if (text && text.length > 0 && text.length < 50) {
                    var innerInputs = prevSibling.querySelectorAll('input, textarea, select');
                    if (innerInputs.length === 0) {
                        return text.substring(0, 50);
                    }
                }
                prevSibling = prevSibling.previousElementSibling;
            }
            
            var label = findLabelInContainer(parent, input);
            if (label) return label;
            
            parent = parent.parentElement;
        }
        
        return '';
    }
    
    function getLabelFromTable(input) {
        var td = input.closest('td');
        if (!td) return '';
        
        var tr = td.closest('tr');
        if (!tr) return '';
        
        var allTds = tr.querySelectorAll('td');
        var myTdIndex = -1;
        for (var i = 0; i < allTds.length; i++) {
            if (allTds[i].contains(input)) {
                myTdIndex = i;
                break;
            }
        }
        
        if (myTdIndex > 0) {
            var prevTd = allTds[myTdIndex - 1];
            var text = prevTd.textContent.trim();
            if (text && text.length < 50) {
                return text;
            }
        }
        
        return '';
    }
    
    function generateSelector(input) {
        if (input.id) {
            return '#' + CSS.escape(input.id);
        }
        
        if (input.name) {
            var byName = document.querySelectorAll('[name=""' + input.name + '""]');
            if (byName.length === 1) {
                return '[name=""' + input.name + '""]';
            }
        }
        
        var path = [];
        var el = input;
        
        while (el && el.nodeType === Node.ELEMENT_NODE) {
            var tagName = el.tagName.toLowerCase();
            var selector = tagName;
            
            if (el.id) {
                selector = '#' + CSS.escape(el.id);
                path.unshift(selector);
                break;
            }
            
            if (el.className && typeof el.className === 'string') {
                var classes = el.className.trim().split(/\s+/).filter(function(c) {
                    return c && !c.match(/^[0-9]/) && c.length < 30;
                });
                if (classes.length > 0) {
                    selector = tagName + '.' + classes.slice(0, 2).join('.');
                }
            }
            
            path.unshift(selector);
            el = el.parentElement;
            
            if (path.length > 5) break;
        }
        
        return path.join(' > ');
    }
    
    inputs.forEach(function(input) {
        var type = input.type ? input.type.toLowerCase() : 'text';
        
        if (type === 'hidden' || type === 'submit' || type === 'button' || type === 'reset' || type === 'image' || type === 'file')
            return;
        
        if (!isVisible(input)) return;
        
        var label = '';
        
        if (input.id) {
            var labelEl = document.querySelector('label[for=""' + input.id + '""]');
            if (labelEl) label = labelEl.textContent.trim();
        }
        
        if (!label) label = getLabelFromTable(input);
        if (!label) label = getLabelFromDOMStructure(input);
        
        if (!label && input.placeholder) label = input.placeholder;
        if (!label && input.getAttribute('aria-label')) label = input.getAttribute('aria-label');
        if (!label && input.title) label = input.title;
        if (!label && input.name) label = input.name;
        
        label = label.replace(/[：:*\s]+$/g, '').replace(/^[：:*\s]+/g, '').substring(0, 50);
        
        if (!label) label = '字段' + (fieldIndex + 1);
        
        var selector = generateSelector(input);
        
        fields.push({
            index: fieldIndex++,
            label: label,
            fieldType: type,
            isRequired: input.required || false,
            name: input.name || '',
            id: input.id || '',
            placeholder: input.placeholder || '',
            selector: selector,
            inputId: input.id || ''
        });
    });
    
    return JSON.stringify(fields);
})();";

            var result = await WebView.ExecuteScriptAsync(script);

            if (result.StartsWith("\"") && result.EndsWith("\""))
            {
                result = JsonSerializer.Deserialize<string>(result) ?? "[]";
            }

            var jsonFields = JsonSerializer.Deserialize<List<FormFieldInfo>>(result);
            if (jsonFields != null)
            {
                fields = jsonFields;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"获取表单字段失败: {ex.Message}");
        }

        return fields;
    }

    private async void ConfirmFillBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing) return;
        if (_fieldMatches.Count == 0)
        {
            ShowWarning("无匹配结果", "没有可填写的匹配结果");
            return;
        }

        _isProcessing = true;
        ConfirmFillBtn.IsEnabled = false;
        ShowProgress(true);
        Step4Indicator.Background = _stepActive;

        try
        {
            AppendLog("========== 开始填写 ==========");

            int filledCount = 0;
            int totalFields = _fieldMatches.Count;

            for (int i = 0; i < _fieldMatches.Count; i++)
            {
                var match = _fieldMatches[i];
                if (match.FieldIndex < 0 || match.FieldIndex >= _formFieldInfos.Count)
                    continue;

                var field = _formFieldInfos[match.FieldIndex];
                UpdateProgressDetail($"正在填写 {i + 1}/{totalFields}: {field.Label}");
                AppendLog($"  填充: {field.Label} = {match.Value}");

                var success = await FillFieldAsync(field, match.Value);
                if (success)
                {
                    filledCount++;
                    AppendLog($"    ✓ 已填充");
                }
                else
                {
                    AppendLog($"    ✗ 填充失败");
                }

                await Task.Delay(100);
            }

            UpdateStepIndicator(4, true);
            UpdateStatus($"填写完成！已填充 {filledCount} 个字段，请确认后手动提交");
            UpdateProgressDetail($"填写完成！已填充 {filledCount}/{totalFields} 个字段");
            AppendLog($"========== 填写完成 ==========");
            AppendLog($"已成功填充 {filledCount} 个字段");
            AppendLog("请检查填写内容，确认无误后手动提交表单");
            
            ShowInfo("填写完成", $"已成功填充 {filledCount} 个字段，请检查内容后手动提交表单");
        }
        catch (Exception ex)
        {
            ShowError("填写失败", $"填写过程中发生错误：{ex.Message}");
            UpdateProgressDetail("填写失败，请重试");
        }
        finally
        {
            _isProcessing = false;
            ConfirmFillBtn.Visibility = Visibility.Collapsed;
            RecognizeAndFillBtn.Visibility = Visibility.Visible;
            RecognizeAndFillBtn.IsEnabled = true;
            ShowProgress(false);
            CheckCanRecognize();
        }
    }

    private async Task<bool> FillFieldAsync(FormFieldInfo field, string value)
    {
        try
        {
            var escapedValue = EscapeJsString(value);
            var escapedSelector = EscapeJsString(field.Selector);
            var escapedInputId = EscapeJsString(field.InputId);

            string script;

            if (!string.IsNullOrEmpty(escapedInputId))
            {
                script = $@"
(function() {{
    var input = document.getElementById('{escapedInputId}');
    if (!input) {{
        input = document.querySelector('{escapedSelector}');
    }}
    if (!input) return false;
    
    input.focus();
    input.value = '{escapedValue}';
    input.dispatchEvent(new Event('input', {{ bubbles: true }}));
    input.dispatchEvent(new Event('change', {{ bubbles: true }}));
    input.blur();
    return true;
}})();";
            }
            else if (field.FieldType == "select" || field.FieldType == "select-one")
            {
                script = $@"
(function() {{
    var select = document.querySelector('{escapedSelector}');
    if (!select) return false;
    
    var options = select.options;
    for (var i = 0; i < options.length; i++) {{
        if (options[i].text.includes('{escapedValue}') || options[i].value.includes('{escapedValue}')) {{
            select.selectedIndex = i;
            select.dispatchEvent(new Event('change', {{ bubbles: true }}));
            return true;
        }}
    }}
    
    for (var i = 0; i < options.length; i++) {{
        if ('{escapedValue}'.includes(options[i].text) || '{escapedValue}'.includes(options[i].value)) {{
            select.selectedIndex = i;
            select.dispatchEvent(new Event('change', {{ bubbles: true }}));
            return true;
        }}
    }}
    
    return false;
}})();";
            }
            else if (field.FieldType == "checkbox")
            {
                var shouldCheck = value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                  value.Equals("是", StringComparison.OrdinalIgnoreCase) ||
                                  value.Equals("1", StringComparison.OrdinalIgnoreCase);
                var checkValue = shouldCheck ? "true" : "false";

                script = $@"
(function() {{
    var checkbox = document.querySelector('{escapedSelector}');
    if (!checkbox) return false;
    
    var shouldCheck = {checkValue};
    if (checkbox.checked !== shouldCheck) {{
        checkbox.click();
        checkbox.dispatchEvent(new Event('change', {{ bubbles: true }}));
    }}
    return true;
}})();";
            }
            else if (field.FieldType == "radio")
            {
                script = $@"
(function() {{
    var radios = document.querySelectorAll('input[type=""radio""][name=""{escapedSelector}""]');
    for (var i = 0; i < radios.length; i++) {{
        if (radios[i].value.includes('{escapedValue}') || radios[i].nextSibling.textContent.includes('{escapedValue}')) {{
            radios[i].click();
            radios[i].dispatchEvent(new Event('change', {{ bubbles: true }}));
            return true;
        }}
    }}
    return false;
}})();";
            }
            else
            {
                script = $@"
(function() {{
    var input = document.querySelector('{escapedSelector}');
    if (!input) return false;
    
    input.focus();
    input.value = '{escapedValue}';
    input.dispatchEvent(new Event('input', {{ bubbles: true }}));
    input.dispatchEvent(new Event('change', {{ bubbles: true }}));
    input.blur();
    return true;
}})();";
            }

            var result = await WebView.ExecuteScriptAsync(script);
            return result == "true";
        }
        catch (Exception ex)
        {
            AppendLog($"    填充出错: {ex.Message}");
            return false;
        }
    }

    private string EscapeJsString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private async Task<List<ExtractedField>> RecognizeImageAsync()
    {
        var fields = new List<ExtractedField>();

        try
        {
            if (!File.Exists(_selectedImagePath))
            {
                throw new FileNotFoundException("图片文件不存在");
            }

            using var bitmap = new Bitmap(_selectedImagePath);

            // 使用 OCR 字段识别场景
            var rawResult = await _imageService.AnalyzeImageAsync(bitmap, scenario: ImageAnalysisScenario.OCRFieldRecognition);

            if (string.IsNullOrEmpty(rawResult) ||
                rawResult.Contains("无法连接") ||
                rawResult.Contains("错误") ||
                rawResult.Contains("失败"))
            {
                AppendLog($"视觉模型识别失败: {rawResult}");
                return fields;
            }

            AppendLog($"识别结果: {rawResult.Substring(0, Math.Min(200, rawResult.Length))}...");

            var cleanResult = rawResult;
            cleanResult = Regex.Replace(cleanResult, @"```json\s*", "");
            cleanResult = Regex.Replace(cleanResult, @"```\s*", "");
            cleanResult = cleanResult.Trim();

            var jsonMatch = Regex.Match(cleanResult, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var jsonStr = jsonMatch.Value;
                var jsonObj = JsonSerializer.Deserialize<JsonElement>(jsonStr);

                if (jsonObj.TryGetProperty("fields", out var fieldsArray))
                {
                    foreach (var field in fieldsArray.EnumerateArray())
                    {
                        var label = field.GetProperty("label").GetString() ?? "";
                        var value = field.GetProperty("value").GetString() ?? "";
                        if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(value))
                        {
                            fields.Add(new ExtractedField { Label = label, Value = value });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"图片识别出错: {ex.Message}");
        }

        return fields;
    }

    private async Task<List<FormFieldMatch>> MatchFieldsAsync()
    {
        var matches = new List<FormFieldMatch>();

        try
        {
            var matchPrompt = "请根据网页表单字段和从图片提取的数据进行匹配。\n\n";
            matchPrompt += "【网页表单字段】：\n";
            for (int i = 0; i < _formFieldInfos.Count; i++)
            {
                var field = _formFieldInfos[i];
                matchPrompt += $"[{i}] {field.Label} ({field.FieldType}) {(field.IsRequired ? "[必填]" : "")}";
                if (!string.IsNullOrEmpty(field.Placeholder))
                    matchPrompt += $" placeholder: {field.Placeholder}";
                matchPrompt += "\n";
            }

            matchPrompt += "\n【从图片提取的数据】：\n";
            for (int i = 0; i < _extractedFields.Count; i++)
            {
                matchPrompt += $"- {_extractedFields[i].Label}: {_extractedFields[i].Value}\n";
            }

            matchPrompt += "\n【匹配规则】：\n";
            matchPrompt += "1. 根据字段名称的语义相似性进行匹配\n";
            matchPrompt += "2. 只输出有匹配关系的字段\n";
            matchPrompt += "3. 输出格式：{\"matches\":[{\"field_index\":0,\"value\":\"实际值\"}]}\n";

            var messages = new List<OllamaService.ChatMessage>
            {
                new OllamaService.ChatMessage("user", matchPrompt)
            };

            var matchResult = await _ollama.ChatAsync(_chatModel, messages);

            if (string.IsNullOrEmpty(matchResult))
            {
                AppendLog("AI匹配返回空结果");
                return matches;
            }

            var cleanResult = Regex.Replace(matchResult, @"```json\s*", "");
            cleanResult = Regex.Replace(cleanResult, @"```\s*", "");
            cleanResult = cleanResult.Trim();

            var jsonMatch = Regex.Match(cleanResult, @"\{[\s\S]*\}");
            if (!jsonMatch.Success)
            {
                AppendLog("未找到匹配结果JSON");
                return matches;
            }

            var jsonObj = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
            if (!jsonObj.TryGetProperty("matches", out var matchesArray))
            {
                return matches;
            }

            foreach (var match in matchesArray.EnumerateArray())
            {
                var fieldIndex = match.GetProperty("field_index").GetInt32();
                var value = match.GetProperty("value").GetString() ?? "";

                if (fieldIndex < 0 || fieldIndex >= _formFieldInfos.Count)
                    continue;

                var field = _formFieldInfos[fieldIndex];
                matches.Add(new FormFieldMatch
                {
                    FieldIndex = fieldIndex,
                    FieldLabel = field.Label,
                    Value = value
                });
            }
        }
        catch (Exception ex)
        {
            AppendLog($"字段匹配出错: {ex.Message}");
        }

        return matches;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _logTimer?.Stop();
    }
}
