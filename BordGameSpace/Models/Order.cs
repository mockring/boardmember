using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class Order
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string OrderType { get; set; } = string.Empty; // Product/Play/Space/GameRental

    public int? MemberId { get; set; }

    [MaxLength(100)]
    public string? MemberName { get; set; }

    [MaxLength(20)]
    public string? MemberPhone { get; set; }

    [Column(TypeName = "decimal(10,0)")]
    public decimal TotalAmount { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal FinalAmount { get; set; } = 0;

    public int PointsUsed { get; set; } = 0;

    public int PointsEarned { get; set; } = 0;

    public int? CouponId { get; set; }

    [MaxLength(50)]
    public string PaymentStatus { get; set; } = "Paid"; // Paid/Unpaid

    [MaxLength(50)]
    public string? PaymentMethod { get; set; } // Cash/Transfer

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Member? Member { get; set; }
    public Coupon? Coupon { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}
