namespace MedDRA_Backhend.Services.Abstractions;

public interface IMedDraVersionService
{
    IReadOnlyCollection<(string Version, string CollectionName)> GetAvailableVersions();

    string ResolveCollectionName(string version);
}
