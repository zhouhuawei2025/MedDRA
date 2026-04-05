using System.Text.Json.Serialization;

namespace MedDRA_Backhend.Infrastructure.AI;

public sealed class AiCandidateSelectionResponse
{
    [JsonPropertyName("candidates")]
    public List<AiCandidateSelectionItem> Candidates { get; set; } = [];

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AiCandidateSelectionItem
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("lltCode")]
    public string LltCode { get; set; } = string.Empty;
}
