namespace MedDRA_Backhend.Contracts.Files;

public sealed class UploadPreviewRowDto
{
    public int RowNumber { get; set; }

    public string RawTerm { get; set; } = string.Empty;
}
