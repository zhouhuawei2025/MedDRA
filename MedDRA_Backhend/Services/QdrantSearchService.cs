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

    public async Task<IReadOnlyList<MedDraSearchCandidate>> ExactMatchByLltNameAsync(
        string version,
        string lltName,
        int limit,
        bool onlyCurrentTerms,
        CancellationToken cancellationToken)
    {
        var collectionName = _versionService.ResolveCollectionName(version);
        var request = new QdrantScrollRequest
        {
            Limit = limit,
            WithPayload = true,
            WithVector = false,
            Filter = new QdrantFilter
            {
                Must =
                [
                    new QdrantFieldCondition
                    {
                        Key = "llt_name",
                        Match = new QdrantMatch { Value = lltName }
                    }
                ]
            }
        };

        if (onlyCurrentTerms)
        {
            request.Filter.Must.Add(new QdrantFieldCondition
            {
                Key = "is_current",
                Match = new QdrantMatch { Value = true }
            });
        }

        Console.WriteLine($"[Qdrant精确匹配] 开始按 payload 查询：条件 llt_name='{lltName}'，只查当前有效术语={onlyCurrentTerms}，最多返回={limit} 条。");
        using var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/scroll",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var scrollResults = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Qdrant 精确匹配未返回有效结果。");

        var candidates = scrollResults.Result.Points
            .Select(x => new MedDraSearchCandidate
            {
                Term = QdrantRestPayloadMapper.ToMedDraTerm(x.Payload),
                // Exact payload match is a business rule, not a vector score. Use 1.0 so it stays first after merge.
                Score = 1.0f
            })
            .Where(x => !onlyCurrentTerms || x.Term.IsCurrent)
            // When several LLTs have the same display name, prefer the canonical row where LLTCode equals PTCode.
            .OrderByDescending(x => string.Equals(x.Term.LltCode, x.Term.PtCode, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Term.LltCode, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        Console.WriteLine($"[Qdrant精确匹配] 查询完成：llt_name='{lltName}' 精确命中 {candidates.Length} 条。若存在多条同名 LLT，会优先排列 LLTCode=PTCode 的标准条目。");
        return candidates;
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
