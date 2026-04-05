using MedDRA_Backhend.Contracts.Files;

namespace MedDRA_Backhend.Services.Abstractions;

public interface IExcelTermParser
{
    Task<UploadPreviewResponse> ParseAsync(IFormFile file, CancellationToken cancellationToken);
}
