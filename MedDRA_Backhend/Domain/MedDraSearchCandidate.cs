namespace MedDRA_Backhend.Domain;

public sealed class MedDraSearchCandidate
{
    public required MedDraTerm Term { get; init; }

    public float Score { get; init; }
}
