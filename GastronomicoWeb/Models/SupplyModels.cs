namespace RotiseriaWeb.Models;

public class SupplyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? Supplier { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLowStock => CurrentStock <= MinimumStock;
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

public class ProductRecipeDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public decimal TotalCost { get; set; }
    public decimal EstimatedMarginPercentage { get; set; }
    public List<RecipeItemDetailDto> Items { get; set; } = new();
}

public class RecipeItemDetailDto
{
    public int Id { get; set; }
    public int SupplyId { get; set; }
    public string SupplyName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal Quantity { get; set; }
    public decimal SubtotalCost { get; set; }
}

public class SaveRecipeRequest
{
    public List<RecipeItemInputDto> Items { get; set; } = new();
}

public class RecipeItemInputDto
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
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
    public List<PurchaseItemInputDto> Items { get; set; } = new();
}

public class PurchaseItemInputDto
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
