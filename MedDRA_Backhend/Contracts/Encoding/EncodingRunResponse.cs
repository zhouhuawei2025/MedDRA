namespace MedDRA_Backhend.Contracts.Encoding;

public sealed class EncodingRunResponse
{
    public string Version { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public List<EncodingResultDto> Results { get; set; } = [];
}
