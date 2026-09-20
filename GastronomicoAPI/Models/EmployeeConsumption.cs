namespace RotiseriaAPI.Models;

public class EmployeeConsumption : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
