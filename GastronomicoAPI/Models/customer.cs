namespace RotiseriaAPI.Models;

public class Customer : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public decimal Balance { get; set; }
}
