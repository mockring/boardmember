using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class MemberCoupon
{
    [Key]
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int CouponId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    public DateTime? UsedAt { get; set; }

    public int? OrderId { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;
    public Coupon Coupon { get; set; } = null!;
    public Order? Order { get; set; }
}
