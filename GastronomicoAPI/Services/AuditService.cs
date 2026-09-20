using RotiseriaAPI.Models;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, string? entityId = null, object? metadata = null);
}

public class AuditService : IAuditService
{
    private readonly Data.AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;

    public AuditService(Data.AppDbContext db, ITenantContext tenant, IHttpContextAccessor http)
    {
        _db = db;
        _tenant = tenant;
        _http = http;
    }

    public async Task LogAsync(string action, string entityType, string? entityId = null, object? metadata = null)
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLog
        {
            BusinessId = _tenant.BusinessId,
            UserId = _tenant.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = metadata == null ? null : System.Text.Json.JsonSerializer.Serialize(metadata),
            IpAddress = ip,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
