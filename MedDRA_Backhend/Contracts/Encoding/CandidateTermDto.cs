namespace MedDRA_Backhend.Contracts.Encoding;

public sealed class CandidateTermDto
{
    public int Rank { get; set; }

    public string LltCode { get; set; } = string.Empty;

    public string LltName { get; set; } = string.Empty;

    public string PtCode { get; set; } = string.Empty;

    public string PtName { get; set; } = string.Empty;

    public string HltCode { get; set; } = string.Empty;

    public string HltName { get; set; } = string.Empty;

    public string HgltCode { get; set; } = string.Empty;

    public string HgltName { get; set; } = string.Empty;

    public string SocCode { get; set; } = string.Empty;

    public string SocName { get; set; } = string.Empty;

    public float Score { get; set; }
}
