using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class SpaceReservation
{
    [Key]
    public int Id { get; set; }

    public int? MemberId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    public DateTime ReservationDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public int PeopleCount { get; set; } = 2;

    [MaxLength(20)]
    public string SpaceType { get; set; } = "訂位";

    public int? Hours { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal? HourlyRate { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal? TotalAmount { get; set; } = 0;

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending/Approved/Rejected/Cancelled

    public int? OrderId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Member? Member { get; set; } = null!;
    public Order? Order { get; set; }
}
