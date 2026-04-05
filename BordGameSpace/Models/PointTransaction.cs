using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class PointTransaction
{
    [Key]
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int? OrderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // Earn/Redeem/Expire/Adjust

    public int Points { get; set; } = 0;

    public int Balance { get; set; } = 0;

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Member Member { get; set; } = null!;
    public Order? Order { get; set; }
}
