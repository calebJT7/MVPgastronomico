namespace RotiseriaAPI.Models;

public class Supply : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg"; // kg, gr, lt, ml, unidad
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? Supplier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsLowStock => CurrentStock <= MinimumStock;
}

public class RecipeItem : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int SupplyId { get; set; }
    public Supply? Supply { get; set; }
    public decimal Quantity { get; set; }
}

public enum InventoryMovementType
{
    Purchase = 1,
    SaleConsumption = 2,
    Waste = 3,
    Adjustment = 4
}

public class InventoryMovement : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? SupplyId { get; set; }
    public Supply? Supply { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewStock { get; set; }
    public decimal Cost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserName { get; set; }
}

public class SupplierPurchase : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public ICollection<SupplierPurchaseItem> Items { get; set; } = new List<SupplierPurchaseItem>();
}

public class SupplierPurchaseItem : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int SupplierPurchaseId { get; set; }
    public SupplierPurchase? SupplierPurchase { get; set; }
    public int SupplyId { get; set; }
    public Supply? Supply { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Subtotal => Quantity * UnitCost;
}
