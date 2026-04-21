using System.Text;
using MedDRA_Backhend.Domain;
using MedDRA_Backhend.Infrastructure.AI;
using MedDRA_Backhend.Options;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;

namespace MedDRA_Backhend.Services;

public sealed class DashScopeAiRerankService : IAiRerankService
{
    private readonly IChatClient _chatClient;
    private readonly LlmOptions _options;

    public DashScopeAiRerankService(IOptions<LlmOptions> options)
    {
        _options = options.Value;

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(_options.Endpoint)
        };

        _chatClient = new ChatClient(
            _options.Model,
            new ApiKeyCredential(_options.ApiKey),
            clientOptions).AsIChatClient();
    }

    public async Task<(IReadOnlyList<MedDraSearchCandidate> Candidates, string Reason)> RerankAsync(
        string rawTerm,
        IReadOnlyList<MedDraSearchCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return (candidates.Take(3).ToArray(), "未配置 LLM 或候选为空，直接返回检索结果。");
        }

        var prompt = BuildPrompt(rawTerm, candidates);
        ChatResponse chatResponse;
        try
        {
            // 模型只负责在候选池内重排，不让它自由生成新的 MedDRA 编码。
            //模型只输出LLTCode，后续会再做map
            chatResponse = await _chatClient.GetResponseAsync(
                [
                    new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, "你是医学词典编码助手，需要对待编码字段选择合适的候选编码词条。请只从给定候选中选择最优、次优、较优 3 条，返回纯 JSON。" +
                                                                             "注意：1. 如果发现候选列表的匹配度很低，难以抉择，依旧选择最可能的前3条，严禁虚构不存在的编码；" +
                                                                             "2. 优先选择PT和待编码术语相同，或LLT和和待编码术语相同的结果；" +
                                                                             "3. 如果存在多个PT-LLT相同的结果，优先选择PTcode和LLTcode一致的结果"),
                    new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt)
                ],
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return (candidates.Take(3).ToArray(), $"LLM 调用失败：{ex.Message}");
        }

        var content = chatResponse.Text;
        if (string.IsNullOrWhiteSpace(content))
        {
            return (candidates.Take(3).ToArray(), "LLM 未返回有效内容，直接使用检索结果。");
        }

        if (AiJsonParser.TryDeserializeFromAiText<AiCandidateSelectionResponse>(content, out var parsed) == false ||
            parsed?.Candidates.Count is not > 0)
        {
            return (candidates.Take(3).ToArray(), "LLM 返回格式不正确，直接使用检索结果。");
        }

        // 最终仍以本地候选池为准，避免模型虚构不存在的编码。
        var candidateMap = candidates.ToDictionary(x => x.Term.LltCode, StringComparer.OrdinalIgnoreCase);
        var reranked = parsed.Candidates
            .OrderBy(x => x.Rank)
            .Where(x => candidateMap.ContainsKey(x.LltCode))
            .Select(x => candidateMap[x.LltCode])
            .Take(3)
            .ToList();

        if (reranked.Count == 0)
        {
            return (candidates.Take(3).ToArray(), "LLM 未选中有效候选，直接使用检索结果。");
        }

        return (reranked, parsed.Reason);
    }

    private static string BuildPrompt(string rawTerm, IReadOnlyList<MedDraSearchCandidate> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"待编码术语：{rawTerm}");
        sb.AppendLine("候选列表：");

        for (var i = 0; i < candidates.Count; i++)
        {
            var item = candidates[i];
            sb.AppendLine(
                $"{i + 1}. LLT={item.Term.LltName}({item.Term.LltCode}), PT={item.Term.PtName}({item.Term.PtCode}), HLT={item.Term.Hlt}({item.Term.HltCode}), HLGT={item.Term.Hglt}({item.Term.HgltCode}), SOC={item.Term.Soc}({item.Term.SocCode}), Score={item.Score:F4}");
        }

        sb.AppendLine("请只返回如下 JSON：");
        sb.AppendLine("{\"candidates\":[{\"rank\":1,\"lltCode\":\"...\"},{\"rank\":2,\"lltCode\":\"...\"},{\"rank\":3,\"lltCode\":\"...\"}],\"reason\":\"...\"}");
        Console.WriteLine(sb.ToString());
        return sb.ToString();
    }
}
