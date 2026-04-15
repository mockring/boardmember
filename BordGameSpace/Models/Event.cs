using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BordGameSpace.Models;

public class Event
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime EventDate { get; set; }

    public DateTime RegistrationDeadline { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "RegistrationOpen"; // RegistrationOpen/RegistrationClosed/Ended

    public int? MaxParticipants { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Navigation
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
}