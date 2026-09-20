namespace RotiseriaWeb.Models;

public class DashboardDTO
{
    public decimal SalesToday { get; set; }
    public decimal TotalDebt { get; set; }
    public int OrdersCount { get; set; }
    public List<LowStockItem> LowStock { get; set; } = new();
    public decimal DeliveryRevenue { get; set; }
    public int DeliveryTrips { get; set; }
}

public class LowStockItem
{
    public string Name { get; set; } = "";
    public int Stock { get; set; }
}