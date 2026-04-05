using MedDRA_Backhend.Contracts.Meddra;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace MedDRA_Backhend.Controllers;

[ApiController]
[Route("api/meddra")]
public sealed class MeddraController : ControllerBase
{
    private readonly IMedDraVersionService _versionService;

    public MeddraController(IMedDraVersionService versionService)
    {
        _versionService = versionService;
    }

    [HttpGet("versions")]
    public ActionResult<List<MeddraVersionDto>> GetVersions()
    {
        var versions = _versionService.GetAvailableVersions()
            .Select(x => new MeddraVersionDto
            {
                Version = x.Version,
                CollectionName = x.CollectionName
            })
            .ToList();

        return Ok(versions);
    }
}
