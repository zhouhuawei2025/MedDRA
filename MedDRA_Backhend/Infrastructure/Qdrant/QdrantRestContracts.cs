using System.Text.Json.Serialization;

namespace MedDRA_Backhend.Infrastructure.Qdrant;

public sealed class QdrantSearchRequest
{
    [JsonPropertyName("vector")]
    public float[] Vector { get; set; } = [];

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("with_payload")]
    public bool WithPayload { get; set; } = true;
}

public sealed class QdrantSearchResponse
{
    [JsonPropertyName("result")]
    public List<QdrantSearchResultItem> Result { get; set; } = [];
}

public sealed class QdrantSearchResultItem
{
    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; set; } = [];
}
