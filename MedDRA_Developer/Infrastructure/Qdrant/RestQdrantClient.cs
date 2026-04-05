using System.Net.Http.Json;
using System.Text.Json;

namespace MedDRA_Developer.Infrastructure.Qdrant;

internal sealed class RestQdrantClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public RestQdrantClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CollectionExistsAsync(string collectionName)
    {
        using var response = await _httpClient.GetAsync($"/collections/{collectionName}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<ulong> GetVectorSizeAsync(string collectionName)
    {
        var response = await _httpClient.GetFromJsonAsync<QdrantCollectionResponse>($"/collections/{collectionName}", JsonOptions)
            ?? throw new InvalidOperationException("未获取到 collection 信息。");

        return response.Result.Config.Params.Vectors.Size;
    }

    public async Task DeleteCollectionAsync(string collectionName)
    {
        using var response = await _httpClient.DeleteAsync($"/collections/{collectionName}");
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateCollectionAsync(string collectionName, ulong vectorSize)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{collectionName}",
            new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = "Cosine"
                }
            },
            JsonOptions);

        response.EnsureSuccessStatusCode();
    }

    public async Task UpsertAsync(string collectionName, IReadOnlyCollection<QdrantPoint> points)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{collectionName}/points",
            new
            {
                points
            },
            JsonOptions);

        response.EnsureSuccessStatusCode();
    }
}
