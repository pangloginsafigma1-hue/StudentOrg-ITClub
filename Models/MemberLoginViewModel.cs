using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class MemberLoginViewModel
{
    [Required]
    [StringLength(20)]
    [Display(Name = "Student ID")]
    public string MemberId { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
