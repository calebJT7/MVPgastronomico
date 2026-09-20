namespace RotiseriaAPI.Models;

public class Expense : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
