using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class GameRental
{
    [Key]
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int ProductId { get; set; }

    public DateTime BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    [Column(TypeName = "decimal(10,0)")]
    public decimal Deposit { get; set; } = 0;

    [Column(TypeName = "decimal(10,0)")]
    public decimal RentalFee { get; set; } = 0;

    [MaxLength(50)]
    public string Status { get; set; } = "Borrowed"; // Borrowed/Renewed/Returned/Overdue

    public int? OrderId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Member Member { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Order? Order { get; set; }
}
