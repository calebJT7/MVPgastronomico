using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;
using RotiseriaAPI.Services;
using Xunit;

namespace GastronomicoAPI.Tests;

public class CapabilitiesAndPlanTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CapabilitiesAndPlanTests()
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
    public async Task FeatureCapabilities_BasicPlanHasBasicFeatures_NotPremiumFeatures()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 10, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 10, TradeName = "Rotiseria Básico" });

        var basicPlan = new Plan { Id = 1, Code = "basic", Name = "Plan Básico" };
        var premiumPlan = new Plan { Id = 2, Code = "premium", Name = "Plan Premium" };
        db.Plans.AddRange(basicPlan, premiumPlan);

        var featOrders = new Feature { Id = 1, Code = FeatureCodes.Orders, Name = "Pedidos" };
        var featCash = new Feature { Id = 2, Code = FeatureCodes.Cash, Name = "Caja" };
        var featRecipes = new Feature { Id = 3, Code = FeatureCodes.Recipes, Name = "Recetas", IsPremium = true };
        var featKds = new Feature { Id = 4, Code = FeatureCodes.Kds, Name = "KDS", IsPremium = true };
        var featSupplies = new Feature { Id = 5, Code = FeatureCodes.AdvancedInventory, Name = "Insumos", IsPremium = true };

        db.Features.AddRange(featOrders, featCash, featRecipes, featKds, featSupplies);

        // Basic features
        db.PlanFeatures.Add(new PlanFeature { PlanId = 1, FeatureId = 1 });
        db.PlanFeatures.Add(new PlanFeature { PlanId = 1, FeatureId = 2 });

        // Premium features
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 1 });
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 2 });
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 3 });
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 4 });
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 5 });

        // Tenant has Basic subscription
        db.Subscriptions.Add(new Subscription
        {
            BusinessId = 10,
            PlanId = 1,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddMonths(1)
        });

        await db.SaveChangesAsync();

        var access = new SubscriptionAccessService(db, tenant);

        // Assert basic features are enabled
        Assert.True(await access.HasFeatureAsync(FeatureCodes.Orders));
        Assert.True(await access.HasFeatureAsync(FeatureCodes.Cash));

        // Assert premium features are locked
        Assert.False(await access.HasFeatureAsync(FeatureCodes.Recipes));
        Assert.False(await access.HasFeatureAsync(FeatureCodes.Kds));
        Assert.False(await access.HasFeatureAsync(FeatureCodes.AdvancedInventory));
    }

    [Fact]
    public async Task FeatureCapabilities_PremiumPlanUnlocksAllFeatures()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 20, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 20, TradeName = "Restaurante Gourmet" });

        var premiumPlan = new Plan { Id = 2, Code = "premium", Name = "Plan Premium" };
        db.Plans.Add(premiumPlan);

        var featRecipes = new Feature { Id = 10, Code = FeatureCodes.Recipes, Name = "Recetas", IsPremium = true };
        var featSplit = new Feature { Id = 11, Code = FeatureCodes.SplitBills, Name = "Split Bills", IsPremium = true };
        db.Features.AddRange(featRecipes, featSplit);
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 10 });
        db.PlanFeatures.Add(new PlanFeature { PlanId = 2, FeatureId = 11 });

        db.Subscriptions.Add(new Subscription
        {
            BusinessId = 20,
            PlanId = 2,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddMonths(1)
        });

        await db.SaveChangesAsync();

        var access = new SubscriptionAccessService(db, tenant);

        Assert.True(await access.HasFeatureAsync(FeatureCodes.Recipes));
        Assert.True(await access.HasFeatureAsync(FeatureCodes.SplitBills));
    }

    [Fact]
    public async Task CashShift_TracksInitialCash_Movements_AndArqueoDifference()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 30, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 30, TradeName = "Café Central" });

        var shift = new CashShift
        {
            BusinessId = 30,
            UserId = 1,
            UserName = "Cajero Test",
            OpenedAtUtc = DateTime.UtcNow,
            InitialCash = 10000m,
            IsOpen = true
        };
        db.CashShifts.Add(shift);
        await db.SaveChangesAsync();

        // Simulate Cash Sale
        shift.TotalCashSales += 5000m;

        // Simulate Inflow
        shift.TotalInflows += 2000m;
        db.CashMovements.Add(new CashMovement
        {
            BusinessId = 30,
            CashShiftId = shift.Id,
            Type = CashMovementType.Inflow,
            Amount = 2000m,
            Reason = "Cambio inicial extra"
        });

        // Simulate Outflow (Expense)
        shift.TotalOutflows += 1500m;
        db.CashMovements.Add(new CashMovement
        {
            BusinessId = 30,
            CashShiftId = shift.Id,
            Type = CashMovementType.Outflow,
            Amount = 1500m,
            Reason = "Pago a proveedor de hielo"
        });

        await db.SaveChangesAsync();

        // Expected Cash should be: 10000 + 5000 + 2000 - 1500 = 15500
        Assert.Equal(15500m, shift.ExpectedCash);

        // Cashier counts 15500 physical cash: exact match
        shift.ActualCash = 15500m;
        shift.Difference = shift.ActualCash - shift.ExpectedCash;
        shift.IsOpen = false;
        shift.ClosedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        Assert.Equal(0m, shift.Difference);
        Assert.False(shift.IsOpen);
    }

    [Fact]
    public async Task Recipe_SupplyDeductionCalculation_CorrectlyUpdatesStock()
    {
        var tenant = new TenantContext();
        tenant.Set(businessId: 40, userId: 1, role: "Owner");

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 40, TradeName = "Pizzería Nápoles" });

        var flour = new Supply
        {
            BusinessId = 40,
            Name = "Harina 000",
            Unit = "kg",
            CurrentStock = 50m,
            MinimumStock = 10m,
            CostPerUnit = 800m,
            IsActive = true
        };

        var cheese = new Supply
        {
            BusinessId = 40,
            Name = "Muzzarella",
            Unit = "kg",
            CurrentStock = 20m,
            MinimumStock = 5m,
            CostPerUnit = 6000m,
            IsActive = true
        };

        db.Supplies.AddRange(flour, cheese);
        await db.SaveChangesAsync();

        var pizza = new Product
        {
            BusinessId = 40,
            Name = "Pizza Muzzarella",
            Price = 9500m,
            IsActive = true
        };
        db.Products.Add(pizza);
        await db.SaveChangesAsync();

        // Recipe: 0.3kg flour + 0.25kg cheese per pizza
        db.RecipeItems.AddRange(
            new RecipeItem { BusinessId = 40, ProductId = pizza.Id, SupplyId = flour.Id, Quantity = 0.300m },
            new RecipeItem { BusinessId = 40, ProductId = pizza.Id, SupplyId = cheese.Id, Quantity = 0.250m }
        );
        await db.SaveChangesAsync();

        // Simulate sale of 2 pizzas: should deduct 0.6kg flour and 0.5kg cheese
        int pizzasSold = 2;
        var recipeItems = await db.RecipeItems.Include(r => r.Supply).Where(r => r.ProductId == pizza.Id).ToListAsync();

        foreach (var r in recipeItems)
        {
            var supply = await db.Supplies.FirstOrDefaultAsync(s => s.Id == r.SupplyId);
            if (supply != null)
            {
                var needed = r.Quantity * pizzasSold;
                var prev = supply.CurrentStock;
                supply.CurrentStock = Math.Max(0, supply.CurrentStock - needed);

                db.InventoryMovements.Add(new InventoryMovement
                {
                    BusinessId = 40,
                    SupplyId = supply.Id,
                    Type = InventoryMovementType.SaleConsumption,
                    Quantity = needed,
                    PreviousStock = prev,
                    NewStock = supply.CurrentStock,
                    Cost = needed * supply.CostPerUnit
                });
            }
        }
        await db.SaveChangesAsync();

        var updatedFlour = await db.Supplies.FindAsync(flour.Id);
        var updatedCheese = await db.Supplies.FindAsync(cheese.Id);

        Assert.Equal(49.4m, updatedFlour!.CurrentStock);
        Assert.Equal(19.5m, updatedCheese!.CurrentStock);

        var movements = await db.InventoryMovements.Where(m => m.Type == InventoryMovementType.SaleConsumption).ToListAsync();
        Assert.Equal(2, movements.Count);
    }

    [Fact]
    public async Task TesterRole_BypassesFeatureLocksAndLimits()
    {
        var tenant = new TenantContext();
        // Set user with Tester role on a business with basic plan
        tenant.Set(businessId: 50, userId: 99, role: UserRoleNames.Tester);

        using var db = CreateDbContext(tenant);
        db.Businesses.Add(new Business { Id = 50, TradeName = "Tester Cafe" });

        var basicPlan = new Plan { Id = 1, Code = "basic", Name = "Plan Básico", MaxProducts = 5 };
        db.Plans.Add(basicPlan);

        var featRecipes = new Feature { Id = 3, Code = FeatureCodes.Recipes, Name = "Recetas", IsPremium = true };
        db.Features.Add(featRecipes);

        // Subscription has expired status to also test EnsureCanOperateAsync bypass
        db.Subscriptions.Add(new Subscription
        {
            BusinessId = 50,
            PlanId = 1,
            Status = SubscriptionStatus.PastDue,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var access = new SubscriptionAccessService(db, tenant);

        // 1. EnsureCanOperateAsync should NOT throw despite subscription being PastDue
        await access.EnsureCanOperateAsync();

        // 2. EnsureFeatureAsync should NOT throw despite feature being premium and plan being basic
        await access.EnsureFeatureAsync(FeatureCodes.Recipes);

        // 3. EnsureLimitAsync should NOT throw even if count (100) > max (5)
        await access.EnsureLimitAsync("products", 100);
    }

    [Fact]
    public async Task DataSeeder_SeedsInternalTesterUserWithCorrectRoleAndCredentials()
    {
        var tenant = new TenantContext();
        tenant.EnableBypass();
        using var db = CreateDbContext(tenant);
        var config = new ConfigurationBuilder().Build();

        await DataSeeder.SeedAsync(db, config);

        var tester = await db.Users.FirstOrDefaultAsync(u => u.Email == "testertld1@gmail.com");
        Assert.NotNull(tester);
        Assert.Equal(UserRole.Tester, tester.Role);
        Assert.True(tester.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("justin1612", tester.PasswordHash));

        var testerBiz = await db.Businesses.FirstOrDefaultAsync(b => b.Id == tester.BusinessId);
        Assert.NotNull(testerBiz);

        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.BusinessId == testerBiz.Id);
        Assert.NotNull(sub);
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.Equal("premium", sub.Plan!.Code);
    }
}
