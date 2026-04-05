using MedDRA_Backhend.Contracts.Encoding;

namespace MedDRA_Backhend.Services.Abstractions;

public interface IExcelExportService
{
    Task<byte[]> ExportAsync(IReadOnlyCollection<EncodingResultDto> results, CancellationToken cancellationToken);
}
