using MedDRA_Backhend.Domain;

namespace MedDRA_Backhend.Services.Abstractions;

public interface IAiRerankService
{
    Task<(IReadOnlyList<MedDraSearchCandidate> Candidates, string Reason)> RerankAsync(
        string rawTerm,
        IReadOnlyList<MedDraSearchCandidate> candidates,
        CancellationToken cancellationToken);
}
