namespace RotiseriaAPI.Models;

public class Order : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int? CustomerId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string OrderType { get; set; } = "Delivery";
    public int? TableId { get; set; }
    public string PaymentMethod { get; set; } = "Efectivo";
    public string? Comments { get; set; }
    public decimal DeliveryCost { get; set; }
    public decimal TipAmount { get; set; }
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
    public int? CashShiftId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public string Status { get; set; } = "Pendiente";
    public DateTime? DispatchedAt { get; set; }
    public bool Alert30Dismissed { get; set; }
}
