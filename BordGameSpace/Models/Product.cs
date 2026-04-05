using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty; // 桌遊/零食/飲料

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,0)")]
    public decimal Price { get; set; } = 0;

    public int? Stock { get; set; }

    public int LowStockAlert { get; set; } = 1;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 是否為服務項目（true = 不扣庫存，如遊玩服務、空間租借）
    /// </summary>
    public bool IsService { get; set; } = false;

    // Navigation
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<RestockRecord> RestockRecords { get; set; } = new List<RestockRecord>();
    public ICollection<GameRental> GameRentals { get; set; } = new List<GameRental>();
}
