using ESC_POS_USB_NET.Printer;
using RotiseriaAPI.Models;

namespace RotiseriaAPI.Services;

public class PrintService
{
    private readonly IConfiguration _config;
    private readonly ILogger<PrintService> _logger;

    public PrintService(IConfiguration config, ILogger<PrintService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public void PrintOrder(Order order)
    {
        var enabled = _config.GetValue("Printing:Enabled", false);
        if (!enabled) return;

        var printerName = _config["Printing:PrinterName"];
        if (string.IsNullOrWhiteSpace(printerName))
        {
            _logger.LogWarning("Printing enabled but Printing:PrinterName is missing.");
            return;
        }

        var printer = new Printer(printerName);
        printer.BoldMode("--- COMANDA ---");
        printer.Append($"Pedido #{order.Id} - {order.Date:HH:mm}");
        printer.Separator();
        printer.Append($"CLIENTE: {order.ClientName.ToUpperInvariant()}");
        if (order.OrderType == "Delivery")
            printer.BoldMode($"DIR: {order.DeliveryAddress.ToUpperInvariant()}");
        printer.Append($"TEL: {order.Phone}");
        printer.Separator();
        printer.BoldMode("DETALLE DE COCINA:");
        foreach (var item in order.Items)
            printer.Append($"{item.Quantity} x {item.ProductName}");
        printer.Separator();
        if (!string.IsNullOrEmpty(order.Comments))
        {
            printer.BoldMode("NOTAS:");
            printer.Append(order.Comments);
            printer.Separator();
        }
        printer.Append($"TIPO: {order.OrderType} | PAGO: {order.PaymentMethod}");
        printer.Append($"ENVIO: ${order.DeliveryCost}");
        printer.BoldMode($"TOTAL A COBRAR: ${order.Total}");
        printer.FullPaperCut();
        printer.PrintDocument();
    }
}
