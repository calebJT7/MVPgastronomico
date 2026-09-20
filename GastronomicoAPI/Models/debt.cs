namespace RotiseriaAPI.Models;

public class Debt : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public bool IsPaid { get; set; }
}
