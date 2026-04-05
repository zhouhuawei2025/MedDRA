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

            //DashScopeEmbeddingService 具有实现文本向量化的函数
            var vector = await _embeddingService.GenerateAsync(term, cancellationToken);
            
            //QdrantSearchService 具备通过相似度匹配返回最合适的前10个结果的函数
            var searchResults = await _qdrantSearchService.SearchAsync(
                request.Version,
                vector,
                _encodingOptions.SearchLimit,
                _encodingOptions.OnlyCurrentTerms,
                cancellationToken);

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
                //DashScopeAiRerankService 具备LLM判断的函数（10个里面挑3个）
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
