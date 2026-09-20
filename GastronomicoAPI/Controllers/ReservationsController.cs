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
[RequireFeature(FeatureCodes.Reservations)]
public class ReservationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ReservationsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetReservations([FromQuery] DateTime? date = null, [FromQuery] ReservationStatus? status = null)
    {
        var query = _db.Reservations.AsNoTracking().Include(r => r.Table).AsQueryable();

        if (date.HasValue)
        {
            var d = date.Value.Date;
            query = query.Where(r => r.ReservationDate.Date == d);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var list = await query.OrderBy(r => r.ReservationDate).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] ReservationWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest("El nombre del cliente es obligatorio.");

        if (request.ReservationDate == default)
            return BadRequest("La fecha y hora de la reserva es obligatoria.");

        var res = new Reservation
        {
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone?.Trim(),
            CustomerEmail = request.CustomerEmail?.Trim(),
            TableId = request.TableId,
            ReservationDate = request.ReservationDate,
            Pax = Math.Max(1, request.Pax),
            Status = ReservationStatus.Pending,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Reservations.Add(res);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("create_reservation", "Reservation", res.Id.ToString(), new { res.CustomerName, res.ReservationDate, res.Pax });

        return Ok(res);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateReservationStatusRequest request)
    {
        var res = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (res == null) return NotFound();

        res.Status = request.Status;
        if (request.TableId.HasValue)
            res.TableId = request.TableId;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("update_reservation_status", "Reservation", id.ToString(), new { res.Status });

        return Ok(res);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelReservation(int id)
    {
        var res = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (res == null) return NotFound();

        res.Status = ReservationStatus.Cancelled;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("cancel_reservation", "Reservation", id.ToString());

        return NoContent();
    }
}

public class ReservationWriteRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? TableId { get; set; }
    public DateTime ReservationDate { get; set; }
    public int Pax { get; set; } = 2;
    public string? Notes { get; set; }
}

public class UpdateReservationStatusRequest
{
    public ReservationStatus Status { get; set; }
    public int? TableId { get; set; }
}
