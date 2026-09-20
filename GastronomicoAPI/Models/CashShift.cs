namespace RotiseriaAPI.Models;

public enum CashMovementType
{
    Inflow = 1,
    Outflow = 2
}

public class CashShift : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public decimal InitialCash { get; set; }
    public decimal ActualCash { get; set; }
    public decimal Difference { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalTransferSales { get; set; }
    public decimal TotalOtherSales { get; set; }
    public decimal TotalInflows { get; set; }
    public decimal TotalOutflows { get; set; }
    public string? Notes { get; set; }
    public bool IsOpen { get; set; } = true;

    public decimal ExpectedCash => InitialCash + TotalCashSales + TotalInflows - TotalOutflows;
    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}

public class CashMovement : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int CashShiftId { get; set; }
    public CashShift? CashShift { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserName { get; set; } = string.Empty;
}
