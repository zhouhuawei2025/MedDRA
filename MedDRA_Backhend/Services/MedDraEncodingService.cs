using MedDRA_Backhend.Contracts.Encoding;
using MedDRA_Backhend.Domain;
using MedDRA_Backhend.Options;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MedDRA_Backhend.Services;

public sealed class MedDraEncodingService : IMedDraEncodingService
{
    private readonly IAiRerankService _aiRerankService;
    private readonly IEmbeddingService _embeddingService;
    private readonly EncodingOptions _encodingOptions;
    private readonly IQdrantSearchService _qdrantSearchService;

    public MedDraEncodingService(IEmbeddingService embeddingService, IQdrantSearchService qdrantSearchService,
        IAiRerankService aiRerankService, IOptions<EncodingOptions> encodingOptions)
    {
        _embeddingService = embeddingService;
        _qdrantSearchService = qdrantSearchService;
        _aiRerankService = aiRerankService;
        _encodingOptions = encodingOptions.Value;
    }

    // 后端编码主流程：接收前端提交的待编码词条列表；先生成查询向量，再从 Qdrant 召回候选，必要时调用 LLM 重排，最后返回候选编码结果。 
    public async Task<EncodingRunResponse> RunAsync(EncodingRunRequest request, CancellationToken cancellationToken)
    {
        if (request.Terms.Count == 0)
        {
            return new EncodingRunResponse
            {
                Version = request.Version,
                TotalCount = 0
            };
        }

        var threshold = request.HighConfidenceThreshold ?? _encodingOptions.HighConfidenceThreshold;
        var scoreGap = request.MinimumScoreGap ?? _encodingOptions.MinimumScoreGap;

        var results = new List<EncodingResultDto>(request.Terms.Count);
        foreach (var term in request.Terms.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Exact LLT match is checked first so standard MedDRA terms like "发热" are not lost by semantic retrieval.
            var exactMatches = await _qdrantSearchService.ExactMatchByLltNameAsync(
                request.Version,
                term,
                _encodingOptions.SearchLimit,
                _encodingOptions.OnlyCurrentTerms,
                cancellationToken);
            Console.WriteLine($"[编码流程] 待编码术语='{term}'：LLT 名称精确匹配命中 {exactMatches.Count} 条，会优先放入候选列表。");

            //DashScopeEmbeddingService 具有实现文本向量化的函数
            var vector = await _embeddingService.GenerateAsync(term, cancellationToken);
            
            // QdrantSearchService retrieves a wider semantic candidate pool; exact matches are merged ahead of it below.
            var vectorSearchResults = await _qdrantSearchService.SearchAsync(
                request.Version,
                vector,
                _encodingOptions.SearchLimit,
                _encodingOptions.OnlyCurrentTerms,
                cancellationToken);
            Console.WriteLine($"[编码流程] 待编码术语='{term}'：向量相似度检索返回 {vectorSearchResults.Count} 条候选，配置的检索上限 SearchLimit={_encodingOptions.SearchLimit}。");

            var searchResults = MergeCandidates(exactMatches, vectorSearchResults, _encodingOptions.SearchLimit);
            Console.WriteLine($"[编码流程] 待编码术语='{term}'：合并规则为“精确匹配优先 + 向量候选补充 + 按 LLTCode 去重”，合并后进入后续判断/AI重排的候选数={searchResults.Count}。");

            if (searchResults.Count == 0)
            {
                results.Add(new EncodingResultDto
                {
                    RawTerm = term,
                    Version = request.Version,
                    Remark = "未检索到候选项。"
                });
                continue;
            }

            // 高置信命中直接返回，只有边界场景才交给 LLM 做候选重排。
            var usedAi = ShouldUseAi(searchResults, threshold, scoreGap) == false;
            var finalCandidates = searchResults.Take(3).ToArray();
            var remark = usedAi ? string.Empty : "高置信命中，未调用 AI。";

            if (usedAi)
            {
                //DashScopeAiRerankService 具备LLM判断的函数（15个里面挑3个）
                (finalCandidates, remark) = await RerankWithAiAsync(term, searchResults, cancellationToken);
            }

            results.Add(new EncodingResultDto
            {
                RawTerm = term,
                Version = request.Version,
                Top1Score = searchResults[0].Score,
                UsedAi = usedAi,
                Remark = remark,
                Candidates = finalCandidates
                    .Select((x, index) => ToCandidateDto(x, index + 1))
                    .ToList()
            });
        }

        return new EncodingRunResponse
        {
            Version = request.Version,
            TotalCount = results.Count,
            Results = results
        };
    }

    private async Task<(MedDraSearchCandidate[] Candidates, string Remark)> RerankWithAiAsync(
        string rawTerm,
        IReadOnlyList<MedDraSearchCandidate> searchResults,
        CancellationToken cancellationToken)
    {
        var (candidates, reason) = await _aiRerankService.RerankAsync(rawTerm, searchResults, cancellationToken);
        return (candidates.Take(3).ToArray(), reason);
    }

    private static IReadOnlyList<MedDraSearchCandidate> MergeCandidates(
        IReadOnlyList<MedDraSearchCandidate> exactMatches,
        IReadOnlyList<MedDraSearchCandidate> vectorSearchResults,
        int limit)
    {
        var merged = new List<MedDraSearchCandidate>(exactMatches.Count + vectorSearchResults.Count);
        var seenLltCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 合并顺序很重要：先放入 LLT 名称精确匹配结果，再补充向量相似度结果。
        // 因为 exactMatches 排在前面，即使合并后的总数达到 limit，优先保留的也是精确匹配结果；
        // 被截断的只会是排在后面的向量候选，用来避免候选池过长导致后续 AI prompt 过大。
        foreach (var candidate in exactMatches.Concat(vectorSearchResults))
        {
            if (string.IsNullOrWhiteSpace(candidate.Term.LltCode))
            {
                continue;
            }

            // 同一个 LLTCode 可能既被精确匹配命中，又出现在向量检索结果中，这里只保留第一次出现的候选。
            if (!seenLltCodes.Add(candidate.Term.LltCode))
            {
                continue;
            }

            merged.Add(candidate);
            if (merged.Count >= limit)
            {
                break;
            }
        }

        return merged;
    }

    private static bool ShouldUseAi(IReadOnlyList<MedDraSearchCandidate> candidates, float threshold, float minimumScoreGap)
    {
        var top1 = candidates[0].Score;
        var top2 = candidates.Count > 1 ? candidates[1].Score : 0f;
        return top1 >= threshold && top1 - top2 >= minimumScoreGap;
    }

    private static CandidateTermDto ToCandidateDto(MedDraSearchCandidate candidate, int rank)
    {
        return new CandidateTermDto
        {
            Rank = rank,
            LltCode = candidate.Term.LltCode,
            LltName = candidate.Term.LltName,
            PtCode = candidate.Term.PtCode,
            PtName = candidate.Term.PtName,
            HltCode = candidate.Term.HltCode,
            HltName = candidate.Term.Hlt,
            HgltCode = candidate.Term.HgltCode,
            HgltName = candidate.Term.Hglt,
            SocCode = candidate.Term.SocCode,
            SocName = candidate.Term.Soc,
            Score = candidate.Score
        };
    }
}
