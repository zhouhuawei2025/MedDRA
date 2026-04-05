namespace MedDRA_Developer.Infrastructure.Qdrant;

internal sealed class QdrantCollectionResponse
{
    public QdrantCollectionResult Result { get; set; } = new();
}

internal sealed class QdrantCollectionResult
{
    public QdrantCollectionConfig Config { get; set; } = new();
}

internal sealed class QdrantCollectionConfig
{
    public QdrantCollectionParams Params { get; set; } = new();
}

internal sealed class QdrantCollectionParams
{
    public QdrantVectorParams Vectors { get; set; } = new();
}

internal sealed class QdrantVectorParams
{
    public ulong Size { get; set; }
}
