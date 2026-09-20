using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Data;

public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenant;

    public TenantSaveChangesInterceptor(ITenantContext tenant) => _tenant = tenant;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context == null || !_tenant.BusinessId.HasValue)
            return;

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.BusinessId == 0)
                entry.Entity.BusinessId = _tenant.BusinessId.Value;

            if (entry.State is EntityState.Modified or EntityState.Added)
            {
                if (entry.Entity.BusinessId != _tenant.BusinessId.Value && !_tenant.BypassFilters)
                    throw new InvalidOperationException("No se puede modificar un recurso de otro negocio.");
            }
        }
    }
}
