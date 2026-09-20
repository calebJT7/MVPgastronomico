using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.DTOs;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[RequireFeature(FeatureCodes.Orders)]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PrintService _printService;
    private readonly IAuditService _audit;
    private readonly SubscriptionAccessService _access;
    private readonly IHubContext<Hubs.OrderHub> _hub;
    private readonly ITenantDateTimeService _dateTime;

    public OrdersController(AppDbContext context, PrintService printService, IAuditService audit, SubscriptionAccessService access, IHubContext<Hubs.OrderHub> hub, ITenantDateTimeService dateTime)
    {
        _context = context;
        _printService = printService;
        _audit = audit;
        _access = access;
        _hub = hub;
        _dateTime = dateTime;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(CreateOrderRequest request)
    {
        await _access.EnsureCanOperateAsync();
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("El pedido no tiene productos.");

        var order = new Order
        {
            Date = DateTime.UtcNow,
            CustomerId = request.CustomerId,
            ClientName = request.ClientName,
            Phone = request.Phone,
            DeliveryAddress = request.DeliveryAddress,
            OrderType = request.OrderType,
            TableId = request.TableId,
            PaymentMethod = request.PaymentMethod,
            Comments = request.Comments,
            DeliveryCost = request.DeliveryCost,
            Status = "Pendiente"
        };

        decimal totalProductos = 0;
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0) return BadRequest("Cantidad inválida.");
            var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == line.ProductId);
            if (product == null) return BadRequest("Producto inexistente o de otro negocio.");
            if (!product.IsActive || !product.IsAvailable)
                return BadRequest($"El producto {product.Name} no está disponible.");

            if (product.Category?.TracksStock == true || (product.Category?.Name.Contains("Bebida", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                if (product.Stock < line.Quantity)
                    return BadRequest($"No hay stock suficiente de {product.Name}. Disponible: {product.Stock}");
                product.Stock -= line.Quantity;
            }

            var item = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CategoryName = product.Category?.Name ?? string.Empty,
                Quantity = line.Quantity,
                UnitPrice = product.Price
            };
            order.Items.Add(item);
            totalProductos += item.UnitPrice * item.Quantity;
        }

        if (order.CustomerId.HasValue)
        {
            var customerOk = await _context.Customers.AnyAsync(c => c.Id == order.CustomerId.Value);
            if (!customerOk) return BadRequest("El cliente no pertenece a este negocio.");
        }

        // Mesa / Salón
        if (order.OrderType == "Salon" && order.TableId.HasValue)
        {
            var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == order.TableId.Value);
            if (table == null) return BadRequest("La mesa no existe.");

            if (table.CurrentOrderId.HasValue)
            {
                var existingOrder = await _context.Orders.Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == table.CurrentOrderId);
                if (existingOrder == null) return BadRequest("La mesa tiene una cuenta inválida.");

                existingOrder.Items.AddRange(order.Items);
                existingOrder.Total += totalProductos;
                existingOrder.Status = "Pendiente";
                existingOrder.Date = DateTime.UtcNow;

                await ProcessRecipeDeductionAsync(order.Items, existingOrder.Id);
                await _context.SaveChangesAsync();

                ImprimirTicket(existingOrder);
                await _audit.LogAsync("add_to_order", "Order", existingOrder.Id.ToString());
                await NotifyOrderChange(existingOrder);
                return Ok(existingOrder);
            }

            order.Total = totalProductos;
            order.ClientName = $"MESA {table.Number}";
            order.OrderType = $"SALÓN - MESA {table.Number}";
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            table.CurrentOrderId = order.Id;
            table.Status = "Ocupada";
            await ProcessRecipeDeductionAsync(order.Items, order.Id);
            await _context.SaveChangesAsync();

            ImprimirTicket(order);
            await _audit.LogAsync("create_order", "Order", order.Id.ToString());
            await NotifyOrderChange(order);
            return Ok(order);
        }

        // Mostrador / Delivery
        order.Total = totalProductos + order.DeliveryCost;
        if (order.PaymentMethod == "Cuenta Corriente")
        {
            if (!order.CustomerId.HasValue)
                return BadRequest("La cuenta corriente requiere un cliente registrado.");
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == order.CustomerId.Value);
            if (customer == null) return BadRequest("Cliente inválido.");
            customer.Balance -= order.Total;
        }

        // Impacto en Caja Abierta (si existe turno abierto)
        var openShift = await _context.CashShifts.FirstOrDefaultAsync(s => s.IsOpen);
        if (openShift != null)
        {
            order.CashShiftId = openShift.Id;
            var method = order.PaymentMethod?.ToLower() ?? "efectivo";
            if (method.Contains("efectivo") || method.Contains("cash"))
                openShift.TotalCashSales += order.Total;
            else if (method.Contains("tarjeta") || method.Contains("card") || method.Contains("posnet"))
                openShift.TotalCardSales += order.Total;
            else if (method.Contains("transferencia") || method.Contains("qr") || method.Contains("mp"))
                openShift.TotalTransferSales += order.Total;
            else if (!method.Contains("cuenta corriente"))
                openShift.TotalOtherSales += order.Total;
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Descuento automático de recetas / insumos si el plan incluye recetas
        await ProcessRecipeDeductionAsync(order.Items, order.Id);
        await _context.SaveChangesAsync();

        ImprimirTicket(order);
        await _audit.LogAsync("create_order", "Order", order.Id.ToString());
        await NotifyOrderChange(order);
        return Ok(order);
    }

    private async Task ProcessRecipeDeductionAsync(IEnumerable<OrderItem> items, int orderId)
    {
        try
        {
            var hasRecipes = await _access.HasFeatureAsync(FeatureCodes.Recipes);
            if (!hasRecipes) return;

            foreach (var item in items)
            {
                var recipes = await _context.RecipeItems
                    .Include(r => r.Supply)
                    .Where(r => r.ProductId == item.ProductId)
                    .ToListAsync();

                foreach (var r in recipes)
                {
                    if (r.Supply != null && r.Quantity > 0)
                    {
                        var deduction = r.Quantity * item.Quantity;
                        var oldStock = r.Supply.CurrentStock;
                        r.Supply.CurrentStock -= deduction;
                        r.Supply.UpdatedAtUtc = DateTime.UtcNow;

                        _context.InventoryMovements.Add(new InventoryMovement
                        {
                            SupplyId = r.SupplyId,
                            ProductId = item.ProductId,
                            Type = InventoryMovementType.SaleConsumption,
                            Quantity = -deduction,
                            PreviousStock = oldStock,
                            NewStock = r.Supply.CurrentStock,
                            Cost = deduction * r.Supply.CostPerUnit,
                            Reason = $"Consumo receta Pedido #{orderId} ({item.Quantity}x {item.ProductName})",
                            CreatedAtUtc = DateTime.UtcNow,
                            CreatedByUserName = "Sistema (Recetas)"
                        });
                    }
                }
            }
        }
        catch
        {
            // best-effort recipe deduction
        }
    }

    private async Task NotifyOrderChange(Order order)
    {
        try
        {
            await _hub.Clients.Group($"business_{order.BusinessId}").SendAsync("OrderUpdated", new { order.Id, order.Status, order.OrderType, order.Total, order.TableId });
        }
        catch { }
    }

    private void ImprimirTicket(Order order)
    {
        try { _printService.PrintOrder(order); }
        catch { }
    }

    [HttpPost("reprint/{id:int}")]
    public async Task<IActionResult> ReprintOrder(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        ImprimirTicket(order);
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<Order>>> GetOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.Orders.AsNoTracking().Include(o => o.Items).OrderByDescending(o => o.Date);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<Order> { Items = items, Page = page, PageSize = pageSize, Total = total });
    }

    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<Order>>> GetTodayOrders()
    {
        var today = await _dateTime.GetTodayStartUtcAsync();
        return await _context.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.Date >= today)
            .OrderByDescending(o => o.Date)
            .ToListAsync();
    }

    // Monitor de Cocina (KDS) - Requiere Feature KDS (Premium)
    [HttpGet("kds")]
    [RequireFeature(FeatureCodes.Kds)]
    public async Task<ActionResult<IEnumerable<Order>>> GetKdsOrders()
    {
        var today = await _dateTime.GetTodayStartUtcAsync();
        return await _context.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.Date >= today && o.Status != "Cancelado")
            .OrderBy(o => o.Date)
            .ToListAsync();
    }

    [HttpPatch("dispatch/{id:int}")]
    public async Task<IActionResult> DispatchOrder(int id)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        order.Status = "Despachado";
        order.DispatchedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await NotifyOrderChange(order);
        return Ok();
    }

    [HttpPatch("dismiss30m/{id:int}")]
    public async Task<IActionResult> Dismiss30MinAlert(int id)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        order.Alert30Dismissed = true;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("cancel/{id:int}")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        order.Status = "Cancelado";
        await _context.SaveChangesAsync();
        await _audit.LogAsync("cancel_order", "Order", order.Id.ToString());
        await NotifyOrderChange(order);
        return Ok();
    }
}
