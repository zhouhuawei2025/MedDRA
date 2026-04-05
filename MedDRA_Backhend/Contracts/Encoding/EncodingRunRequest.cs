namespace MedDRA_Backhend.Contracts.Encoding;

public sealed class EncodingRunRequest
{
    public string Version { get; set; } = string.Empty;

    public float? HighConfidenceThreshold { get; set; }

    public float? MinimumScoreGap { get; set; }

    public List<string> Terms { get; set; } = [];
}
