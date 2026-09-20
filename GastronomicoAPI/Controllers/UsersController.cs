using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.DTOs;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin}")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly SubscriptionAccessService _access;
    private readonly IEmailService _email;

    public UsersController(AppDbContext db, IAuditService audit, SubscriptionAccessService access, IEmailService email)
    {
        _db = db;
        _audit = audit;
        _access = access;
        _email = email;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Email, u.FullName, Role = u.Role.ToString(), u.IsActive, u.LastLoginAtUtc })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var count = await _db.Users.CountAsync();
        await _access.EnsureLimitAsync("users", count);

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.Owner)
            return BadRequest("Rol inválido.");

        var email = request.Email.Trim().ToLowerInvariant();
        _db.ChangeTracker.Clear();
        // unique globally — bypass not needed because query filter is current tenant; check global:
        var exists = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
        if (exists) return Conflict("El email ya está en uso.");

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("create_user", "User", user.Id.ToString(), new { user.Email, user.Role });

        var business = await _db.Businesses.FindAsync(user.BusinessId);
        try
        {
            await _email.SendStaffInviteAsync(user.Email, user.FullName, user.Role.ToString(), request.Password, business?.TradeName ?? "Sistema Gastronómico");
        }
        catch { }

        return Ok(new { user.Id, user.Email, user.FullName, Role = user.Role.ToString(), user.IsActive });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        if (user.Role == UserRole.Owner && request.Role != UserRole.Owner.ToString())
            return BadRequest("No se puede cambiar el rol del propietario.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.Owner && user.Role != UserRole.Owner)
            return BadRequest("Rol inválido.");

        user.FullName = request.FullName.Trim();
        user.Role = user.Role == UserRole.Owner ? UserRole.Owner : role;
        user.IsActive = user.Role == UserRole.Owner || request.IsActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("update_user", "User", user.Id.ToString(), new { user.Role, user.IsActive });
        return Ok();
    }
}
