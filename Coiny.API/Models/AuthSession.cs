using System.ComponentModel.DataAnnotations;
namespace Coiny.API.Models;
public class AuthSession
{
    [MaxLength(36)] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    [MaxLength(256)] public string Device { get; set; } = "";
}
