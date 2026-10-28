namespace ITClubOrg.Models;

public sealed class ReportTable
{
    public IReadOnlyList<string> Headers { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}
