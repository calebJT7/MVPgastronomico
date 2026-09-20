using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;
using RotiseriaAPI.Services;
using Xunit;

namespace GastronomicoAPI.Tests;

public class SubscriptionLimitTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SubscriptionLimitTests()
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
    public async Task SubscriptionAccessService_EnforcesMaxProductsLimit()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 1, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 1, TradeName = "Local Test" });
        var starterPlan = new Plan
        {
            Id = 1,
            Code = "starter",
            Name = "Starter",
            MaxProducts = 5,
            MaxUsers = 3,
            MaxTables = 10
        };
        db.Plans.Add(starterPlan);

        db.Subscriptions.Add(new Subscription
        {
            BusinessId = 1,
            PlanId = 1,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(30)
        });

        await db.SaveChangesAsync();

        var access = new SubscriptionAccessService(db, tenant);

        // Within limit: 4 products
        await access.EnsureLimitAsync("products", 4);

        // At limit: 5 products -> throws PlanLimitException
        await Assert.ThrowsAsync<PlanLimitException>(() => access.EnsureLimitAsync("products", 5));
    }

    [Fact]
    public async Task SubscriptionAccessService_BlocksExpiredSubscriptions()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 2, userId: 2, role: "Owner");

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 2, TradeName = "Local Test 2" });
        var plan = new Plan { Id = 1, Code = "pro", Name = "Pro", MaxProducts = 100, MaxUsers = 10, MaxTables = 20 };
        db.Plans.Add(plan);

        db.Subscriptions.Add(new Subscription
        {
            BusinessId = 2,
            PlanId = 1,
            Status = SubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddMinutes(-5) // Expired!
        });

        await db.SaveChangesAsync();

        var access = new SubscriptionAccessService(db, tenant);

        await Assert.ThrowsAsync<SubscriptionBlockedException>(() => access.EnsureCanOperateAsync());
    }
}
