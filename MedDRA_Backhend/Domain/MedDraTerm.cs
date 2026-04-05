namespace MedDRA_Backhend.Domain;

public sealed class MedDraTerm
{
    public string LltCode { get; set; } = string.Empty;

    public string LltName { get; set; } = string.Empty;

    public string PtCode { get; set; } = string.Empty;

    public string PtName { get; set; } = string.Empty;

    public string Hlt { get; set; } = string.Empty;

    public string HltCode { get; set; } = string.Empty;

    public string Hglt { get; set; } = string.Empty;

    public string HgltCode { get; set; } = string.Empty;

    public string Soc { get; set; } = string.Empty;

    public string SocCode { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool IsCurrent { get; set; } = true;
}
