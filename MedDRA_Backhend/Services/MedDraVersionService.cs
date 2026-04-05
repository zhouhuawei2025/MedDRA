using MedDRA_Backhend.Options;
using MedDRA_Backhend.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MedDRA_Backhend.Services;

public sealed class MedDraVersionService : IMedDraVersionService
{
    private readonly VectorStoreOptions _options;

    public MedDraVersionService(IOptions<VectorStoreOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyCollection<(string Version, string CollectionName)> GetAvailableVersions()
    {
        if (_options.Collections.Count == 0)
        {
            return [("28.1", _options.DefaultCollectionName)];
        }

        return _options.Collections
            .Select(x => (x.Key, x.Value))
            .ToArray();
    }

    public string ResolveCollectionName(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return _options.DefaultCollectionName;
        }

        if (_options.Collections.TryGetValue(version, out var configured))
        {
            return configured;
        }

        return $"meddra_{version.Replace('.', '_')}";
    }
}
