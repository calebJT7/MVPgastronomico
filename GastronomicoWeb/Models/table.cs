namespace RotiseriaWeb.Models
{
    public class Table
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Status { get; set; } = "Libre";
        public int? CurrentOrderId { get; set; }
    }
}