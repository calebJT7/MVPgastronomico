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
[RequireFeature(FeatureCodes.AdvancedInventory)]
public class SuppliesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public SuppliesController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetSupplies([FromQuery] string? q = null, [FromQuery] bool? onlyLowStock = null)
    {
        var query = _db.Supplies.AsNoTracking().Where(s => s.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.Name.ToLower().Contains(q.ToLower()));

        if (onlyLowStock == true)
            query = query.Where(s => s.CurrentStock <= s.MinimumStock);

        var list = await query.OrderBy(s => s.Name).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSupply([FromBody] SupplyWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("El nombre del insumo es requerido.");

        var supply = new Supply
        {
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            CurrentStock = request.CurrentStock,
            MinimumStock = request.MinimumStock,
            CostPerUnit = request.CostPerUnit,
            Supplier = request.Supplier?.Trim(),
            IsActive = true,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Supplies.Add(supply);
        await _db.SaveChangesAsync();

        if (supply.CurrentStock > 0)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                SupplyId = supply.Id,
                Type = InventoryMovementType.Adjustment,
                Quantity = supply.CurrentStock,
                PreviousStock = 0,
                NewStock = supply.CurrentStock,
                Cost = supply.CostPerUnit * supply.CurrentStock,
                Reason = "Stock inicial de alta de insumo",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserName = User.Identity?.Name ?? "Usuario"
            });
            await _db.SaveChangesAsync();
        }

        await _audit.LogAsync("create_supply", "Supply", supply.Id.ToString(), new { supply.Name });
        return Ok(supply);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSupply(int id, [FromBody] SupplyWriteRequest request)
    {
        var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == id);
        if (supply == null) return NotFound();

        var oldStock = supply.CurrentStock;
        supply.Name = request.Name.Trim();
        supply.Unit = request.Unit.Trim();
        supply.MinimumStock = request.MinimumStock;
        supply.CostPerUnit = request.CostPerUnit;
        supply.Supplier = request.Supplier?.Trim();
        supply.UpdatedAtUtc = DateTime.UtcNow;

        if (oldStock != request.CurrentStock)
        {
            var diff = request.CurrentStock - oldStock;
            supply.CurrentStock = request.CurrentStock;
            _db.InventoryMovements.Add(new InventoryMovement
            {
                SupplyId = supply.Id,
                Type = InventoryMovementType.Adjustment,
                Quantity = diff,
                PreviousStock = oldStock,
                NewStock = request.CurrentStock,
                Cost = supply.CostPerUnit * Math.Abs(diff),
                Reason = "Ajuste manual de inventario",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserName = User.Identity?.Name ?? "Usuario"
            });
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("update_supply", "Supply", supply.Id.ToString());
        return Ok(supply);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSupply(int id)
    {
        var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == id);
        if (supply == null) return NotFound();

        supply.IsActive = false;
        supply.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("delete_supply", "Supply", supply.Id.ToString());
        return NoContent();
    }

    [HttpPost("waste")]
    public async Task<IActionResult> RegisterWaste([FromBody] RegisterWasteRequest request)
    {
        var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == request.SupplyId);
        if (supply == null) return NotFound("Insumo no encontrado.");

        if (request.Quantity <= 0)
            return BadRequest("La cantidad debe ser mayor a cero.");

        var oldStock = supply.CurrentStock;
        supply.CurrentStock = Math.Max(0, supply.CurrentStock - request.Quantity);
        supply.UpdatedAtUtc = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            SupplyId = supply.Id,
            Type = InventoryMovementType.Waste,
            Quantity = -request.Quantity,
            PreviousStock = oldStock,
            NewStock = supply.CurrentStock,
            Cost = supply.CostPerUnit * request.Quantity,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Merma o desperdicio" : request.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserName = User.Identity?.Name ?? "Usuario"
        };

        _db.InventoryMovements.Add(movement);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("register_waste", "InventoryMovement", movement.Id.ToString(), new { supply.Name, request.Quantity, request.Reason });

        return Ok(new { success = true, currentStock = supply.CurrentStock });
    }

    [HttpPost("purchases")]
    public async Task<IActionResult> RegisterPurchase([FromBody] RegisterPurchaseRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("La compra debe contener al menos un insumo.");

        var purchase = new SupplierPurchase
        {
            SupplierName = request.SupplierName.Trim(),
            InvoiceNumber = request.InvoiceNumber?.Trim(),
            Date = request.Date == default ? DateTime.UtcNow : request.Date,
            Notes = request.Notes?.Trim(),
            TotalAmount = 0
        };

        foreach (var item in request.Items)
        {
            var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == item.SupplyId);
            if (supply == null) return BadRequest($"Insumo ID {item.SupplyId} inexistente.");

            var oldStock = supply.CurrentStock;
            supply.CurrentStock += item.Quantity;
            if (item.UnitCost > 0)
            {
                supply.CostPerUnit = item.UnitCost; // Actualiza el costo de reposición
            }
            supply.UpdatedAtUtc = DateTime.UtcNow;

            var subtotal = item.Quantity * item.UnitCost;
            purchase.TotalAmount += subtotal;

            purchase.Items.Add(new SupplierPurchaseItem
            {
                SupplyId = supply.Id,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost
            });

            _db.InventoryMovements.Add(new InventoryMovement
            {
                SupplyId = supply.Id,
                Type = InventoryMovementType.Purchase,
                Quantity = item.Quantity,
                PreviousStock = oldStock,
                NewStock = supply.CurrentStock,
                Cost = subtotal,
                Reason = $"Compra a proveedor {purchase.SupplierName} Fac: {purchase.InvoiceNumber}",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserName = User.Identity?.Name ?? "Usuario"
            });
        }

        _db.SupplierPurchases.Add(purchase);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("supplier_purchase", "SupplierPurchase", purchase.Id.ToString(), new { purchase.SupplierName, purchase.TotalAmount });

        return Ok(purchase);
    }

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] int? supplyId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.InventoryMovements.AsNoTracking().Include(m => m.Supply).AsQueryable();
        if (supplyId.HasValue)
            query = query.Where(m => m.SupplyId == supplyId.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(m => m.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new { items, page, pageSize, total });
    }
}

public class SupplyWriteRequest
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? Supplier { get; set; }
}

public class RegisterWasteRequest
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RegisterPurchaseRequest
{
    public string SupplierName { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public List<PurchaseItemRequest> Items { get; set; } = new();
}

public class PurchaseItemRequest
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
