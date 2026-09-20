namespace RotiseriaAPI.DTOs;

public class DailyReport
{
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalTransfer { get; set; }
    public decimal TotalDebt { get; set; } // Lo que quedó "fiado" hoy
    public decimal TotalExpenses { get; set; }
    public decimal NetBalance => TotalSales - TotalExpenses; // Lo que realmente queda
    public int OrdersCount { get; set; }

}