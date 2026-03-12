using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XIAOHAI.AI.Services;
using Drawing = System.Drawing;

namespace XIAOHAI.AI.Services;

public class ImageService
{
    private readonly OllamaService _ollama;
    private readonly string _visionModel;

    public ImageService(OllamaService ollama, string visionModel = "llava")
    {
        _ollama = ollama;
        _visionModel = visionModel;
    }

    public async Task<string> AnalyzeImageAsync(Drawing.Bitmap bitmap, string? customPrompt = null, ImageAnalysisScenario scenario = ImageAnalysisScenario.General)
    {
        try
        {
            Console.WriteLine($"[Vision] 开始处理图像：{bitmap.Width}x{bitmap.Height}, 格式：{bitmap.PixelFormat}");
            Console.WriteLine($"[Vision] 使用模型：{_visionModel}");
            Console.WriteLine($"[Vision] 分析场景：{scenario}");

            string base64Image = BitmapToBase64(bitmap);
            if (string.IsNullOrEmpty(base64Image))
            {
                return "图片转换失败";
            }

            Console.WriteLine($"[Vision] 图片 Base64 长度：{base64Image.Length} 字符");

            // 根据场景生成最合适的 prompt
            string prompt = customPrompt ?? GetPromptForScenario(scenario);
            Console.WriteLine($"[Vision] Prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");

            var visionResult = await _ollama.ChatWithImageAsync(_visionModel, prompt, base64Image);

            Console.WriteLine($"[Vision] 识别结果：{(visionResult?.Length > 100 ? visionResult.Substring(0, 100) + "..." : visionResult)}");

            // 检查是否为有效的识别结果
            if (!string.IsNullOrWhiteSpace(visionResult))
            {
                // 只有当结果明确包含连接失败或特定错误信息时，才判定为失败
                bool isConnectionError = visionResult.Contains("无法连接") ||
                                        visionResult.Contains("连接 Ollama 失败") ||
                                        visionResult.Contains("请求失败") ||
                                        visionResult.Contains("HttpRequestException");

                bool isModelError = visionResult.Contains("model") && visionResult.Contains("not found") ||
                                   visionResult.Contains("模型不存在") ||
                                   visionResult.Contains("未安装");

                if (!isConnectionError && !isModelError && visionResult.Length > 5)
                {
                    Console.WriteLine($"[Vision] 识别成功，返回结果");
                    return visionResult;
                }

                Console.WriteLine($"[Vision] 识别结果包含错误信息：{visionResult.Substring(0, Math.Min(100, visionResult.Length))}");
            }
            else
            {
                Console.WriteLine($"[Vision] 识别结果为空");
            }

            return "AI 视觉识别失败，请检查是否已安装视觉模型 (ollama pull llava)";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vision] 处理异常: {ex.Message}");
            return $"AI视觉识别异常: {ex.Message}";
        }
    }

    public string BitmapToBase64(Drawing.Bitmap bitmap)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, Drawing.Imaging.ImageFormat.Png);
            byte[] imageBytes = memoryStream.ToArray();
            return Convert.ToBase64String(imageBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vision] Bitmap转Base64失败: {ex.Message}");
            return null;
        }
    }

    public Drawing.Bitmap? ConvertImageSourceToBitmap(ImageSource imageSource)
    {
        try
        {
            if (imageSource == null)
            {
                Console.WriteLine("[OCR] ImageSource 为 null");
                return null;
            }

            var bitmap = imageSource as BitmapSource;
            if (bitmap == null)
            {
                Console.WriteLine("[OCR] 无法转换为 BitmapSource");
                return null;
            }

            Console.WriteLine($"[OCR] BitmapSource 格式: {bitmap.Format}, 尺寸: {bitmap.PixelWidth}x{bitmap.PixelHeight}");

            BitmapSource convertedBitmap = bitmap;
            if (bitmap.Format != PixelFormats.Bgr24 &&
                bitmap.Format != PixelFormats.Bgr32 &&
                bitmap.Format != PixelFormats.Rgb24 &&
                bitmap.Format != PixelFormats.Pbgra32)
            {
                convertedBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
                Console.WriteLine($"[OCR] 已转换像素格式为 Bgr24");
            }

            if (convertedBitmap.CanFreeze)
            {
                convertedBitmap.Freeze();
            }

            int width = convertedBitmap.PixelWidth;
            int height = convertedBitmap.PixelHeight;
            int stride = width * ((convertedBitmap.Format.BitsPerPixel + 7) / 8);
            byte[] pixels = new byte[height * stride];

            convertedBitmap.CopyPixels(pixels, stride, 0);

            var resultBitmap = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format24bppRgb);
            var bmpData = resultBitmap.LockBits(
                new Drawing.Rectangle(0, 0, width, height),
                Drawing.Imaging.ImageLockMode.WriteOnly,
                Drawing.Imaging.PixelFormat.Format24bppRgb);

            try
            {
                if (convertedBitmap.Format == PixelFormats.Bgr32 ||
                    convertedBitmap.Format == PixelFormats.Pbgra32)
                {
                    int sourceStride = width * 4;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int sourceIndex = y * sourceStride + x * 4;
                            int destIndex = y * bmpData.Stride + x * 3;
                            if (sourceIndex + 2 < pixels.Length && destIndex + 2 < bmpData.Stride * height)
                            {
                                Marshal.WriteByte(bmpData.Scan0, destIndex, pixels[sourceIndex]);
                                Marshal.WriteByte(bmpData.Scan0, destIndex + 1, pixels[sourceIndex + 1]);
                                Marshal.WriteByte(bmpData.Scan0, destIndex + 2, pixels[sourceIndex + 2]);
                            }
                        }
                    }
                }
                else
                {
                    Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                }
            }
            finally
            {
                resultBitmap.UnlockBits(bmpData);
            }

            Console.WriteLine($"[OCR] 成功转换为 Bitmap: {resultBitmap.Width}x{resultBitmap.Height}");
            return resultBitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OCR] 图像转换异常: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    public Drawing.Bitmap? ConvertDibToBitmap(Stream dibStream)
    {
        try
        {
            dibStream.Position = 0;
            using var reader = new BinaryReader(dibStream);
            var headerSize = reader.ReadInt32();
            dibStream.Position = 0;
            return new Drawing.Bitmap(dibStream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OCR] DIB 转换失败: {ex.Message}");
            return null;
        }
    }

    public byte[] GetBitmapBytes(Drawing.Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, Drawing.Imaging.ImageFormat.Png);
        return memory.ToArray();
    }

    /// <summary>
    /// 根据场景生成最合适的 prompt
    /// </summary>
    private string GetPromptForScenario(ImageAnalysisScenario scenario)
    {
        return scenario switch
        {
            ImageAnalysisScenario.General => GetGeneralPrompt(),
            ImageAnalysisScenario.ScreenProblem => GetScreenProblemPrompt(),
            ImageAnalysisScenario.OCRFieldRecognition => GetOCRFieldRecognitionPrompt(),
            _ => GetGeneralPrompt()
        };
    }

    /// <summary>
    /// 通用场景：日常对话中的图片分析
    /// 适用于用户在输入框粘贴图片，需要识别图片内容的场景
    /// </summary>
    private string GetGeneralPrompt()
    {
        return """
            你是一个专业的图像分析助手。请分析这张图片并用中文回答。
            
            请按以下步骤分析：
            1. 首先判断图片类型（照片/截图/文档/图表/二维码/其他）
            2. 根据类型提供相应分析：
               - 照片：描述场景、人物、物体、颜色、氛围
               - 截图：描述界面内容、软件名称、主要功能
               - 文档：完整准确地识别所有文字内容
               - 图表：描述数据趋势、关键信息、图表类型
               - 二维码：识别并输出内容
            3. 如果图片中有文字，优先完整输出文字内容
            4. 如果图片模糊或无法识别，请说明原因
            
            要求：
            - 直接输出分析结果，不要添加"分析结果"等前缀
            - 保持简洁，重点突出关键信息
            - 使用中文回答
            """;
    }

    /// <summary>
    /// 屏幕问题分析场景：悬浮球功能，分析用户当前屏幕可能遇到的问题
    /// 适用于用户点击悬浮球"查看当前问题"功能
    /// </summary>
    private string GetScreenProblemPrompt()
    {
        return """
            你是一个专业的用户界面分析和问题诊断助手。请分析这张屏幕截图，完成以下任务：
            
            【分析步骤】
            1. 界面类型识别
               - 判断这是什么类型的界面（网页浏览器/桌面应用/文档编辑器/代码编辑器/系统窗口/其他）
               - 识别具体的应用程序名称（如果能识别）
            
            2. 界面内容描述
               - 描述界面中显示的主要内容
               - 识别界面中的关键元素（按钮、菜单、输入框、提示信息等）
               - 注意任何错误提示、警告标志或异常状态
            
            3. 问题诊断
               - 如果界面显示错误信息，详细说明错误内容和可能原因
               - 如果界面显示异常（如空白、乱码、加载失败），描述异常现象
               - 如果界面正常，推测用户当前可能遇到的问题或想要完成的任务
            
            4. 使用场景分析
               - 根据界面内容，推测用户正在进行什么操作
               - 分析用户可能遇到的困难或需要的帮助
            
            【输出格式】
            请按以下结构输出：
            - 界面类型：[类型名称]
            - 主要内容：[描述]
            - 问题诊断：[分析结果]
            - 使用场景：[推测]
            - 建议：[可选，给出解决建议]
            
            要求：
            - 使用中文简洁明了地回答
            - 保持专业且有帮助性
            - 如果有明确的错误信息，优先完整输出
            """;
    }

    /// <summary>
    /// OCR 字段识别场景：视觉识别窗口，识别证件、表单等图片中的结构化字段
    /// 适用于用户需要识别身份证、银行卡、发票等证件并自动填表的场景
    /// </summary>
    private string GetOCRFieldRecognitionPrompt()
    {
        return """
            你是一个专业的 OCR 文字识别和字段提取助手。请识别这张图片中的所有文字，并按结构化格式提取字段信息。
            
            【识别要求】
            1. 完整识别图片中的所有文字内容，不遗漏任何可见文字
            2. 准确识别字段标签和对应的值
            3. 保持文字的原始顺序和层次结构
            4. 对于模糊或不确定的文字，用 [?] 标记
            
            【字段提取规则】
            - 识别常见的证件类型（身份证、银行卡、驾驶证、行驶证、发票等）
            - 提取关键字段（姓名、性别、民族、出生、住址、公民身份号码等）
            - 对于表格类图片，按行列结构提取数据
            
            【输出格式】
            严格按照以下 JSON 格式输出，不要添加任何额外说明：
            {
                "documentType": "证件类型（如：居民身份证/银行卡/发票等）",
                "confidence": "识别置信度（high/medium/low）",
                "fields": [
                    {
                        "label": "字段名称（如：姓名）",
                        "value": "字段值（如：张三）",
                        "confidence": "字段置信度（high/medium/low）"
                    }
                ],
                "rawText": "完整识别的原始文字内容（按行输出）"
            }
            
            【注意事项】
            - 如果图片不是证件或表单，而是普通文档，则输出纯文本内容
            - 确保数字、字母、汉字都准确识别
            - 对于关键信息（如身份证号、金额、日期）要特别仔细核对
            - 如果图片质量差导致无法识别，请说明原因
            
            要求：
            - 只输出 JSON，不要有任何其他文字
            - 确保 JSON 格式正确，可以被解析
            - 使用中文字段名
            """;
    }
}

/// <summary>
/// 图像分析场景枚举
/// </summary>
public enum ImageAnalysisScenario
{
    /// <summary>
    /// 通用场景：日常对话中的图片分析
    /// </summary>
    General,

    /// <summary>
    /// 屏幕问题分析：悬浮球功能，分析用户当前屏幕可能遇到的问题
    /// </summary>
    ScreenProblem,

    /// <summary>
    /// OCR 字段识别：视觉识别窗口，识别证件、表单等图片中的结构化字段
    /// </summary>
    OCRFieldRecognition
}
