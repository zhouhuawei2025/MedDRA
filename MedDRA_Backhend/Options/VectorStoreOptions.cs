namespace MedDRA_Backhend.Options;

public sealed class VectorStoreOptions
{
    public const string SectionName = "VectorStore";

    public string Endpoint { get; set; } = "http://127.0.0.1:6333";

    public string ApiKey { get; set; } = string.Empty;

    public string DefaultCollectionName { get; set; } = "meddra_26_1";

    public Dictionary<string, string> Collections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
