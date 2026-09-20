using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;
using Xunit;

namespace GastronomicoAPI.Tests;

public class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TenantIsolationTests()
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
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant))
            .Options;

        var context = new AppDbContext(options, tenant);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task QueryFilters_IsolateDataBetweenTenants()
    {
        // 1. Seed data for Business 1 and Business 2
        var seedTenant = new TenantContext();
        seedTenant.EnableBypass();
        using (var seedDb = CreateDbContext(seedTenant))
        {
            seedDb.Businesses.AddRange(
                new Business { Id = 1, TradeName = "Restaurante Uno" },
                new Business { Id = 2, TradeName = "Restaurante Dos" }
            );

            seedDb.Products.AddRange(
                new Product { Id = 1, BusinessId = 1, Name = "Pizza Napolitana", Price = 8000 },
                new Product { Id = 2, BusinessId = 2, Name = "Hamburguesa Doble", Price = 7500 }
            );

            seedDb.Tables.AddRange(
                new Table { Id = 1, BusinessId = 1, Number = 1, Status = "Libre" },
                new Table { Id = 2, BusinessId = 2, Number = 1, Status = "Libre" }
            );

            await seedDb.SaveChangesAsync();
        }

        // 2. Query as Business 1
        var tenant1 = new TenantContext();
        tenant1.Set(businessId: 1, userId: 10, role: "Owner");
        using (var db1 = CreateDbContext(tenant1))
        {
            var products = await db1.Products.ToListAsync();
            Assert.Single(products);
            Assert.Equal("Pizza Napolitana", products[0].Name);
            Assert.Equal(1, products[0].BusinessId);

            var tables = await db1.Tables.ToListAsync();
            Assert.Single(tables);
            Assert.Equal(1, tables[0].BusinessId);
        }

        // 3. Query as Business 2
        var tenant2 = new TenantContext();
        tenant2.Set(businessId: 2, userId: 20, role: "Owner");
        using (var db2 = CreateDbContext(tenant2))
        {
            var products = await db2.Products.ToListAsync();
            Assert.Single(products);
            Assert.Equal("Hamburguesa Doble", products[0].Name);
            Assert.Equal(2, products[0].BusinessId);
        }
    }

    [Fact]
    public async Task Interceptor_AutomaticallyStampsBusinessIdOnInsert()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 42, userId: 5, role: "Admin");

        using var db = CreateDbContext(tenant);
        var product = new Product
        {
            Name = "Lomito Completo",
            Price = 9000,
            BusinessId = 0 // Stamped by interceptor
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        Assert.Equal(42, product.BusinessId);
    }

    [Fact]
    public async Task Interceptor_BlocksCrossTenantModifications()
    {
        var adminTenant = new TenantContext();
        adminTenant.EnableBypass();
        using (var seedDb = CreateDbContext(adminTenant))
        {
            seedDb.Businesses.Add(new Business { Id = 1, TradeName = "Local A" });
            seedDb.Businesses.Add(new Business { Id = 2, TradeName = "Local B" });
            seedDb.Products.Add(new Product { Id = 99, BusinessId = 2, Name = "Vino Malbec", Price = 6000 });
            await seedDb.SaveChangesAsync();
        }

        // Tenant 1 tries to modify a product belonging to Tenant 2
        var tenant1 = new TenantContext();
        tenant1.Set(businessId: 1, userId: 1, role: "Owner");

        using var db1 = CreateDbContext(tenant1);
        var rogueProduct = new Product
        {
            Id = 99,
            BusinessId = 2, // Belongs to Tenant 2
            Name = "Vino Hacked",
            Price = 1
        };

        db1.Products.Attach(rogueProduct);
        db1.Entry(rogueProduct).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db1.SaveChangesAsync());
    }
}
