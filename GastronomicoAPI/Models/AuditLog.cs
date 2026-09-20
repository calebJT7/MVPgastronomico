namespace RotiseriaAPI.Models;

public class AuditLog
{
    public long Id { get; set; }
    public int? BusinessId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
