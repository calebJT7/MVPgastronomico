namespace RotiseriaWeb.Models;

public class Category
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 1;
    public bool TracksStock { get; set; }
    public bool IsActive { get; set; } = true;
}
