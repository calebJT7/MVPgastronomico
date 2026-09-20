using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.DTOs;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class BusinessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public BusinessController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var id = User.GetBusinessIdSafe();
        var business = await _db.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (business == null) return NotFound();
        return Ok(business);
    }

    [HttpPut]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Update(BusinessUpdateRequest request)
    {
        var id = User.GetBusinessIdSafe();
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == id);
        if (business == null) return NotFound();

        business.TradeName = request.TradeName.Trim();
        business.LegalName = request.LegalName;
        business.TaxId = request.TaxId;
        business.Address = request.Address;
        business.Phone = request.Phone;
        business.Email = request.Email;
        business.LogoUrl = request.LogoUrl;
        business.OpeningHoursJson = request.OpeningHoursJson;
        business.PreferencesJson = request.PreferencesJson;
        business.BusinessType = request.BusinessType;
        business.TablesEnabled = request.TablesEnabled;
        business.DeliveryEnabled = request.DeliveryEnabled;
        business.TakeawayEnabled = request.TakeawayEnabled;
        business.KitchenEnabled = request.KitchenEnabled;
        business.ReservationsEnabled = request.ReservationsEnabled;
        business.CashControlEnabled = request.CashControlEnabled;
        business.InventoryEnabled = request.InventoryEnabled;
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
            business.TimeZoneId = request.TimeZoneId.Trim();
        business.UpdatedAtUtc = DateTime.UtcNow;
        if (!business.OnboardingCompleted && business.OnboardingStep == Models.OnboardingStep.BusinessProfile)
            business.OnboardingStep = Models.OnboardingStep.Categories;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("update_business", "Business", business.Id.ToString());
        return Ok(business);
    }

    [HttpPost("onboarding/complete")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Tester}")]
    public async Task<IActionResult> CompleteOnboarding()
    {
        var id = User.GetBusinessIdSafe();
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == id);
        if (business == null) return NotFound();
        business.OnboardingCompleted = true;
        business.OnboardingStep = Models.OnboardingStep.Completed;
        business.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(business);
    }
}

public static class UserClaimHelper
{
    public static int GetBusinessIdSafe(this System.Security.Claims.ClaimsPrincipal user)
        => Security.ClaimsPrincipalExtensions.GetBusinessId(user);
}
