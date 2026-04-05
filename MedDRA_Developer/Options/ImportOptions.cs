namespace MedDRA_Developer.Options;

internal sealed class ImportOptions
{
    public string FilePath { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;

    public bool UseHierarchyInSearchText { get; set; } = true;

    public int BatchSize { get; set; } = 100;

    public bool RecreateCollectionIfVectorSizeChanged { get; set; } = true;
}
