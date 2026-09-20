using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Services;

public class SubscriptionAccessService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public SubscriptionAccessService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Subscription?> GetCurrentAsync()
    {
        if (!_tenant.BusinessId.HasValue)
            return null;

        var sub = await _db.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
                .ThenInclude(p => p!.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(s => s.BusinessId == _tenant.BusinessId.Value);

        if (sub != null)
        {
            ApplyExpiration(sub);
        }

        return sub;
    }

    public async Task EnsureCanOperateAsync()
    {
        if (_tenant.Role == UserRoleNames.Tester)
            return;

        var sub = await GetCurrentAsync();
        if (sub == null)
            throw new SubscriptionBlockedException("El negocio no tiene una suscripción activa.");

        if (sub.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new SubscriptionBlockedException("La suscripción está suspendida o vencida. Regularizá la cuenta para continuar operando.");
    }

    public async Task<bool> HasFeatureAsync(string featureCode)
    {
        var sub = await GetCurrentAsync();
        if (sub?.Plan == null) return false;
        if (sub.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            return false;

        return sub.Plan.PlanFeatures.Any(pf => pf.Feature != null && pf.Feature.Code.Equals(featureCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task EnsureFeatureAsync(string featureCode)
    {
        if (_tenant.Role == UserRoleNames.Tester)
            return;

        await EnsureCanOperateAsync();
        var sub = await GetCurrentAsync();
        if (sub?.Plan == null)
            throw new FeatureLockedException(featureCode, "No se encontró el plan de suscripción.");

        var hasFeat = sub.Plan.PlanFeatures.Any(pf => pf.Feature != null && pf.Feature.Code.Equals(featureCode, StringComparison.OrdinalIgnoreCase));
        if (!hasFeat)
        {
            throw new FeatureLockedException(featureCode, $"La función '{featureCode}' está disponible únicamente en el Plan Premium.");
        }
    }

    public async Task EnsureLimitAsync(string resource, int currentCount)
    {
        if (_tenant.Role == UserRoleNames.Tester)
            return;

        await EnsureCanOperateAsync();
        var sub = await GetCurrentAsync();
        var plan = sub?.Plan;
        if (plan == null) return;

        var max = resource.ToLower() switch
        {
            "users" => plan.MaxUsers,
            "products" => plan.MaxProducts,
            "tables" => plan.MaxTables,
            "customers" => plan.MaxCustomers,
            "branches" => plan.MaxBranches,
            _ => int.MaxValue
        };

        if (currentCount >= max)
            throw new PlanLimitException($"Has alcanzado el límite máximo de {resource} ({max}) permitido por tu plan actual ({plan.Name}).");
    }

    public void ApplyExpiration(Subscription sub)
    {
        var now = DateTime.UtcNow;
        if (sub.Status == SubscriptionStatus.Trial && sub.TrialEndsAtUtc.HasValue && sub.TrialEndsAtUtc <= now)
        {
            sub.Status = SubscriptionStatus.Expired;
        }
        else if (sub.Status == SubscriptionStatus.Active && sub.CurrentPeriodEndUtc.HasValue && sub.CurrentPeriodEndUtc <= now)
        {
            sub.Status = SubscriptionStatus.PastDue;
        }
    }

    public async Task<List<string>> GetEnabledFeatureCodesAsync()
    {
        var sub = await GetCurrentAsync();
        if (sub?.Plan == null) return new();
        if (sub.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            return new();

        return sub.Plan.PlanFeatures
            .Where(pf => pf.Feature != null)
            .Select(pf => pf.Feature!.Code)
            .ToList();
    }
}

public class SubscriptionBlockedException : Exception
{
    public SubscriptionBlockedException(string message) : base(message) { }
}

public class PlanLimitException : Exception
{
    public PlanLimitException(string message) : base(message) { }
}

public class FeatureLockedException : Exception
{
    public string FeatureCode { get; }
    public FeatureLockedException(string featureCode, string message) : base(message)
    {
        FeatureCode = featureCode;
    }
}
