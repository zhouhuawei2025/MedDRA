using MedDRA_Backhend.Contracts.Encoding;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace MedDRA_Backhend.Controllers;

[ApiController]
[Route("api/encoding")]
public sealed class EncodingController : ControllerBase
{
    private readonly IMedDraEncodingService _encodingService;

    public EncodingController(IMedDraEncodingService encodingService)
    {
        _encodingService = encodingService;
    }

    [HttpPost("run")]
    public async Task<ActionResult<EncodingRunResponse>> RunAsync(
        [FromBody] EncodingRunRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return BadRequest("请选择 MedDRA 版本。");
        }

        var response = await _encodingService.RunAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("single")]
    public async Task<ActionResult<EncodingResultDto>> RunSingleAsync(
        [FromBody] SingleEncodingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return BadRequest("请选择 MedDRA 版本。");
        }

        if (string.IsNullOrWhiteSpace(request.Term))
        {
            return BadRequest("请输入待编码术语。");
        }

        var response = await _encodingService.RunAsync(
            new EncodingRunRequest
            {
                Version = request.Version,
                HighConfidenceThreshold = request.HighConfidenceThreshold,
                MinimumScoreGap = request.MinimumScoreGap,
                Terms = [request.Term]
            },
            cancellationToken);

        var result = response.Results.FirstOrDefault();
        if (result is null)
        {
            return NotFound("未返回编码结果。");
        }

        return Ok(result);
    }
}
