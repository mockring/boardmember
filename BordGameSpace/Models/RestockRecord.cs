using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class RestockRecord
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; } = 0;

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Product Product { get; set; } = null!;
}
