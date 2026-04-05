using MedDRA_Backhend.Contracts.Encoding;

namespace MedDRA_Backhend.Services.Abstractions;

public interface IMedDraEncodingService
{
    Task<EncodingRunResponse> RunAsync(EncodingRunRequest request, CancellationToken cancellationToken);
}
