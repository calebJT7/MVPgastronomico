namespace RotiseriaWeb.Models;

public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Seated = 2,
    Cancelled = 3,
    NoShow = 4
}

public class ReservationDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? TableId { get; set; }
    public Table? Table { get; set; }
    public DateTime ReservationDate { get; set; }
    public int Pax { get; set; } = 2;
    public ReservationStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class ReservationWriteRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? TableId { get; set; }
    public DateTime ReservationDate { get; set; } = DateTime.Now.AddHours(2);
    public int Pax { get; set; } = 2;
    public string? Notes { get; set; }
}

public class UpdateReservationStatusRequest
{
    public ReservationStatus Status { get; set; }
    public int? TableId { get; set; }
}

public class SplitBillRequest
{
    public int Parts { get; set; } = 2;
    public decimal TipPercentage { get; set; }
    public decimal TipAmount { get; set; }
}

public class SplitBillResultDto
{
    public int TableId { get; set; }
    public int TableNumber { get; set; }
    public int Parts { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tip { get; set; }
    public decimal TotalWithTip { get; set; }
    public decimal PerPerson { get; set; }
}
