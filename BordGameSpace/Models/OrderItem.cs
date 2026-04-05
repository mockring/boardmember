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

    // Navigation
    public Order Order { get; set; } = null!;
}
