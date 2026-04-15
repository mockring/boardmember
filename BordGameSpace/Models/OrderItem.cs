using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class OrderItem
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ItemType { get; set; } = string.Empty; // Product/Play/Space/GameRental

    public int ItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,0)")]
    public decimal UnitPrice { get; set; } = 0;

    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(10,0)")]
    public decimal Subtotal { get; set; } = 0;

    // 折扣欄位（None/Percentage/FixedAmount）
    [MaxLength(50)]
    public string DiscountType { get; set; } = "None"; // None/Percentage/FixedAmount

    [Column(TypeName = "decimal(10,0)")]
    public decimal DiscountValue { get; set; } = 0;


    // CouponId：用於追蹤此項目適用的優惠券（對應 MemberCoupon）
    public int? CouponId { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public Coupon? Coupon { get; set; }
}
