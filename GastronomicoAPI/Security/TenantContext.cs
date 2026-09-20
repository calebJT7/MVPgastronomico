namespace RotiseriaAPI.Security;

public interface ITenantContext
{
    int? BusinessId { get; }
    int? UserId { get; }
    string? Role { get; }
    bool BypassFilters { get; }
    void Set(int businessId, int userId, string role);
    void EnableBypass();
    void Clear();
}

public sealed class TenantContext : ITenantContext
{
    public int? BusinessId { get; private set; }
    public int? UserId { get; private set; }
    public string? Role { get; private set; }
    public bool BypassFilters { get; private set; }

    public void Set(int businessId, int userId, string role)
    {
        BusinessId = businessId;
        UserId = userId;
        Role = role;
        BypassFilters = false;
    }

    public void EnableBypass()
    {
        BypassFilters = true;
    }

    public void Clear()
    {
        BusinessId = null;
        UserId = null;
        Role = null;
        BypassFilters = false;
    }
}
