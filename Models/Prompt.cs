//Modelo de prompt de usuario
using System.ComponentModel.DataAnnotations;
namespace SCIABackendDemo.Models;

public class Prompt
{
    public int Id { get; set; }

   [MaxLength(1000)]
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
