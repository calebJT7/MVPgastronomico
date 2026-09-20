namespace RotiseriaAPI.Models;

public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Seated = 2,
    Cancelled = 3,
    NoShow = 4
}

public class Reservation : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? TableId { get; set; }
    public Table? Table { get; set; }
    public DateTime ReservationDate { get; set; }
    public int Pax { get; set; } = 2;
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
