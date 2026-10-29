using System.ComponentModel.DataAnnotations;
namespace Coiny.API.Models;
public class AuthRecoveryCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [MaxLength(64)] public string CodeHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
