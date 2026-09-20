using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Services;

public interface ITenantDateTimeService
{
    Task<DateTime> GetTodayStartUtcAsync();
    Task<TimeZoneInfo> GetTenantTimeZoneAsync();
    Task<DateTime> ToTenantTimeAsync(DateTime utcDateTime);
}

public class TenantDateTimeService : ITenantDateTimeService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public TenantDateTimeService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TimeZoneInfo> GetTenantTimeZoneAsync()
    {
        if (!_tenant.BusinessId.HasValue)
            return TimeZoneInfo.Utc;

        var business = await _db.Businesses.AsNoTracking()
            .Where(b => b.Id == _tenant.BusinessId.Value)
            .Select(b => new { b.TimeZoneId })
            .FirstOrDefaultAsync();

        var tzId = business?.TimeZoneId;
        if (string.IsNullOrWhiteSpace(tzId))
            tzId = "America/Argentina/Buenos_Aires";

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            // Windows fallback for common IANA timezones
            if (tzId.Contains("Buenos_Aires", StringComparison.OrdinalIgnoreCase) || tzId.Contains("Argentina", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"); } catch { }
            }

            return TimeZoneInfo.CreateCustomTimeZone("TenantTZ", TimeSpan.FromHours(-3), "Tenant Local Time", "Tenant Local Time");
        }
    }

    public async Task<DateTime> GetTodayStartUtcAsync()
    {
        var tz = await GetTenantTimeZoneAsync();
        var nowTenant = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayTenantStart = nowTenant.Date;
        return TimeZoneInfo.ConvertTimeToUtc(todayTenantStart, tz);
    }

    public async Task<DateTime> ToTenantTimeAsync(DateTime utcDateTime)
    {
        var tz = await GetTenantTimeZoneAsync();
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), tz);
    }
}
