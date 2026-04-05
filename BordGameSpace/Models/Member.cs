using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class Member
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime? Birthday { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPlayHours { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalSpending { get; set; } = 0;

    public int LevelId { get; set; } = 2; // 預設小鳳梨

    public bool Status { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Level Level { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    public ICollection<MemberCoupon> MemberCoupons { get; set; } = new List<MemberCoupon>();
    public ICollection<PlayRecord> PlayRecords { get; set; } = new List<PlayRecord>();
    public ICollection<GameRental> GameRentals { get; set; } = new List<GameRental>();
    public ICollection<SpaceReservation> SpaceReservations { get; set; } = new List<SpaceReservation>();
}
