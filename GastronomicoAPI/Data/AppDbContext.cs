using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Models;
using RotiseriaAPI.Security;

namespace RotiseriaAPI.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<EmployeeConsumption> EmployeeConsumptions => Set<EmployeeConsumption>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<BillingWebhookEvent> BillingWebhookEvents => Set<BillingWebhookEvent>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Nuevas entidades operativas y premium
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<SupplierPurchase> SupplierPurchases => Set<SupplierPurchase>();
    public DbSet<SupplierPurchaseItem> SupplierPurchaseItems => Set<SupplierPurchaseItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Business>(e =>
        {
            e.HasIndex(x => x.Email);
            e.Property(x => x.TradeName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => new { x.BusinessId, x.Email });
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
            e.HasOne(x => x.Business).WithMany(b => b.Users).HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Name }).IsUnique();
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Name });
            e.HasIndex(x => x.CategoryId);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Name });
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Date });
            e.HasIndex(x => new { x.BusinessId, x.Status });
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.DeliveryCost).HasPrecision(18, 2);
            e.Property(x => x.TipAmount).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Ignore(x => x.Subtotal);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Table>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Number }).IsUnique();
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Debt>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<EmployeeConsumption>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<Plan>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Feature>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<PlanFeature>(e =>
        {
            e.HasKey(x => new { x.PlanId, x.FeatureId });
            e.HasOne(x => x.Plan).WithMany(p => p.PlanFeatures).HasForeignKey(x => x.PlanId);
            e.HasOne(x => x.Feature).WithMany(f => f.PlanFeatures).HasForeignKey(x => x.FeatureId);
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.HasIndex(x => x.BusinessId).IsUnique();
            e.HasOne(x => x.Business).WithOne(b => b.Subscription).HasForeignKey<Subscription>(x => x.BusinessId);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillingWebhookEvent>(e =>
        {
            e.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
        });

        modelBuilder.Entity<LegalDocument>(e =>
        {
            e.HasIndex(x => new { x.Type, x.Version }).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasIndex(x => x.TokenHash);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).IsRequired(false);
        });

        // Configuración de Caja
        modelBuilder.Entity<CashShift>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.IsOpen });
            e.Property(x => x.InitialCash).HasPrecision(18, 2);
            e.Property(x => x.ActualCash).HasPrecision(18, 2);
            e.Property(x => x.Difference).HasPrecision(18, 2);
            e.Property(x => x.TotalCashSales).HasPrecision(18, 2);
            e.Property(x => x.TotalCardSales).HasPrecision(18, 2);
            e.Property(x => x.TotalTransferSales).HasPrecision(18, 2);
            e.Property(x => x.TotalOtherSales).HasPrecision(18, 2);
            e.Property(x => x.TotalInflows).HasPrecision(18, 2);
            e.Property(x => x.TotalOutflows).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
            e.HasMany(x => x.Movements).WithOne(m => m.CashShift).HasForeignKey(m => m.CashShiftId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CashMovement>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        // Configuración de Insumos y Recetas
        modelBuilder.Entity<Supply>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Name });
            e.Property(x => x.CurrentStock).HasPrecision(18, 3);
            e.Property(x => x.MinimumStock).HasPrecision(18, 3);
            e.Property(x => x.CostPerUnit).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<RecipeItem>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.ProductId, x.SupplyId }).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Supply).WithMany().HasForeignKey(x => x.SupplyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryMovement>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.PreviousStock).HasPrecision(18, 3);
            e.Property(x => x.NewStock).HasPrecision(18, 3);
            e.Property(x => x.Cost).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        modelBuilder.Entity<SupplierPurchase>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Date });
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
            e.HasMany(x => x.Items).WithOne(i => i.SupplierPurchase).HasForeignKey(i => i.SupplierPurchaseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierPurchaseItem>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
        });

        // Configuración de Reservas
        modelBuilder.Entity<Reservation>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.ReservationDate });
            e.HasQueryFilter(x => _tenant.BypassFilters || (_tenant.BusinessId.HasValue && x.BusinessId == _tenant.BusinessId));
            e.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
