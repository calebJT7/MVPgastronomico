using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;

namespace RotiseriaAPI.Controllers;

[Route("api/audit")]
[ApiController]
[Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin}")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var businessId = User.GetBusinessIdSafe();
        var query = _db.AuditLogs.AsNoTracking().Where(a => a.BusinessId == businessId).OrderByDescending(a => a.CreatedAtUtc);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { items, page, pageSize, total });
    }
}
