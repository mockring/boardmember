using System.ComponentModel.DataAnnotations;

namespace BordGameSpace.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "請輸入電話或Email")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
}
