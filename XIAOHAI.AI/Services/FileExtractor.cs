using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace XIAOHAI.AI.Services;

public static class FileExtractor
{
    private static readonly string[] TextExtensions = {
        ".txt", ".ini", ".conf", ".reg", ".log", ".bat", ".cmd",
        ".c", ".cpp", ".h", ".cs", ".java", ".py", ".html", ".htm",
        ".css", ".js", ".sql", ".json", ".xml", ".md", ".yml", ".yaml", ".csv"
    };

    public static string ExtractToPlainText(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        var extension = Path.GetExtension(filePath).ToLower();
        StringBuilder plainText = new();

        if (TextExtensions.Contains(extension))
        {
            return ReadTextFile(filePath);
        }

        return extension switch
        {
            ".docx" => ExtractFromDocx(filePath),
            ".doc" => throw new NotSupportedException(".doc 格式需要额外的库支持，建议转换为 .docx 格式"),
            ".xlsx" or ".xls" => ExtractFromExcel(filePath, extension),
            ".pdf" => ExtractFromPdf(filePath),
            _ => throw new NotSupportedException($"暂不支持解析{extension}格式文件")
        };
    }

    private static string ReadTextFile(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string ExtractFromDocx(string filePath)
    {
        StringBuilder plainText = new();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        XWPFDocument doc = new(fs);

        foreach (var para in doc.Paragraphs)
        {
            plainText.AppendLine(para.Text.Trim());
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

        return plainText.ToString();
    }

    private static string ExtractFromExcel(string filePath, string extension)
    {
        StringBuilder plainText = new();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

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
                    string cellValue = GetCellValue(cell);
                    plainText.Append(cellValue?.Trim() ?? "" + "\t");
                }
                plainText.AppendLine();
            }
            plainText.AppendLine("------------------------");
        }

        return plainText.ToString();
    }

    private static string GetCellValue(NPOI.SS.UserModel.ICell? cell)
    {
        if (cell == null) return "";

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue.ToString()
                : cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.CellFormula,
            _ => cell.ToString()
        };
    }

    private static string ExtractFromPdf(string filePath)
    {
        StringBuilder plainText = new();
        using var document = PdfDocument.Open(filePath);

        plainText.AppendLine($"【PDF总页数：{document.NumberOfPages}】");

        for (int i = 0; i < document.NumberOfPages; i++)
        {
            var page = document.GetPage(i + 1);
            plainText.AppendLine($"【第{i + 1}页】");
            plainText.AppendLine(page.Text.Trim());
            plainText.AppendLine("------------------------");
        }

        return plainText.ToString();
    }

    public static string CleanText(string text)
    {
        return Regex.Replace(text, @"\n{2,}", "\n").Trim();
    }

    public static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" ||
               extension == ".gif" || extension == ".bmp" || extension == ".tiff" || extension == ".tif";
    }
}
