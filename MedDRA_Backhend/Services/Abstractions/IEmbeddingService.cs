namespace MedDRA_Backhend.Services.Abstractions;

public interface IEmbeddingService
{
    Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken);
}
