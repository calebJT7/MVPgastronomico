using System.Security.Claims;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var businessId = context.User.GetBusinessId();
            var userId = context.User.GetUserId();
            var role = context.User.FindFirstValue(ClaimTypes.Role) ?? UserRoleNames.Employee;
            if (businessId > 0 && userId > 0)
                tenant.Set(businessId, userId, role);
        }

        await _next(context);
    }
}

public static class UserRoleNames
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
}
