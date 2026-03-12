using Microsoft.Win32;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UglyToad.PdfPig;
using XIAOHAI.AI.Services;
using WinForms = System.Windows.Forms;

namespace XIAOHAI.AI
{
    public partial class KnowledgeBaseWindow : Window
    {
        private ObservableCollection<KnowledgeEntryViewModel> _knowledgeEntries;
        private readonly VectorDatabaseService _vectorDb;
        private readonly OllamaService _ollama;
        private readonly string _embeddingModel = "nomic-embed-text";
        private bool _isProcessing = false;

        public KnowledgeBaseWindow()
        {
            InitializeComponent();
            _ollama = new OllamaService();
            _vectorDb = new VectorDatabaseService();
            _knowledgeEntries = new ObservableCollection<KnowledgeEntryViewModel>();
            KnowledgeDataGrid.ItemsSource = _knowledgeEntries;
            LoadSettings();
            _ = LoadEntriesAsync();
        }

        private void LoadSettings()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(json);
                    if (settings != null)
                    {
                        _ollama.UpdateBaseUrl(settings.OllamaUrl);
                    }
                }
            }
            catch { }
        }

        private async Task LoadEntriesAsync()
        {
            try
            {
                ShowProgress("正在加载知识库...", 0);
                await Task.Delay(100); // UI 更新延迟

                var entries = await _vectorDb.GetAllEntriesAsync();
                _knowledgeEntries.Clear();
                
                foreach (var entry in entries)
                {
                    _knowledgeEntries.Add(new KnowledgeEntryViewModel(entry));
                }

                UpdateStatus($"已加载 {_knowledgeEntries.Count} 条知识", true);
                await UpdateCountsAsync();
                HideProgress();
            }
            catch (Exception ex)
            {
                UpdateStatus($"加载失败：{ex.Message}", false);
                HideProgress();
            }
        }

        private async void AddManualBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ManualInputDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowProgress("正在添加知识...", 0);
                    
                    var entry = new KnowledgeEntryViewModel
                    {
                        Title = dialog.Title,
                        Content = dialog.Content,
                        Type = "手动输入",
                        CreatedTime = DateTime.Now
                    };

                    // 保存到数据库
                    var id = await _vectorDb.CreateEntryAsync(
                        entry.Title, 
                        entry.Content, 
                        entry.Type,
                        null, 0, false);

                    if (id > 0)
                    {
                        entry.Id = id;
                        _knowledgeEntries.Insert(0, entry);
                        UpdateStatus($"已添加：{entry.Title}", true);
                        await UpdateCountsAsync();
                    }
                    
                    HideProgress();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"添加失败：{ex.Message}", false);
                    HideProgress();
                }
            }
        }

        private async void ImportFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "文本文件|*.txt;*.md;*.docx;*.pdf;*.xlsx;*.xls|所有文件|*.*",
                Title = "选择要导入的文件",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    int successCount = 0;
                    int totalCount = openFileDialog.FileNames.Length;
                    
                    ShowProgress("正在导入文件...", 0);

                    foreach (var filePath in openFileDialog.FileNames)
                    {
                        try
                        {
                            var content = ExtractFileToPlainText(filePath);
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            var extension = Path.GetExtension(filePath).TrimStart('.').ToUpper();

                            var entry = new KnowledgeEntryViewModel
                            {
                                Title = fileName,
                                Content = content,
                                Type = extension,
                                CreatedTime = DateTime.Now
                            };

                            var id = await _vectorDb.CreateEntryAsync(
                                entry.Title,
                                entry.Content,
                                entry.Type,
                                null, 0, false);

                            if (id > 0)
                            {
                                entry.Id = id;
                                _knowledgeEntries.Insert(0, entry);
                                successCount++;
                            }

                            // 更新进度
                            var progress = (successCount * 100) / totalCount;
                            ShowProgress($"正在导入文件... ({successCount}/{totalCount})", progress);
                        }
                        catch (Exception ex)
                        {
                            UpdateStatus($"导入失败 {Path.GetFileName(filePath)}: {ex.Message}", false);
                        }
                    }

                    UpdateStatus($"导入完成：成功 {successCount}/{totalCount} 个文件", true);
                    await UpdateCountsAsync();
                    HideProgress();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"导入失败：{ex.Message}", false);
                    HideProgress();
                }
            }
        }

        private async void BatchImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "文本文件|*.txt;*.md;*.docx;*.pdf;*.xlsx;*.xls|所有文件|*.*",
                Title = "批量导入文件（可多选）",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    int successCount = 0;
                    int totalCount = openFileDialog.FileNames.Length;
                    
                    ShowProgress("正在批量导入文件...", 0);

                    foreach (var filePath in openFileDialog.FileNames)
                    {
                        try
                        {
                            var content = ExtractFileToPlainText(filePath);
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            var extension = Path.GetExtension(filePath).TrimStart('.').ToUpper();

                            var entry = new KnowledgeEntryViewModel
                            {
                                Title = fileName,
                                Content = content,
                                Type = extension,
                                CreatedTime = DateTime.Now
                            };

                            var id = await _vectorDb.CreateEntryAsync(
                                entry.Title,
                                entry.Content,
                                entry.Type,
                                null, 0, false);

                            if (id > 0)
                            {
                                entry.Id = id;
                                _knowledgeEntries.Insert(0, entry);
                                successCount++;
                            }

                            var progress = (successCount * 100) / totalCount;
                            ShowProgress($"正在批量导入... ({successCount}/{totalCount})", progress);
                        }
                        catch (Exception ex)
                        {
                            UpdateStatus($"导入失败 {Path.GetFileName(filePath)}: {ex.Message}", false);
                        }
                    }

                    UpdateStatus($"批量导入完成：成功 {successCount}/{totalCount} 个文件", true);
                    await UpdateCountsAsync();
                    HideProgress();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"批量导入失败：{ex.Message}", false);
                    HideProgress();
                }
            }
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            
            try
            {
                _isProcessing = true;
                ShowProgress("正在同步到向量数据库...", 0);

                var entries = _knowledgeEntries.ToList();
                int processed = 0;
                int total = entries.Count;

                foreach (var entry in entries)
                {
                    try
                    {
                        // 生成向量
                        var textForEmbedding = $"标题：{entry.Title}\n内容：{entry.Content}";
                        var vector = await _ollama.GetEmbeddingForLongTextAsync(textForEmbedding, _embeddingModel);

                        if (vector != null && vector.Length > 0)
                        {
                            // 存储向量
                            await _vectorDb.StoreVectorAsync(
                                entry.Id, 
                                entry.Title, 
                                entry.Content, 
                                vector);
                            
                            processed++;
                        }

                        // 更新进度
                        var progress = (processed * 100) / total;
                        ShowProgress($"正在同步向量... ({processed}/{total})", progress);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"同步失败 {entry.Title}: {ex.Message}", false);
                    }
                }

                UpdateStatus($"同步完成：{processed}/{total} 条记录", true);
                await UpdateCountsAsync();
                HideProgress();
            }
            catch (Exception ex)
            {
                UpdateStatus($"同步失败：{ex.Message}", false);
                HideProgress();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadEntriesAsync();
        }

        private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is KnowledgeEntryViewModel entry)
            {
                if (entry.IsLocked)
                {
                    System.Windows.MessageBox.Show("此条目已被锁定，无法删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (System.Windows.MessageBox.Show($"确定要删除 '{entry.Title}' 吗？", "确认删除", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 从数据库删除
                        await _vectorDb.DeleteEntryAsync(entry.Id);
                        await _vectorDb.DeleteVectorAsync(entry.Id);
                        
                        _knowledgeEntries.Remove(entry);
                        UpdateStatus($"已删除：{entry.Title}", true);
                        await UpdateCountsAsync();
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"删除失败：{ex.Message}", false);
                    }
                }
            }
        }

        private void LockUnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is KnowledgeEntryViewModel entry)
            {
                if (!entry.IsLocked)
                {
                    // 锁定条目
                    entry.IsLocked = true;
                    _ = _vectorDb.UpdateEntryAsync(entry.Id, entry.Title, entry.Content, 
                        entry.OriginalContent, entry.CompressionLevel, entry.IsLocked);
                    UpdateStatus($"已锁定：{entry.Title}", true);
                }
                else
                {
                    // 解锁条目 - 需要输入密码
                    var passwordDialog = new PasswordDialog();
                    passwordDialog.Owner = this;
                    if (passwordDialog.ShowDialog() == true)
                    {
                        if (passwordDialog.Password == "admin888")
                        {
                            entry.IsLocked = false;
                            _ = _vectorDb.UpdateEntryAsync(entry.Id, entry.Title, entry.Content,
                                entry.OriginalContent, entry.CompressionLevel, entry.IsLocked);
                            UpdateStatus($"已解锁：{entry.Title}", true);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("密码错误，无法解锁！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is KnowledgeEntryViewModel entry)
            {
                if (entry.IsLocked)
                {
                    System.Windows.MessageBox.Show("此条目已被锁定，无法编辑！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new ManualInputDialog(entry.Title, entry.Content);
                if (dialog.ShowDialog() == true)
                {
                    entry.Title = dialog.Title;
                    entry.Content = dialog.Content;
                    
                    // 更新数据库
                    _ = _vectorDb.UpdateEntryAsync(entry.Id, entry.Title, entry.Content,
                        entry.OriginalContent, entry.CompressionLevel, entry.IsLocked);
                    
                    UpdateStatus($"已编辑：{entry.Title}", true);
                }
            }
        }

        private async void CompressBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is KnowledgeEntryViewModel entry)
            {
                if (entry.IsLocked)
                {
                    System.Windows.MessageBox.Show("此条目已被锁定，无法压缩！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new CompressionDialog(entry.ToKnowledgeEntry(), _ollama, "glm-4.7-flash") { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    entry.OriginalContent = entry.Content;
                    entry.Content = dialog.CompressedContent;
                    entry.CompressionLevel = dialog.CompressionLevel;
                    
                    // 更新数据库
                    await _vectorDb.UpdateEntryAsync(entry.Id, entry.Title, entry.Content,
                        entry.OriginalContent, entry.CompressionLevel, entry.IsLocked);
                    
                    UpdateStatus($"已压缩：{entry.Title}", true);
                }
            }
        }

        private async Task UpdateCountsAsync()
        {
            try
            {
                var entryCount = await _vectorDb.GetEntryCountAsync();
                var vectorCount = await _vectorDb.GetVectorCountAsync();
                
                ItemCountText.Text = $"{entryCount} 条";
                VectorCountText.Text = $"{vectorCount} 个";
            }
            catch { }
        }

        private void ShowProgress(string title, int value)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressTitle.Text = title;
                SyncProgressBar.Value = value;
                ProgressText.Text = $"{value}%";
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
            });
        }

        private void UpdateStatus(string message, bool isSuccess)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = (isSuccess ? "✓ " : "✗ ") + message;
                StatusText.Foreground = isSuccess 
                    ? (System.Windows.Media.Brush)FindResource("Success") 
                    : (System.Windows.Media.Brush)FindResource("Danger");
            });
        }

        private string ExtractFileToPlainText(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var extension = Path.GetExtension(filePath).ToLower();
            StringBuilder plainText = new StringBuilder();

            // 1. 纯文本文件
            var textExtensions = new[] { ".txt", ".md", ".log", ".ini", ".conf", ".reg", ".bat", ".cmd", 
                ".c", ".cpp", ".h", ".cs", ".java", ".py", ".html", ".htm", ".css", ".js", 
                ".sql", ".json", ".xml", ".yml", ".yaml", ".csv" };
            
            if (textExtensions.Contains(extension))
            {
                using (var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    return reader.ReadToEnd();
                }
            }

            // 2. Word 文档
            if (extension == ".docx")
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    XWPFDocument doc = new XWPFDocument(fs);
                    foreach (var para in doc.Paragraphs)
                    {
                        plainText.AppendLine(para.Text?.Trim());
                    }
                    foreach (var table in doc.Tables)
                    {
                        foreach (var row in table.Rows)
                        {
                            foreach (var cell in row.GetTableCells())
                            {
                                var cellText = cell.Paragraphs != null 
                                    ? string.Join(" ", cell.Paragraphs.Select(p => p.Text)) 
                                    : "";
                                plainText.Append(cellText.Trim() + "\t");
                            }
                            plainText.AppendLine();
                        }
                    }
                }
                return plainText.ToString();
            }

            // 3. Excel 文档
            if (extension == ".xlsx" || extension == ".xls")
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    IWorkbook workbook = extension == ".xlsx"
                        ? new XSSFWorkbook(fs)
                        : new HSSFWorkbook(fs);

                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                    {
                        var sheet = workbook.GetSheetAt(i);
                        if (sheet == null) continue;

                        plainText.AppendLine($"【工作表：{sheet.SheetName}】");

                        for (int rowIdx = 0; rowIdx <= sheet.LastRowNum; rowIdx++)
                        {
                            var rowObj = sheet.GetRow(rowIdx);
                            if (rowObj == null) continue;

                            for (int colIdx = 0; colIdx < rowObj.LastCellNum; colIdx++)
                            {
                                var cell = rowObj.GetCell(colIdx);
                                string cellValue = "";
                                if (cell != null)
                                {
                                    switch (cell.CellType)
                                    {
                                        case CellType.String:
                                            cellValue = cell.StringCellValue;
                                            break;
                                        case CellType.Numeric:
                                            cellValue = DateUtil.IsCellDateFormatted(cell)
                                                ? cell.DateCellValue.ToString()
                                                : cell.NumericCellValue.ToString();
                                            break;
                                        case CellType.Boolean:
                                            cellValue = cell.BooleanCellValue.ToString();
                                            break;
                                        case CellType.Formula:
                                            cellValue = cell.CellFormula;
                                            break;
                                        default:
                                            cellValue = cell.ToString();
                                            break;
                                    }
                                }
                                plainText.Append(cellValue?.Trim() ?? "" + "\t");
                            }
                            plainText.AppendLine();
                        }
                        plainText.AppendLine("------------------------");
                    }
                }
                return plainText.ToString();
            }

            // 4. PDF 文档
            if (extension == ".pdf")
            {
                using (var document = PdfDocument.Open(filePath))
                {
                    plainText.AppendLine($"【PDF 总页数：{document.NumberOfPages}】");
                    for (int i = 0; i < document.NumberOfPages; i++)
                    {
                        var page = document.GetPage(i + 1);
                        plainText.AppendLine($"【第{i + 1}页】");
                        plainText.AppendLine(page.Text?.Trim());
                        plainText.AppendLine("------------------------");
                    }
                }
                return plainText.ToString();
            }

            throw new NotSupportedException($"暂不支持解析{extension}格式文件");
        }
    }

    // KnowledgeEntryViewModel 类
    public class KnowledgeEntryViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _title = "";
        private string _content = "";
        private string _type = "";
        private DateTime _createdTime;
        private bool _isLocked = false;
        private string? _originalContent = "";
        private int _compressionLevel = 0;

        public KnowledgeEntryViewModel() { }

        public KnowledgeEntryViewModel(KnowledgeEntryData data)
        {
            Id = data.Id;
            Title = data.Title;
            Content = data.Content;
            Type = data.Type;
            IsLocked = data.IsLocked;
            OriginalContent = data.OriginalContent;
            CompressionLevel = data.CompressionLevel;
            CreatedTime = data.CreatedTime;
        }

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(nameof(Content)); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public DateTime CreatedTime
        {
            get => _createdTime;
            set { _createdTime = value; OnPropertyChanged(nameof(CreatedTime)); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); OnPropertyChanged(nameof(LockStatusDisplay)); OnPropertyChanged(nameof(LockButtonText)); }
        }

        public string? OriginalContent
        {
            get => _originalContent;
            set { _originalContent = value; OnPropertyChanged(nameof(OriginalContent)); }
        }

        public int CompressionLevel
        {
            get => _compressionLevel;
            set { _compressionLevel = value; OnPropertyChanged(nameof(CompressionLevel)); OnPropertyChanged(nameof(CompressionStatusDisplay)); OnPropertyChanged(nameof(IsCompressed)); }
        }

        public bool IsCompressed => _compressionLevel > 0;

        public string CompressionStatusDisplay => _compressionLevel switch
        {
            0 => "未压缩",
            1 => "轻度压缩",
            2 => "中度压缩",
            3 => "高度压缩",
            _ => "已压缩"
        };

        public string LockStatusDisplay => IsLocked ? "🔒 已锁定" : "🔓 未锁定";
        public string LockButtonText => IsLocked ? "解锁" : "锁定";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 转换为 KnowledgeEntry（用于压缩对话框）
        public KnowledgeEntry ToKnowledgeEntry()
        {
            return new KnowledgeEntry
            {
                Id = this.Id,
                Title = this.Title,
                Content = this.Content,
                Type = this.Type,
                CreatedTime = this.CreatedTime,
                IsLocked = this.IsLocked,
                OriginalContent = this.OriginalContent,
                CompressionLevel = this.CompressionLevel
            };
        }
    }

    // KnowledgeEntry 类（兼容压缩对话框）
    public class KnowledgeEntry
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime CreatedTime { get; set; }
        public bool IsLocked { get; set; }
        public string? OriginalContent { get; set; }
        public int CompressionLevel { get; set; }
    }
}
