namespace MedDRA_Backhend.Contracts.Files;

public sealed class UploadPreviewResponse
{
    public string FileName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public List<UploadPreviewRowDto> Rows { get; set; } = [];
}
