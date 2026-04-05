namespace MedDRA_Backhend.Contracts.Encoding;

public sealed class EncodingResultDto
{
    public string RawTerm { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public float Top1Score { get; set; }

    public bool UsedAi { get; set; }

    public string Remark { get; set; } = string.Empty;

    public List<CandidateTermDto> Candidates { get; set; } = [];
}
