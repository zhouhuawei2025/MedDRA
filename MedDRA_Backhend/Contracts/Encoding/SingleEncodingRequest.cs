namespace MedDRA_Backhend.Contracts.Encoding;

public sealed class SingleEncodingRequest
{
    public string Version { get; set; } = string.Empty;

    public string Term { get; set; } = string.Empty;

    public float? HighConfidenceThreshold { get; set; }

    public float? MinimumScoreGap { get; set; }
}
