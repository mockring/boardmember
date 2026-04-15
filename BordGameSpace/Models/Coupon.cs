using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class Coupon
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CouponType { get; set; } = string.Empty; // FixedAmount/Percentage

    [Column(TypeName = "decimal(10,0)")]
    public decimal DiscountValue { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal MinPurchase { get; set; } = 0;

    [MaxLength(50)]
    public string ApplicableTo { get; set; } = "All"; // All/Product/Play/Space

    public int? TotalQuantity { get; set; } = null; // null = 不限張數, 0 = 已用完, >0 = 限張數

    public int UsedCount { get; set; } = 0;

    public DateTime ValidFrom { get; set; }


    public DateTime? ValidUntil { get; set; } // null = 不限時間

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public ICollection<MemberCoupon> MemberCoupons { get; set; } = new List<MemberCoupon>();
}
