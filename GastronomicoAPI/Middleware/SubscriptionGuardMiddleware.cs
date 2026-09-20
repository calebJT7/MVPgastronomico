using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Middleware;

public sealed class SubscriptionGuardMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, AppDbContext db, SubscriptionAccessService access)
    {
        if (context.User.Identity?.IsAuthenticated != true || !tenant.BusinessId.HasValue)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (IsExempt(path))
        {
            await _next(context);
            return;
        }

        var sub = await db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.BusinessId == tenant.BusinessId.Value);

        if (sub != null)
            access.ApplyExpiration(sub);

        if (sub != null && sub.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Suscripción inactiva",
                status = sub.Status.ToString(),
                message = "El negocio no puede operar hasta regularizar la suscripción. Los datos se conservan."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(string path)
    {
        var p = path.ToLowerInvariant();
        return p.StartsWith("/api/auth")
            || p.StartsWith("/api/health")
            || p.StartsWith("/api/legal")
            || p.StartsWith("/api/billing")
            || p.StartsWith("/api/business")
            || p.Contains("/swagger");
    }
}
