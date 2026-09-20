using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.DTOs;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TokenService _tokens;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, ITenantContext tenant, TokenService tokens, IAuditService audit, IEmailService email, ILogger<AuthController> logger)
    {
        _db = db;
        _tenant = tenant;
        _tokens = tokens;
        _audit = audit;
        _email = email;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!request.AcceptLegal)
            return BadRequest("Debés aceptar los documentos legales vigentes para registrarte.");

        _tenant.EnableBypass();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict("Ya existe una cuenta con ese email.");

        var starter = await _db.Plans.FirstOrDefaultAsync(p => p.Code == "basic")
                      ?? await _db.Plans.FirstOrDefaultAsync(p => p.Code == "starter")
                      ?? await _db.Plans.OrderBy(p => p.SortOrder).FirstAsync();

        var business = new Business
        {
            TradeName = request.BusinessName.Trim(),
            Email = email,
            BusinessType = request.BusinessType,
            OnboardingStep = OnboardingStep.BusinessProfile,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();

        var user = new User
        {
            BusinessId = business.Id,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Owner,
            IsActive = true
        };
        _db.Users.Add(user);

        _db.Subscriptions.Add(new Subscription
        {
            BusinessId = business.Id,
            PlanId = starter.Id,
            Status = SubscriptionStatus.Trial,
            StartedAtUtc = DateTime.UtcNow,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(starter.TrialDays),
            CurrentPeriodStartUtc = DateTime.UtcNow,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(starter.TrialDays)
        });

        var docs = await _db.LegalDocuments.Where(d => d.IsCurrent).ToListAsync();
        foreach (var doc in docs)
        {
            _db.LegalAcceptances.Add(new LegalAcceptance
            {
                UserId = 0,
                BusinessId = business.Id,
                LegalDocumentId = doc.Id,
                DocumentVersion = doc.Version,
                DocumentType = doc.Type,
                AcceptedAtUtc = DateTime.UtcNow
            });
        }

        _db.Categories.AddRange(
            new Category { BusinessId = business.Id, Name = "Comida", SortOrder = 1, TracksStock = false },
            new Category { BusinessId = business.Id, Name = "Bebida", SortOrder = 2, TracksStock = true }
        );

        await _db.SaveChangesAsync();

        foreach (var acceptance in _db.LegalAcceptances.Local.Where(a => a.BusinessId == business.Id && a.UserId == 0))
            acceptance.UserId = user.Id;
        await _db.SaveChangesAsync();

        _tenant.Set(business.Id, user.Id, user.Role.ToString());
        await _audit.LogAsync("register", "User", user.Id.ToString(), new { user.Email, business.TradeName });
        try { await _email.SendWelcomeAsync(user.Email, user.FullName, business.TradeName); } catch { }

        return Ok(await BuildAuth(user, business));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        _tenant.EnableBypass();
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Business).FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Usuario o clave incorrectos.");

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _tenant.Set(user.BusinessId, user.Id, user.Role.ToString());
        await _audit.LogAsync("login", "User", user.Id.ToString());
        return Ok(await BuildAuth(user, user.Business!));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("logout", "User", User.GetUserId().ToString());
        return Ok();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        _tenant.EnableBypass();
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Business).FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user != null)
        {
            var raw = TokenService.NewOpaqueToken();
            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = TokenService.HashToken(raw),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });
            await _db.SaveChangesAsync();
            
            var resetLink = $"{Request.Scheme}://{Request.Host}/reset-password?token={raw}";
            await _email.SendPasswordResetAsync(user.Email, resetLink, user.Business?.TradeName ?? "Sistema Gastronómico");
            _logger.LogInformation("Password reset token generated for user {UserId}. Reset link: {Link}", user.Id, resetLink);
        }

        return Ok(new { message = "Si el email existe, vas a recibir instrucciones para restablecer la contraseña." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        _tenant.EnableBypass();
        var hash = TokenService.HashToken(request.Token);
        var token = await _db.PasswordResetTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token == null || token.UsedAtUtc != null || token.ExpiresAtUtc < DateTime.UtcNow || token.User == null)
            return BadRequest("El enlace no es válido o expiró.");

        token.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        token.UsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("La contraseña actual no es correcta.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("change_password", "User", user.Id.ToString());
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var user = await _db.Users.Include(u => u.Business).FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user?.Business == null) return Unauthorized();
        return Ok(await BuildAuth(user, user.Business));
    }

    private async Task<AuthResponse> BuildAuth(User user, Business business)
    {
        var sub = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.Plan)
                .ThenInclude(p => p!.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(s => s.BusinessId == business.Id);

        var features = sub?.Plan?.PlanFeatures
            .Where(pf => pf.Feature != null)
            .Select(pf => pf.Feature!.Code)
            .ToList() ?? new List<string>();

        return new AuthResponse
        {
            Token = _tokens.CreateAccessToken(user),
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            BusinessId = business.Id,
            BusinessName = business.TradeName,
            OnboardingCompleted = business.OnboardingCompleted,
            SubscriptionStatus = sub?.Status.ToString() ?? SubscriptionStatus.Trial.ToString(),
            PlanCode = sub?.Plan?.Code ?? "basic",
            PlanName = sub?.Plan?.Name ?? "Plan Básico",
            EnabledFeatures = features
        };
    }
}
