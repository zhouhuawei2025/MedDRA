namespace MedDRA_Backhend.Options;

public sealed class EncodingOptions
{
    public const string SectionName = "Encoding";

    public float HighConfidenceThreshold { get; set; } = 0.95f;

    public float MinimumScoreGap { get; set; } = 0.03f;

    public int SearchLimit { get; set; } = 10;

    public bool OnlyCurrentTerms { get; set; } = true;
}
