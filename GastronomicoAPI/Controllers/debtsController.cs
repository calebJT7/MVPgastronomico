using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DebtsController : ControllerBase
{
    private readonly AppDbContext _context;
    public DebtsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetDebts() => Ok(await _context.Debts.AsNoTracking().ToListAsync());

    [HttpPost]
    public async Task<IActionResult> PostDebt(Models.Debt debt)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == debt.CustomerId);
        if (customer == null) return BadRequest("Cliente inválido.");
        debt.Id = 0;
        debt.Date = DateTime.UtcNow;
        debt.IsPaid = false;
        customer.Balance -= debt.Amount;
        _context.Debts.Add(debt);
        await _context.SaveChangesAsync();
        return Ok(debt);
    }

    [HttpPut("{id:int}/pay")]
    public async Task<IActionResult> PayDebt(int id)
    {
        var debt = await _context.Debts.Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == id);
        if (debt == null) return NotFound();
        if (!debt.IsPaid)
        {
            debt.IsPaid = true;
            if (debt.Customer != null)
            {
                debt.Customer.Balance += debt.Amount;
            }
            await _context.SaveChangesAsync();
        }
        return NoContent();
    }
}
