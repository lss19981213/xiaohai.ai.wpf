using System;
using System.Text;
using System.Windows;
using XIAOHAI.AI.Services;

namespace XIAOHAI.AI
{
    public partial class CompressionDialog : Window
    {
        private readonly KnowledgeEntry _entry;
        private readonly OllamaService _ollama;
        private readonly string _model;
        private int _selectedLevel = 1;
        private string _compressedContent = "";
        private bool _isCompressing = false;
        private bool _isInitialized = false;

        public string CompressedContent => _compressedContent;
        public int CompressionLevel => _selectedLevel;

        public CompressionDialog(KnowledgeEntry entry, OllamaService ollama, string model)
        {
            InitializeComponent();
            _entry = entry;
            _ollama = ollama;
            _model = model;

            OriginalLengthText.Text = $"{entry.Content.Length} 字符";
            PreviewText.Text = entry.Content.Length > 500
                ? entry.Content.Substring(0, 500) + "..."
                : entry.Content;

            UpdatePreviewDescription();
            _isInitialized = true;
        }

        private void CompressionLevel_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (Level1Radio.IsChecked == true) _selectedLevel = 1;
            else if (Level2Radio.IsChecked == true) _selectedLevel = 2;
            else if (Level3Radio.IsChecked == true) _selectedLevel = 3;

            UpdatePreviewDescription();
        }

        private void UpdatePreviewDescription()
        {
            if (PreviewTitleText == null) return;

            var description = _selectedLevel switch
            {
                1 => "轻度压缩：保留主要信息和关键细节，去除冗余表述",
                2 => "中度压缩：提取核心要点，保留关键数据和结论",
                3 => "高度压缩：仅保留摘要和最重要的信息点",
                _ => ""
            };
            PreviewTitleText.Text = $"压缩预览 - {description}";
        }

        private async void CompressBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) return;

            _isCompressing = true;
            CompressBtn.IsEnabled = false;
            CompressBtn.Content = "压缩中...";
            PreviewText.Text = "正在使用AI压缩内容，请稍候...";

            try
            {
                System.Diagnostics.Debug.WriteLine($"[压缩] 开始压缩，模型: {_model}");
                System.Diagnostics.Debug.WriteLine($"[压缩] Ollama URL: {_ollama.BaseUrl}");

                var prompt = BuildCompressionPrompt(_entry.Content, _selectedLevel);
                System.Diagnostics.Debug.WriteLine($"[压缩] Prompt长度: {prompt.Length}");

                var messages = new List<OllamaService.ChatMessage>
                {
                    new OllamaService.ChatMessage("user", prompt)
                };

                _compressedContent = await _ollama.ChatAsync(_model, messages);

                System.Diagnostics.Debug.WriteLine($"[压缩] 压缩结果长度: {_compressedContent?.Length ?? 0}");

                if (!string.IsNullOrEmpty(_compressedContent))
                {
                    CompressedLengthText.Text = $"{_compressedContent.Length} 字符";
                    var ratio = (1 - (double)_compressedContent.Length / _entry.Content.Length) * 100;
                    PreviewText.Text = $"压缩率: {ratio:F1}%\n\n{_compressedContent}";
                }
                else
                {
                    PreviewText.Text = "压缩失败：AI返回空结果";
                    _isCompressing = false;
                    CompressBtn.IsEnabled = true;
                    CompressBtn.Content = "压缩";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[压缩] 异常: {ex.Message}\n{ex.StackTrace}");
                PreviewText.Text = $"压缩失败: {ex.Message}";
                _isCompressing = false;
                CompressBtn.IsEnabled = true;
                CompressBtn.Content = "压缩";
            }
        }

        private string BuildCompressionPrompt(string content, int level)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是一个专业的内容压缩助手。请对以下内容进行压缩，要求：");
            sb.AppendLine();

            switch (level)
            {
                case 1:
                    sb.AppendLine("【轻度压缩】");
                    sb.AppendLine("1. 保留所有关键信息和重要细节");
                    sb.AppendLine("2. 去除冗余表述和重复内容");
                    sb.AppendLine("3. 简化句子结构，但保持完整语义");
                    sb.AppendLine("4. 保留所有数字、日期、专有名词");
                    sb.AppendLine("5. 目标压缩率：30%-40%");
                    break;
                case 2:
                    sb.AppendLine("【中度压缩】");
                    sb.AppendLine("1. 提取核心要点和关键信息");
                    sb.AppendLine("2. 保留重要数据和结论");
                    sb.AppendLine("3. 使用简洁的语言概括内容");
                    sb.AppendLine("4. 可以合并相似观点");
                    sb.AppendLine("5. 目标压缩率：50%-60%");
                    break;
                case 3:
                    sb.AppendLine("【高度压缩】");
                    sb.AppendLine("1. 仅保留摘要和最重要的信息点");
                    sb.AppendLine("2. 使用最简洁的表达方式");
                    sb.AppendLine("3. 只保留核心数据和关键结论");
                    sb.AppendLine("4. 可以使用列表形式呈现要点");
                    sb.AppendLine("5. 目标压缩率：70%-80%");
                    break;
            }

            sb.AppendLine();
            sb.AppendLine("请直接输出压缩后的内容，不要添加任何解释或说明。");
            sb.AppendLine();
            sb.AppendLine("=== 原始内容 ===");
            sb.AppendLine(content);

            return sb.ToString();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
