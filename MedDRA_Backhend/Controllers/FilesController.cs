using MedDRA_Backhend.Contracts.Encoding;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace MedDRA_Backhend.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private readonly IExcelExportService _excelExportService;
    private readonly IExcelTermParser _excelTermParser;

    public FilesController(IExcelTermParser excelTermParser, IExcelExportService excelExportService)
    {
        _excelTermParser = excelTermParser;
        _excelExportService = excelExportService;
    }

    /// <summary>
    /// 上传 Excel 文件并解析出待编码术语列表。
    /// 成功后返回预览结果，不在此步骤执行编码、向量检索或 AI 调用。
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadAsync([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest("请上传 Excel 文件。");
        }

        var preview = await _excelTermParser.ParseAsync(file, cancellationToken);
        return Ok(preview);
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportAsync([FromBody] List<EncodingResultDto> results, CancellationToken cancellationToken)
    {
        var bytes = await _excelExportService.ExportAsync(results, cancellationToken);
        return File(
            fileContents: bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: $"meddra-coding-result-{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }
}
