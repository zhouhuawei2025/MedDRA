using MedDRA_Backhend.Contracts.Files;
using MedDRA_Backhend.Services.Abstractions;
using OfficeOpenXml;

namespace MedDRA_Backhend.Services;

public sealed class EpplusExcelTermParser : IExcelTermParser
{
    private static readonly string[] SupportedHeaders = ["Term", "待编码术语", "原始术语"];

    public async Task<UploadPreviewResponse> ParseAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("上传文件为空。");
        }

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Excel 中未找到工作表。");

        var headerIndex = FindHeaderIndex(worksheet);
        if (headerIndex is null)
        {
            throw new InvalidOperationException($"未找到术语列，支持的列名：{string.Join(" / ", SupportedHeaders)}");
        }

        var response = new UploadPreviewResponse
        {
            FileName = file.FileName
        };

        var rowCount = worksheet.Dimension?.Rows ?? 0;
        for (var row = 2; row <= rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawTerm = worksheet.Cells[row, headerIndex.Value].Text.Trim();
            if (string.IsNullOrWhiteSpace(rawTerm))
            {
                continue;
            }

            response.Rows.Add(new UploadPreviewRowDto
            {
                RowNumber = row,
                RawTerm = rawTerm
            });
        }

        response.TotalRows = response.Rows.Count;
        return response;
    }

    private static int? FindHeaderIndex(ExcelWorksheet worksheet)
    {
        var columnCount = worksheet.Dimension?.Columns ?? 0;
        for (var col = 1; col <= columnCount; col++)
        {
            var header = worksheet.Cells[1, col].Text.Trim();
            if (SupportedHeaders.Any(x => string.Equals(x, header, StringComparison.OrdinalIgnoreCase)))
            {
                return col;
            }
        }

        return null;
    }
}
