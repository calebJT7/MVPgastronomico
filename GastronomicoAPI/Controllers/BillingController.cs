using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.DTOs;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/billing")]
[ApiController]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;
    private readonly Services.Payment.PaymentGatewayFactory _gatewayFactory;

    public BillingController(AppDbContext db, IAuditService audit, IConfiguration config, Services.Payment.PaymentGatewayFactory gatewayFactory)
    {
        _db = db;
        _audit = audit;
        _config = config;
        _gatewayFactory = gatewayFactory;
    }

    [HttpPost("checkout")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Checkout([FromBody] CreateCheckoutRequest request)
    {
        var businessId = User.GetBusinessIdSafe();
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business == null) return NotFound("Negocio no encontrado.");

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId);
        if (plan == null) return NotFound("Plan no encontrado.");

        var gateway = _gatewayFactory.GetGateway(request.Provider);
        var result = await gateway.CreateSubscriptionCheckoutAsync(business, plan, request.ReturnUrl);

        if (!result.Success)
            return BadRequest(result.ErrorMessage ?? "No se pudo iniciar el checkout.");

        return Ok(result);
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans()
    {
        var plans = await _db.Plans.AsNoTracking()
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return Ok(plans.Select(p => new
        {
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.Currency,
            p.MonthlyPrice,
            PriceConfigured = p.MonthlyPrice > 0,
            p.TrialDays,
            p.MaxUsers,
            p.MaxProducts,
            p.MaxTables,
            p.MaxCustomers,
            p.MaxBranches,
            p.ReportsEnabled,
            p.PremiumFeaturesEnabled,
            Features = p.PlanFeatures.Select(pf => new
            {
                pf.Feature!.Code,
                pf.Feature.Name,
                pf.Feature.Description,
                pf.Feature.Category,
                pf.Feature.IsPremium
            }).ToList()
        }));
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> Current()
    {
        var businessId = User.GetBusinessIdSafe();
        var sub = await _db.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
                .ThenInclude(p => p!.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(s => s.BusinessId == businessId);

        if (sub == null) return NotFound("Suscripción no encontrada.");

        var productsCount = await _db.Products.CountAsync();
        var tablesCount = await _db.Tables.CountAsync();
        var usersCount = await _db.Users.CountAsync();
        var customersCount = await _db.Customers.CountAsync();

        var features = sub.Plan?.PlanFeatures
            .Where(pf => pf.Feature != null)
            .Select(pf => pf.Feature!.Code)
            .ToList() ?? new List<string>();

        return Ok(new
        {
            sub.Id,
            sub.BusinessId,
            sub.Status,
            sub.StartedAtUtc,
            sub.TrialEndsAtUtc,
            sub.CurrentPeriodStartUtc,
            sub.CurrentPeriodEndUtc,
            sub.Provider,
            plan = sub.Plan == null ? null : new
            {
                sub.Plan.Id,
                sub.Plan.Code,
                sub.Plan.Name,
                sub.Plan.Description,
                sub.Plan.MonthlyPrice,
                sub.Plan.Currency,
                sub.Plan.MaxUsers,
                sub.Plan.MaxProducts,
                sub.Plan.MaxTables,
                sub.Plan.MaxCustomers,
                sub.Plan.MaxBranches,
                sub.Plan.PremiumFeaturesEnabled
            },
            features,
            usage = new
            {
                products = productsCount,
                tables = tablesCount,
                users = usersCount,
                customers = customersCount
            }
        });
    }

    [HttpPost("switch-plan/{planCode}")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Tester}")]
    public async Task<IActionResult> SwitchPlan(string planCode)
    {
        var businessId = User.GetBusinessIdSafe();
        var sub = await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.Plan).FirstOrDefaultAsync(s => s.BusinessId == businessId);
        if (sub == null) return NotFound("Suscripción no encontrada.");

        var newPlan = await _db.Plans.Include(p => p.PlanFeatures).FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
        if (newPlan == null) return NotFound("Plan destino no encontrado.");

        var oldPlanCode = sub.Plan?.Code ?? "none";
        sub.PlanId = newPlan.Id;
        sub.Plan = newPlan;
        sub.Status = SubscriptionStatus.Active;
        sub.CurrentPeriodStartUtc = DateTime.UtcNow;
        sub.CurrentPeriodEndUtc = DateTime.UtcNow.AddMonths(1);
        sub.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("switch_plan", "Subscription", sub.Id.ToString(), new { fromPlan = oldPlanCode, toPlan = newPlan.Code });

        return Ok(new
        {
            success = true,
            planCode = newPlan.Code,
            planName = newPlan.Name,
            status = sub.Status.ToString()
        });
    }

    [HttpPost("cancel")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Cancel()
    {
        var businessId = User.GetBusinessIdSafe();
        var sub = await _db.Subscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.BusinessId == businessId);
        if (sub == null) return NotFound();
        sub.Status = SubscriptionStatus.Cancelled;
        sub.CancelledAtUtc = DateTime.UtcNow;
        sub.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("cancel_subscription", "Subscription", sub.Id.ToString());
        return Ok(sub);
    }

    [HttpPost("reactivate")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Reactivate([FromServices] IWebHostEnvironment env)
    {
        var businessId = User.GetBusinessIdSafe();
        var sub = await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.Plan).FirstOrDefaultAsync(s => s.BusinessId == businessId);
        if (sub == null) return NotFound();

        sub.Status = SubscriptionStatus.Active;
        sub.SuspendedAtUtc = null;
        sub.CurrentPeriodStartUtc = DateTime.UtcNow;
        sub.CurrentPeriodEndUtc = DateTime.UtcNow.AddMonths(1);
        sub.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("reactivate_subscription", "Subscription", sub.Id.ToString());
        return Ok(sub);
    }

    [AllowAnonymous]
    [HttpPost("webhooks")]
    public async Task<IActionResult> Webhook([FromHeader(Name = "X-Webhook-Secret")] string? secret, [FromBody] BillingWebhookRequest request)
    {
        var expected = _config["Billing:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(expected) || secret != expected)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ProviderEventId))
            return BadRequest("ProviderEventId requerido.");

        var exists = await _db.BillingWebhookEvents.AnyAsync(e => e.Provider == request.Provider && e.ProviderEventId == request.ProviderEventId);
        if (exists) return Ok(new { duplicate = true });

        var evt = new BillingWebhookEvent
        {
            Provider = request.Provider,
            ProviderEventId = request.ProviderEventId,
            EventType = request.EventType,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(request)
        };
        _db.BillingWebhookEvents.Add(evt);

        Subscription? sub = null;
        if (!string.IsNullOrWhiteSpace(request.ProviderSubscriptionId))
            sub = await _db.Subscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ProviderSubscriptionId == request.ProviderSubscriptionId);
        if (sub == null && request.BusinessId.HasValue)
            sub = await _db.Subscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.BusinessId == request.BusinessId.Value);

        if (sub != null && Enum.TryParse<SubscriptionStatus>(request.NewStatus, true, out var status))
        {
            sub.Status = status;
            sub.UpdatedAtUtc = DateTime.UtcNow;
            if (status == SubscriptionStatus.Suspended) sub.SuspendedAtUtc = DateTime.UtcNow;
            if (status == SubscriptionStatus.Cancelled) sub.CancelledAtUtc = DateTime.UtcNow;
            if (request.PeriodEndUtc.HasValue) sub.CurrentPeriodEndUtc = request.PeriodEndUtc;
        }

        evt.ProcessedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { processed = true });
    }
}
