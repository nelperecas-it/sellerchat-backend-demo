//Modelo de usuarios
using System.ComponentModel.DataAnnotations;

namespace SCIABackendDemo.Models;

public class User
{
    public int Id { get; set; }

    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [MaxLength(255)]
    public string Nombre { get; set; } = null!;

    [MaxLength(20)]
    public string Telefono { get; set; } = null!;

    public ICollection<CallHistory> CallHistories { get; set; } = new List<CallHistory>();
    public ICollection<Prompt> Prompts { get; set; } = new List<Prompt>();

    public bool AutoTriggerEnabled { get; set; } = false;
    public int AutoTriggerDays { get; set; } = 15;

}
