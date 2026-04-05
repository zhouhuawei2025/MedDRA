using MedDRA_Backhend.Contracts.Encoding;
using MedDRA_Backhend.Services.Abstractions;
using OfficeOpenXml;

namespace MedDRA_Backhend.Services;

public sealed class EpplusExcelExportService : IExcelExportService
{
    public Task<byte[]> ExportAsync(IReadOnlyCollection<EncodingResultDto> results, CancellationToken cancellationToken)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("编码结果");

        var headers = new[]
        {
            "原始术语", "MedDRA版本", "Top1Score", "是否调用AI", "备注",
            "最优LLT", "最优LLT编码", "最优PT", "最优PT编码", "最优HLT", "最优HLGT", "最优SOC",
            "次优LLT", "次优LLT编码", "次优PT", "次优PT编码", "次优HLT", "次优HLGT", "次优SOC",
            "较优LLT", "较优LLT编码", "较优PT", "较优PT编码", "较优HLT", "较优HLGT", "较优SOC"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
        }

        var rowIndex = 2;
        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            worksheet.Cells[rowIndex, 1].Value = result.RawTerm;
            worksheet.Cells[rowIndex, 2].Value = result.Version;
            worksheet.Cells[rowIndex, 3].Value = result.Top1Score;
            worksheet.Cells[rowIndex, 4].Value = result.UsedAi ? "Y" : "N";
            worksheet.Cells[rowIndex, 5].Value = result.Remark;

            WriteCandidate(result.Candidates.ElementAtOrDefault(0), worksheet, rowIndex, 6);
            WriteCandidate(result.Candidates.ElementAtOrDefault(1), worksheet, rowIndex, 13);
            WriteCandidate(result.Candidates.ElementAtOrDefault(2), worksheet, rowIndex, 20);

            rowIndex++;
        }

        worksheet.Cells[worksheet.Dimension!.Address].AutoFitColumns();
        return Task.FromResult(package.GetAsByteArray());
    }

    private static void WriteCandidate(CandidateTermDto? candidate, ExcelWorksheet worksheet, int rowIndex, int startColumn)
    {
        if (candidate is null)
        {
            return;
        }

        worksheet.Cells[rowIndex, startColumn].Value = candidate.LltName;
        worksheet.Cells[rowIndex, startColumn + 1].Value = candidate.LltCode;
        worksheet.Cells[rowIndex, startColumn + 2].Value = candidate.PtName;
        worksheet.Cells[rowIndex, startColumn + 3].Value = candidate.PtCode;
        worksheet.Cells[rowIndex, startColumn + 4].Value = candidate.HltName;
        worksheet.Cells[rowIndex, startColumn + 5].Value = candidate.HgltName;
        worksheet.Cells[rowIndex, startColumn + 6].Value = candidate.SocName;
    }
}
