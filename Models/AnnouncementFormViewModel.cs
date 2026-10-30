using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class AnnouncementFormViewModel
{
    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;

    public int[] DivisionIds { get; set; } = [];

    public IReadOnlyList<Division> Divisions { get; init; } = [];
}
