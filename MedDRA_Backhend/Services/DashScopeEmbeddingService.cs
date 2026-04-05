using System.ClientModel;
using MedDRA_Backhend.Options;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace MedDRA_Backhend.Services;

public sealed class DashScopeEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly EmbeddingOptions _options;

    public DashScopeEmbeddingService(IOptions<EmbeddingOptions> options)
    {
        _options = options.Value;

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(_options.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(_options.Endpoint)
            });

        var embeddingClient = openAiClient.GetEmbeddingClient(_options.Model);
        _embeddingGenerator = embeddingClient.AsIEmbeddingGenerator();
    }

    public async Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        // Embedding 输入统一走业务侧准备好的文本，不在这里做额外清洗或拼接。
        var embedding = await _embeddingGenerator.GenerateAsync(input, cancellationToken: cancellationToken);
        return embedding.Vector.ToArray();
    }
}
