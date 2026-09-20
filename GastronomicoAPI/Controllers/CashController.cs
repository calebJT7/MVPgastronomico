using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[RequireFeature(FeatureCodes.Cash)]
public class CashController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public CashController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentShift()
    {
        var shift = await _db.CashShifts
            .Include(s => s.Movements)
            .OrderByDescending(s => s.OpenedAtUtc)
            .FirstOrDefaultAsync(s => s.IsOpen);

        if (shift == null)
        {
            return Ok(new { isOpen = false });
        }

        return Ok(new
        {
            isOpen = true,
            shift.Id,
            shift.UserId,
            shift.UserName,
            shift.OpenedAtUtc,
            shift.InitialCash,
            shift.TotalCashSales,
            shift.TotalCardSales,
            shift.TotalTransferSales,
            shift.TotalOtherSales,
            shift.TotalInflows,
            shift.TotalOutflows,
            shift.ExpectedCash,
            shift.Notes,
            movements = shift.Movements.OrderByDescending(m => m.CreatedAtUtc)
        });
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest request)
    {
        var active = await _db.CashShifts.AnyAsync(s => s.IsOpen);
        if (active)
            return BadRequest("Ya existe una caja abierta para este local.");

        var userName = User.Identity?.Name ?? "Usuario";
        var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

        var shift = new CashShift
        {
            UserId = userId,
            UserName = userName,
            OpenedAtUtc = DateTime.UtcNow,
            InitialCash = request.InitialCash,
            IsOpen = true,
            Notes = request.Notes
        };

        _db.CashShifts.Add(shift);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("open_cash_shift", "CashShift", shift.Id.ToString(), new { shift.InitialCash });

        return Ok(shift);
    }

    [HttpPost("close")]
    public async Task<IActionResult> CloseShift([FromBody] CloseShiftRequest request)
    {
        var shift = await _db.CashShifts
            .Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.IsOpen);

        if (shift == null)
            return BadRequest("No hay ninguna caja abierta actualmente.");

        shift.ClosedAtUtc = DateTime.UtcNow;
        shift.ActualCash = request.ActualCash;
        shift.Difference = request.ActualCash - shift.ExpectedCash;
        shift.IsOpen = false;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes) ? request.Notes : $"{shift.Notes} | Cierre: {request.Notes}";
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("close_cash_shift", "CashShift", shift.Id.ToString(), new { shift.ActualCash, shift.Difference });

        return Ok(shift);
    }

    [HttpPost("movement")]
    public async Task<IActionResult> AddMovement([FromBody] AddCashMovementRequest request)
    {
        var shift = await _db.CashShifts.FirstOrDefaultAsync(s => s.IsOpen);
        if (shift == null)
            return BadRequest("Se requiere una caja abierta para registrar movimientos de dinero.");

        if (request.Amount <= 0)
            return BadRequest("El monto debe ser mayor a cero.");

        var userName = User.Identity?.Name ?? "Usuario";

        var movement = new CashMovement
        {
            CashShiftId = shift.Id,
            Type = request.Type,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserName = userName
        };

        if (request.Type == CashMovementType.Inflow)
            shift.TotalInflows += request.Amount;
        else
            shift.TotalOutflows += request.Amount;

        _db.CashMovements.Add(movement);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("cash_movement", "CashMovement", movement.Id.ToString(), new { movement.Type, movement.Amount, movement.Reason });

        return Ok(movement);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetShiftHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.CashShifts.AsNoTracking().Where(s => !s.IsOpen).OrderByDescending(s => s.ClosedAtUtc);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new { items, page, pageSize, total });
    }
}

public class OpenShiftRequest
{
    public decimal InitialCash { get; set; }
    public string? Notes { get; set; }
}

public class CloseShiftRequest
{
    public decimal ActualCash { get; set; }
    public string? Notes { get; set; }
}

public class AddCashMovementRequest
{
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
