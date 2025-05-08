//Modelo de historial de llamdas
using System.ComponentModel.DataAnnotations;
namespace SCIABackendDemo.Models;

public class CallHistory
{
    public int Id { get; set; }
    public string CallId { get; set; } = null!;

    [MaxLength(20)]
    public string? From { get; set; }
    [MaxLength(20)]
    public string? To { get; set; }
    [MaxLength(20)]
    public string? Direction { get; set; }


    [MaxLength(1000)]
    public string? Summary { get; set; }
    
    [MaxLength(1000)]
    public string? ShortSummary { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public bool IsActive { get; set; } = false;
}
