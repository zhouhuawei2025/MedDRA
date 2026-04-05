namespace MedDRA_Developer.Infrastructure.Qdrant;

internal sealed class QdrantPoint
{
    public ulong Id { get; set; }

    public float[] Vector { get; set; } = [];

    public Dictionary<string, object> Payload { get; set; } = [];
}
