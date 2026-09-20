using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SubscriptionAccessService _access;
    private readonly ITenantDateTimeService _dateTime;

    public ReportsController(AppDbContext context, SubscriptionAccessService access, ITenantDateTimeService dateTime)
    {
        _context = context;
        _access = access;
        _dateTime = dateTime;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboardData()
    {
        var today = await _dateTime.GetTodayStartUtcAsync();
        var ordersToday = await _context.Orders.AsNoTracking()
            .Where(o => o.Date >= today && o.Status != "Cancelado")
            .ToListAsync();

        var paid = ordersToday.Where(o => o.PaymentMethod != "Cuenta Corriente").ToList();
        var salesToday = paid.Sum(o => o.Total - o.DeliveryCost);
        var deliveryRevenue = ordersToday.Where(o => o.OrderType == "Delivery").Sum(o => o.DeliveryCost);
        var deliveryTrips = ordersToday.Count(o => o.OrderType == "Delivery");
        var avgTicket = paid.Count == 0 ? 0 : paid.Average(o => o.Total);

        var totalDebt = await _context.Customers.AsNoTracking().Where(c => c.Balance < 0).SumAsync(c => (decimal?)c.Balance) ?? 0;

        var lowStock = await _context.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null && p.Category.TracksStock && p.Stock < 5)
            .Select(p => new { p.Name, p.Stock })
            .ToListAsync();

        return Ok(new
        {
            SalesToday = salesToday,
            DeliveryRevenue = deliveryRevenue,
            DeliveryTrips = deliveryTrips,
            TotalDebt = Math.Abs(totalDebt),
            OrdersCount = ordersToday.Count,
            AverageTicket = avgTicket,
            LowStock = lowStock
        });
    }

    [HttpGet("monthly-history")]
    public async Task<ActionResult> GetMonthlyHistory()
    {
        await _access.EnsureFeatureAsync("reports");
        var orders = await _context.Orders.AsNoTracking()
            .Where(o => o.Date >= DateTime.UtcNow.AddYears(-1) && o.PaymentMethod != "Cuenta Corriente" && o.Status != "Cancelado")
            .ToListAsync();

        var history = orders
            .GroupBy(o => new { o.Date.Year, o.Date.Month })
            .Select(g => new
            {
                Label = $"{g.Key.Month:00}/{g.Key.Year}",
                Total = g.Sum(o => o.Total - o.DeliveryCost),
                OrderCount = g.Count()
            })
            .OrderBy(x => x.Label)
            .ToList();

        return Ok(history);
    }

    [HttpGet("sales")]
    public async Task<ActionResult> GetSales([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        await _access.EnsureFeatureAsync("reports");
        if (to < from) return BadRequest("Rango inválido.");

        var orders = await _context.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.Date >= from && o.Date <= to && o.Status != "Cancelado")
            .ToListAsync();

        var paid = orders.Where(o => o.PaymentMethod != "Cuenta Corriente").ToList();
        var topProducts = orders.SelectMany(o => o.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Total = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(x => x.Quantity)
            .Take(20)
            .ToList();

        var byCategory = orders.SelectMany(o => o.Items)
            .GroupBy(i => string.IsNullOrWhiteSpace(i.CategoryName) ? "Sin categoría" : i.CategoryName)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var byPayment = orders
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new { Method = g.Key, Total = g.Sum(x => x.Total), Count = g.Count() })
            .ToList();

        return Ok(new
        {
            From = from,
            To = to,
            OrdersCount = orders.Count,
            SalesTotal = paid.Sum(o => o.Total - o.DeliveryCost),
            AverageTicket = paid.Count == 0 ? 0 : paid.Average(o => o.Total),
            TopProducts = topProducts,
            SalesByCategory = byCategory,
            SalesByPaymentMethod = byPayment
        });
    }

    // --- REPORTES AVANZADOS (PREMIUM) ---

    [HttpGet("advanced/margins")]
    [RequireFeature(FeatureCodes.AdvancedReports)]
    public async Task<IActionResult> GetMarginsAndProfitReport()
    {
        var products = await _context.Products.AsNoTracking().Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
        var recipes = await _context.RecipeItems.AsNoTracking().Include(r => r.Supply).ToListAsync();

        var recipeCosts = recipes
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Quantity * (x.Supply != null ? x.Supply.CostPerUnit : 0))
            );

        var report = products.Select(p =>
        {
            var cost = recipeCosts.TryGetValue(p.Id, out var c) ? c : 0;
            var margin = p.Price > 0 ? ((p.Price - cost) / p.Price) * 100 : 0;
            var profit = p.Price - cost;

            return new
            {
                p.Id,
                ProductName = p.Name,
                CategoryName = p.Category?.Name ?? "General",
                Price = p.Price,
                EstimatedCost = cost,
                EstimatedProfit = profit,
                MarginPercentage = Math.Round(margin, 2),
                HasRecipe = recipeCosts.ContainsKey(p.Id)
            };
        }).OrderByDescending(x => x.EstimatedProfit).ToList();

        var totalCatalogValue = products.Sum(p => p.Price);
        var totalEstimatedCost = report.Sum(r => r.EstimatedCost);
        var avgMargin = report.Count > 0 ? report.Average(r => r.MarginPercentage) : 0;

        return Ok(new
        {
            items = report,
            summary = new
            {
                totalProducts = products.Count,
                avgMarginPercentage = Math.Round(avgMargin, 2),
                totalCatalogValue,
                totalEstimatedCost
            }
        });
    }

    [HttpGet("advanced/period-comparison")]
    [RequireFeature(FeatureCodes.AdvancedReports)]
    public async Task<IActionResult> GetPeriodComparison([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var now = DateTime.UtcNow;
        var currentPeriodStart = now.AddDays(-days);
        var previousPeriodStart = currentPeriodStart.AddDays(-days);

        var allOrders = await _context.Orders.AsNoTracking()
            .Where(o => o.Date >= previousPeriodStart && o.Status != "Cancelado")
            .ToListAsync();

        var currentOrders = allOrders.Where(o => o.Date >= currentPeriodStart).ToList();
        var prevOrders = allOrders.Where(o => o.Date >= previousPeriodStart && o.Date < currentPeriodStart).ToList();

        var currentSales = currentOrders.Where(o => o.PaymentMethod != "Cuenta Corriente").Sum(o => o.Total - o.DeliveryCost);
        var prevSales = prevOrders.Where(o => o.PaymentMethod != "Cuenta Corriente").Sum(o => o.Total - o.DeliveryCost);

        var salesGrowth = prevSales > 0 ? ((currentSales - prevSales) / prevSales) * 100 : 0;
        var ordersGrowth = prevOrders.Count > 0 ? ((decimal)(currentOrders.Count - prevOrders.Count) / prevOrders.Count) * 100 : 0;

        var currentAvg = currentOrders.Count > 0 ? currentSales / currentOrders.Count : 0;
        var prevAvg = prevOrders.Count > 0 ? prevSales / prevOrders.Count : 0;
        var avgGrowth = prevAvg > 0 ? ((currentAvg - prevAvg) / prevAvg) * 100 : 0;

        return Ok(new
        {
            days,
            current = new
            {
                periodStart = currentPeriodStart,
                periodEnd = now,
                totalSales = currentSales,
                ordersCount = currentOrders.Count,
                averageTicket = currentAvg
            },
            previous = new
            {
                periodStart = previousPeriodStart,
                periodEnd = currentPeriodStart,
                totalSales = prevSales,
                ordersCount = prevOrders.Count,
                averageTicket = prevAvg
            },
            growth = new
            {
                salesPercentage = Math.Round(salesGrowth, 2),
                ordersPercentage = Math.Round(ordersGrowth, 2),
                averageTicketPercentage = Math.Round(avgGrowth, 2)
            }
        });
    }

    [HttpGet("advanced/by-channel")]
    [RequireFeature(FeatureCodes.AdvancedReports)]
    public async Task<IActionResult> GetChannelBreakdown([FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var orders = await _context.Orders.AsNoTracking()
            .Where(o => o.Date >= from && o.Status != "Cancelado")
            .ToListAsync();

        var channels = orders
            .GroupBy(o =>
            {
                var t = o.OrderType?.ToLower() ?? "";
                if (t.Contains("salon") || t.Contains("mesa")) return "Salón";
                if (t.Contains("delivery")) return "Delivery";
                return "Mostrador";
            })
            .Select(g => new
            {
                Channel = g.Key,
                TotalSales = g.Sum(x => x.Total),
                OrdersCount = g.Count(),
                AverageTicket = g.Count() > 0 ? g.Average(x => x.Total) : 0
            })
            .OrderByDescending(x => x.TotalSales)
            .ToList();

        return Ok(channels);
    }

    [HttpGet("advanced/export-csv")]
    [RequireFeature(FeatureCodes.AdvancedReports)]
    public async Task<IActionResult> ExportSalesCsv([FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var orders = await _context.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Date >= from && o.Status != "Cancelado")
            .OrderByDescending(o => o.Date)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ID;Fecha;Canal;Cliente;MetodoPago;Subtotal;Envio;Total;Estado");

        foreach (var o in orders)
        {
            var subtotal = o.Total - o.DeliveryCost;
            sb.AppendLine($"{o.Id};{o.Date:yyyy-MM-dd HH:mm};{o.OrderType};{o.ClientName};{o.PaymentMethod};{subtotal};{o.DeliveryCost};{o.Total};{o.Status}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"ventas_reporte_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
