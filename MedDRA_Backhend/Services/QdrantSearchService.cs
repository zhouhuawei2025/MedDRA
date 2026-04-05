using System.Net.Http.Json;
using System.Text.Json;
using MedDRA_Backhend.Domain;
using MedDRA_Backhend.Infrastructure.Qdrant;
using MedDRA_Backhend.Options;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MedDRA_Backhend.Services;

public sealed class QdrantSearchService : IQdrantSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IMedDraVersionService _versionService;

    public QdrantSearchService(HttpClient httpClient, IOptions<VectorStoreOptions> options, IMedDraVersionService versionService)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.Endpoint);
        _versionService = versionService;
    }

    public async Task<IReadOnlyList<MedDraSearchCandidate>> SearchAsync(
        string version,
        float[] vector,
        int limit,
        bool onlyCurrentTerms,
        CancellationToken cancellationToken)
    {
        var collectionName = _versionService.ResolveCollectionName(version);
        // API 检索层同样走 REST，和导入器保持一致，避免 gRPC 连接差异。
        using var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/search",
            new QdrantSearchRequest
            {
                Vector = vector,
                Limit = limit,
                WithPayload = true
            },
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var searchResults = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Qdrant 检索未返回有效结果。");

        return searchResults.Result
            .Select(x => new MedDraSearchCandidate
            {
                Term = QdrantRestPayloadMapper.ToMedDraTerm(x.Payload),
                Score = x.Score
            })
            .Where(x => !onlyCurrentTerms || x.Term.IsCurrent)
            .Take(limit)
            .ToArray();
    }
}
