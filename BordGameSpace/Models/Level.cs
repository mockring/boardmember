using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class Level
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal UpgradeThresholdHours { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal UpgradeThresholdAmount { get; set; } = 0;

    [Column(TypeName = "decimal(3,2)")]
    public decimal GameDiscount { get; set; } = 1.0m;

    [Column(TypeName = "decimal(10,0)")]
    public decimal WeekdayHourlyRate { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal HolidayHourlyRate { get; set; } = 0;

    public int SortOrder { get; set; } = 0;

    public bool IsDefault { get; set; } = false;

    public bool IsDeletable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public ICollection<Member> Members { get; set; } = new List<Member>();
}
