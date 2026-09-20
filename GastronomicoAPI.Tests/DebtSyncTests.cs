using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Controllers;
using RotiseriaAPI.Data;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;
using Xunit;

namespace GastronomicoAPI.Tests;

public class DebtSyncTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DebtSyncTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Close();
    }

    private AppDbContext CreateDbContext(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new AppDbContext(options, tenant);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task PostDebt_DecreasesCustomerBalance()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 1, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        var customer = new Customer { Id = 1, BusinessId = 1, Name = "Juan Perez", Balance = 0 };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var controller = new DebtsController(db);
        await controller.PostDebt(new Debt { CustomerId = 1, Amount = 2500 });

        var updated = await db.Customers.FindAsync(1);
        Assert.NotNull(updated);
        Assert.Equal(-2500, updated.Balance);
    }

    [Fact]
    public async Task PayDebt_IncreasesCustomerBalanceAndMarksDebtPaid()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 1, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        var customer = new Customer { Id = 1, BusinessId = 1, Name = "Maria Lopez", Balance = -3000 };
        db.Customers.Add(customer);
        var debt = new Debt { Id = 1, BusinessId = 1, CustomerId = 1, Amount = 3000, IsPaid = false };
        db.Debts.Add(debt);
        await db.SaveChangesAsync();

        var controller = new DebtsController(db);
        await controller.PayDebt(1);

        var updatedCustomer = await db.Customers.FindAsync(1);
        var updatedDebt = await db.Debts.FindAsync(1);

        Assert.NotNull(updatedCustomer);
        Assert.NotNull(updatedDebt);
        Assert.True(updatedDebt.IsPaid);
        Assert.Equal(0, updatedCustomer.Balance);
    }
}
