namespace MedDRA_Backhend.Options;

public sealed class LlmOptions
{
    public const string SectionName = "LLM";

    public string Endpoint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
