namespace RotiseriaWeb.Models;

public enum CashMovementType
{
    Inflow = 1,
    Outflow = 2
}

public class CashShiftDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
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
    public decimal ExpectedCash { get; set; }
    public string? Notes { get; set; }
    public bool IsOpen { get; set; }
    public List<CashMovementDto> Movements { get; set; } = new();
}

public class CurrentShiftResponse
{
    public bool IsOpen { get; set; }
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public decimal InitialCash { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalTransferSales { get; set; }
    public decimal TotalOtherSales { get; set; }
    public decimal TotalInflows { get; set; }
    public decimal TotalOutflows { get; set; }
    public decimal ExpectedCash { get; set; }
    public string? Notes { get; set; }
    public List<CashMovementDto> Movements { get; set; } = new();
}

public class CashMovementDto
{
    public int Id { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
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
