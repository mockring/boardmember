using System.ComponentModel.DataAnnotations;

namespace BordGameSpace.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "姓名為必填")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "電話為必填")]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email為必填")]
    [EmailAddress(ErrorMessage = "Email格式不正確")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "密碼為必填")]
    [MinLength(6, ErrorMessage = "密碼至少6個字元")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "確認密碼與密碼不符")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public DateTime? Birthday { get; set; }
}
