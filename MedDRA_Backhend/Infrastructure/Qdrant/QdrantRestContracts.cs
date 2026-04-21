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

public sealed class QdrantScrollRequest
{
    [JsonPropertyName("filter")]
    public QdrantFilter Filter { get; set; } = new();

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("with_payload")]
    public bool WithPayload { get; set; } = true;

    [JsonPropertyName("with_vector")]
    public bool WithVector { get; set; }
}

public sealed class QdrantFilter
{
    [JsonPropertyName("must")]
    public List<QdrantFieldCondition> Must { get; set; } = [];
}

public sealed class QdrantFieldCondition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("match")]
    public QdrantMatch Match { get; set; } = new();
}

public sealed class QdrantMatch
{
    [JsonPropertyName("value")]
    public object? Value { get; set; }
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

public sealed class QdrantScrollResponse
{
    [JsonPropertyName("result")]
    public QdrantScrollResult Result { get; set; } = new();
}

public sealed class QdrantScrollResult
{
    [JsonPropertyName("points")]
    public List<QdrantScrollPoint> Points { get; set; } = [];
}

public sealed class QdrantScrollPoint
{
    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; set; } = [];
}
