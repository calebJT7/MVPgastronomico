namespace RotiseriaWeb.Models;

public class Order
{
    public int Id { get; set; }
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
    public decimal Total { get; set; }
    public bool IsPaid { get; set; } = false;
    public List<OrderItem> Items { get; set; } = new();

    // Propiedades de Cocina y Logística
    public string Status { get; set; } = "Pendiente";
    public DateTime? DispatchedAt { get; set; }
    public bool Alert30Dismissed { get; set; } = false;
}

public class CreateOrderRequest
{
    public int? CustomerId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string OrderType { get; set; } = "Local";
    public int? TableId { get; set; }
    public string PaymentMethod { get; set; } = "Efectivo";
    public string? Comments { get; set; }
    public decimal DeliveryCost { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class OrderItemRequest : CreateOrderItemRequest
{
}