using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[RequireFeature(FeatureCodes.Tables)]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SubscriptionAccessService _access;
    private readonly IAuditService _audit;
    private readonly IHubContext<Hubs.OrderHub> _hub;

    public TablesController(AppDbContext context, SubscriptionAccessService access, IAuditService audit, IHubContext<Hubs.OrderHub> hub)
    {
        _context = context;
        _access = access;
        _audit = audit;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Table>>> GetTables() =>
        await _context.Tables.AsNoTracking().OrderBy(t => t.Number).ToListAsync();

    [HttpPost]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<ActionResult<Table>> Create(Table table)
    {
        var count = await _context.Tables.CountAsync();
        await _access.EnsureLimitAsync("tables", count);
        table.Id = 0;
        table.Status = "Libre";
        table.CurrentOrderId = null;
        _context.Tables.Add(table);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("create_table", "Table", table.Id.ToString());

        var business = await _context.Businesses.FindAsync(table.BusinessId);
        if (business is { OnboardingCompleted: false, OnboardingStep: OnboardingStep.Tables })
        {
            business.OnboardingStep = OnboardingStep.Completed;
            business.OnboardingCompleted = true;
            await _context.SaveChangesAsync();
        }

        return Ok(table);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Delete(int id)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id);
        if (table == null) return NotFound();
        if (table.Status == "Ocupada") return BadRequest("No se puede eliminar una mesa ocupada.");
        _context.Tables.Remove(table);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/order")]
    public async Task<ActionResult<Order>> GetTableOrder(int id)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id);
        if (table == null || table.CurrentOrderId == null) return NotFound("Mesa libre");

        var order = await _context.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == table.CurrentOrderId);
        if (order == null) return NotFound();
        return order;
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> CloseTable(int id, [FromBody] CloseTableRequest? request = null)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id);
        if (table == null || table.CurrentOrderId == null) return BadRequest("La mesa ya está libre");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == table.CurrentOrderId);
        if (order != null)
        {
            order.Status = "Despachado";
            order.IsPaid = true;
            if (request != null)
            {
                if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
                    order.PaymentMethod = request.PaymentMethod;
                if (request.TipAmount > 0)
                    order.TipAmount = request.TipAmount;
            }

            // Impacto en caja abierta
            var openShift = await _context.CashShifts.FirstOrDefaultAsync(s => s.IsOpen);
            if (openShift != null)
            {
                order.CashShiftId = openShift.Id;
                var totalWithTip = order.Total + order.TipAmount;
                var method = order.PaymentMethod?.ToLower() ?? "efectivo";
                if (method.Contains("efectivo") || method.Contains("cash"))
                    openShift.TotalCashSales += totalWithTip;
                else if (method.Contains("tarjeta") || method.Contains("card") || method.Contains("posnet"))
                    openShift.TotalCardSales += totalWithTip;
                else if (method.Contains("transferencia") || method.Contains("qr") || method.Contains("mp"))
                    openShift.TotalTransferSales += totalWithTip;
                else
                    openShift.TotalOtherSales += totalWithTip;
            }
        }

        table.Status = "Libre";
        table.CurrentOrderId = null;
        await _context.SaveChangesAsync();
        await _audit.LogAsync("close_table", "Table", table.Id.ToString());
        try
        {
            await _hub.Clients.Group($"business_{table.BusinessId}").SendAsync("TableUpdated", new { table.Id, table.Number, table.Status, table.CurrentOrderId });
        }
        catch { }
        return Ok();
    }

    // División de Cuentas (Split Bills) & Propinas - Requiere Feature SPLIT_BILLS (Premium)
    [HttpPost("{id:int}/split-bill")]
    [RequireFeature(FeatureCodes.SplitBills)]
    public async Task<IActionResult> SplitBill(int id, [FromBody] SplitBillRequest request)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id);
        if (table == null || table.CurrentOrderId == null) return BadRequest("La mesa no está ocupada.");

        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == table.CurrentOrderId);
        if (order == null) return NotFound("Comanda no encontrada.");

        var parts = Math.Max(1, request.Parts);
        var subtotal = order.Total;
        var tip = request.TipAmount > 0
            ? request.TipAmount
            : (request.TipPercentage > 0 ? (subtotal * request.TipPercentage / 100m) : 0m);

        var totalWithTip = subtotal + tip;
        var perPerson = Math.Round(totalWithTip / parts, 2);

        return Ok(new
        {
            tableId = table.Id,
            tableNumber = table.Number,
            parts,
            subtotal,
            tip,
            totalWithTip,
            perPerson,
            currency = "ARS"
        });
    }
}

public class CloseTableRequest
{
    public string? PaymentMethod { get; set; }
    public decimal TipAmount { get; set; }
}

public class SplitBillRequest
{
    public int Parts { get; set; } = 2;
    public decimal TipPercentage { get; set; }
    public decimal TipAmount { get; set; }
}
