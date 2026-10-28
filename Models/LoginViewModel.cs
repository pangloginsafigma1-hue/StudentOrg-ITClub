using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class LoginViewModel
{
    [Required]
    [StringLength(50)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
