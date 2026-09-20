using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Models;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _context;
    public CustomerController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers([FromQuery] string? q = null)
    {
        var query = _context.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.ToLower().Contains(q.ToLower()));
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> PostCustomer(Customer customer)
    {
        customer.Id = 0;
        customer.Name = customer.Name.Trim();
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCustomers), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutCustomer(int id, Customer incoming)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        customer.Name = incoming.Name.Trim();
        customer.Phone = incoming.Phone;
        customer.Address = incoming.Address;
        customer.Notes = incoming.Notes;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult> PayDebt(int id, [FromBody] decimal amount)
    {
        if (amount <= 0) return BadRequest("Monto inválido.");
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        customer.Balance += amount;

        var unpaidDebts = await _context.Debts.Where(d => d.CustomerId == id && !d.IsPaid).OrderBy(d => d.Date).ToListAsync();
        decimal remaining = amount;
        foreach (var d in unpaidDebts)
        {
            if (remaining <= 0) break;
            if (remaining >= d.Amount)
            {
                d.IsPaid = true;
                remaining -= d.Amount;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(customer);
    }
}
