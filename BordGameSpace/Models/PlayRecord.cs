using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class PlayRecord
{
    [Key]
    public int Id { get; set; }

    public int? MemberId { get; set; }

    [MaxLength(100)]
    public string? MemberName { get; set; }

    [MaxLength(20)]
    public string? MemberPhone { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TotalHours { get; set; }

    [Column(TypeName = "decimal(10,0)")]
    public decimal HourlyRate { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal Amount { get; set; } = 0;

    public int? OrderId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Playing"; // Playing/Completed/CheckedOut

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Member? Member { get; set; }
    public Order? Order { get; set; }
}
