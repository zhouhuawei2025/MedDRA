using MedDRA_Backhend.Domain;

namespace MedDRA_Backhend.Services.Abstractions;

public interface IQdrantSearchService
{
    Task<IReadOnlyList<MedDraSearchCandidate>> SearchAsync(
        string version,
        float[] vector,
        int limit,
        bool onlyCurrentTerms,
        CancellationToken cancellationToken);
}
