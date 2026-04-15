using System.ComponentModel.DataAnnotations;

namespace BordGameSpace.DTOs;

public class EditMemberDto
{
    public int Id { get; set; }

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

    public DateTime? Birthday { get; set; }

    public int LevelId { get; set; }

    public bool Status { get; set; }
}