using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Models;

namespace RotiseriaAPI.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        // 1. Seed Features
        var features = GetStandardFeatures();
        foreach (var f in features)
        {
            var existing = await db.Features.FirstOrDefaultAsync(x => x.Code == f.Code);
            if (existing == null)
            {
                db.Features.Add(f);
            }
            else
            {
                existing.Name = f.Name;
                existing.Description = f.Description;
                existing.Category = f.Category;
                existing.IsPremium = f.IsPremium;
                existing.SortOrder = f.SortOrder;
            }
        }
        await db.SaveChangesAsync();

        var allFeatures = await db.Features.ToListAsync();
        var basicFeatureCodes = new HashSet<string>
        {
            FeatureCodes.Products,
            FeatureCodes.Orders,
            FeatureCodes.Tables,
            FeatureCodes.Customers,
            FeatureCodes.Cash,
            FeatureCodes.BasicStock,
            FeatureCodes.BasicReports,
            FeatureCodes.BusinessSettings
        };

        // 2. Seed Plans: Básico y Premium
        var basicPlan = await db.Plans.Include(p => p.PlanFeatures).FirstOrDefaultAsync(p => p.Code == "basic" || p.Code == "starter");
        if (basicPlan == null)
        {
            basicPlan = new Plan
            {
                Code = "basic",
                Name = "Plan Básico",
                Description = "Todo lo necesario para operar pedidos, mostrador, delivery, mesas, clientes y caja diaria.",
                Currency = config["Plans:Basic:Currency"] ?? "ARS",
                MonthlyPrice = ParsePrice(config["Plans:Basic:MonthlyPrice"] ?? "15000"),
                TrialDays = ParseInt(config["Plans:Basic:TrialDays"], 14),
                MaxUsers = ParseInt(config["Plans:Basic:MaxUsers"], 3),
                MaxProducts = ParseInt(config["Plans:Basic:MaxProducts"], 100),
                MaxTables = ParseInt(config["Plans:Basic:MaxTables"], 15),
                MaxCustomers = ParseInt(config["Plans:Basic:MaxCustomers"], 250),
                MaxBranches = 1,
                ReportsEnabled = true,
                PremiumFeaturesEnabled = false,
                SortOrder = 1
            };
            db.Plans.Add(basicPlan);
            await db.SaveChangesAsync();
        }
        else
        {
            basicPlan.Code = "basic";
            basicPlan.Name = "Plan Básico";
            basicPlan.MaxUsers = Math.Max(basicPlan.MaxUsers, 3);
            basicPlan.MaxProducts = Math.Max(basicPlan.MaxProducts, 100);
            basicPlan.MaxTables = Math.Max(basicPlan.MaxTables, 15);
            basicPlan.MaxCustomers = 250;
            basicPlan.MaxBranches = 1;
        }

        var premiumPlan = await db.Plans.Include(p => p.PlanFeatures).FirstOrDefaultAsync(p => p.Code == "premium" || p.Code == "pro");
        if (premiumPlan == null)
        {
            premiumPlan = new Plan
            {
                Code = "premium",
                Name = "Plan Premium",
                Description = "Gestión gastronómica total: KDS cocina, insumos, recetas y escandallo, mermas, reservas, reportes avanzados y división de cuentas.",
                Currency = config["Plans:Premium:Currency"] ?? "ARS",
                MonthlyPrice = ParsePrice(config["Plans:Premium:MonthlyPrice"] ?? "35000"),
                TrialDays = ParseInt(config["Plans:Premium:TrialDays"], 14),
                MaxUsers = ParseInt(config["Plans:Premium:MaxUsers"], 25),
                MaxProducts = ParseInt(config["Plans:Premium:MaxProducts"], 1000),
                MaxTables = ParseInt(config["Plans:Premium:MaxTables"], 100),
                MaxCustomers = ParseInt(config["Plans:Premium:MaxCustomers"], 5000),
                MaxBranches = 5,
                ReportsEnabled = true,
                PremiumFeaturesEnabled = true,
                SortOrder = 2
            };
            db.Plans.Add(premiumPlan);
            await db.SaveChangesAsync();
        }
        else
        {
            premiumPlan.Code = "premium";
            premiumPlan.Name = "Plan Premium";
            premiumPlan.MaxUsers = Math.Max(premiumPlan.MaxUsers, 25);
            premiumPlan.MaxProducts = Math.Max(premiumPlan.MaxProducts, 1000);
            premiumPlan.MaxTables = Math.Max(premiumPlan.MaxTables, 100);
            premiumPlan.MaxCustomers = 5000;
            premiumPlan.MaxBranches = 5;
            premiumPlan.PremiumFeaturesEnabled = true;
        }
        await db.SaveChangesAsync();

        // 3. Link PlanFeatures
        // Basic: only basicFeatureCodes
        foreach (var feat in allFeatures.Where(f => basicFeatureCodes.Contains(f.Code)))
        {
            if (!basicPlan.PlanFeatures.Any(pf => pf.FeatureId == feat.Id))
            {
                db.PlanFeatures.Add(new PlanFeature { PlanId = basicPlan.Id, FeatureId = feat.Id });
            }
        }

        // Premium: ALL features
        foreach (var feat in allFeatures)
        {
            if (!premiumPlan.PlanFeatures.Any(pf => pf.FeatureId == feat.Id))
            {
                db.PlanFeatures.Add(new PlanFeature { PlanId = premiumPlan.Id, FeatureId = feat.Id });
            }
        }
        await db.SaveChangesAsync();

        // 4. Seed Legal Documents if not present
        if (!await db.LegalDocuments.AnyAsync())
        {
            const string placeholder = """
                DOCUMENTO BORRADOR — NO CONSTITUYE ASESORAMIENTO LEGAL.

                Razón social: [COMPLETAR]
                CUIT: [COMPLETAR]
                Domicilio: [COMPLETAR]
                Email de contacto: [COMPLETAR]

                Este texto es un placeholder estructural. Debe ser reemplazado por una versión
                revisada profesionalmente antes de ofrecer el servicio al público.
                """;

            db.LegalDocuments.AddRange(
                NewLegal(LegalDocumentType.Terms, "Términos y Condiciones", placeholder),
                NewLegal(LegalDocumentType.Privacy, "Política de Privacidad", placeholder),
                NewLegal(LegalDocumentType.Subscription, "Términos de Suscripción", placeholder),
                NewLegal(LegalDocumentType.Cancellation, "Cancelación y Baja", placeholder),
                NewLegal(LegalDocumentType.Support, "Soporte", placeholder)
            );
            await db.SaveChangesAsync();
        }

        // 5. Seed Internal Testing User (testertld1@gmail.com / justin1612)
        var testerEmail = "testertld1@gmail.com";
        var existingTester = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == testerEmail);
        if (existingTester == null)
        {
            var testBusiness = await db.Businesses.IgnoreQueryFilters().FirstOrDefaultAsync();
            if (testBusiness == null)
            {
                testBusiness = new Business
                {
                    TradeName = "SaaS Gastronómico (Pruebas)",
                    Email = testerEmail,
                    BusinessType = BusinessType.General,
                    TablesEnabled = true,
                    DeliveryEnabled = true,
                    TakeawayEnabled = true,
                    KitchenEnabled = true,
                    ReservationsEnabled = true,
                    CashControlEnabled = true,
                    InventoryEnabled = true,
                    OnboardingCompleted = true,
                    OnboardingStep = OnboardingStep.Completed
                };
                db.Businesses.Add(testBusiness);
                await db.SaveChangesAsync();
            }

            var activeSub = await db.Subscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.BusinessId == testBusiness.Id);
            if (activeSub == null)
            {
                db.Subscriptions.Add(new Subscription
                {
                    BusinessId = testBusiness.Id,
                    PlanId = premiumPlan.Id,
                    Status = SubscriptionStatus.Active,
                    StartedAtUtc = DateTime.UtcNow,
                    CurrentPeriodStartUtc = DateTime.UtcNow,
                    CurrentPeriodEndUtc = DateTime.UtcNow.AddYears(10)
                });
                await db.SaveChangesAsync();
            }

            var testerUser = new User
            {
                BusinessId = testBusiness.Id,
                Email = testerEmail,
                FullName = "Tester Interno",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("justin1612"),
                Role = UserRole.Tester,
                IsActive = true
            };
            db.Users.Add(testerUser);
            await db.SaveChangesAsync();
        }
        else
        {
            existingTester.Role = UserRole.Tester;
            existingTester.PasswordHash = BCrypt.Net.BCrypt.HashPassword("justin1612");
            existingTester.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    private static List<Feature> GetStandardFeatures() => new()
    {
        // Básico
        new Feature { Code = FeatureCodes.Products, Name = "Gestión de Productos", Description = "Administración de carta, productos, categorías y precios", Category = "Catálogo", IsPremium = false, SortOrder = 1 },
        new Feature { Code = FeatureCodes.Orders, Name = "Toma de Pedidos", Description = "Venta en mostrador y delivery", Category = "Ventas", IsPremium = false, SortOrder = 2 },
        new Feature { Code = FeatureCodes.Tables, Name = "Gestión de Mesas", Description = "Mapa de mesas, apertura, adición y cierre", Category = "Salón", IsPremium = false, SortOrder = 3 },
        new Feature { Code = FeatureCodes.Customers, Name = "Clientes y Fiados", Description = "Registro de clientes y saldo de deuda básico", Category = "Clientes", IsPremium = false, SortOrder = 4 },
        new Feature { Code = FeatureCodes.Cash, Name = "Control de Caja", Description = "Apertura, cierre, arqueo e ingresos/egresos de turno", Category = "Caja", IsPremium = false, SortOrder = 5 },
        new Feature { Code = FeatureCodes.BasicStock, Name = "Stock Básico", Description = "Control de cantidad de productos para la venta", Category = "Stock", IsPremium = false, SortOrder = 6 },
        new Feature { Code = FeatureCodes.BasicReports, Name = "Dashboard y Reportes Básicos", Description = "Métricas diarias de venta y canales", Category = "Reportes", IsPremium = false, SortOrder = 7 },
        new Feature { Code = FeatureCodes.BusinessSettings, Name = "Configuración del Local", Description = "Datos fiscales, teléfonos y preferencias", Category = "Configuración", IsPremium = false, SortOrder = 8 },

        // Premium
        new Feature { Code = FeatureCodes.AdvancedInventory, Name = "Insumos y Stock Avanzado", Description = "Materias primas, stock mínimo, alertas, compras a proveedores y mermas", Category = "Stock", IsPremium = true, SortOrder = 9 },
        new Feature { Code = FeatureCodes.Recipes, Name = "Recetas y Escandallo", Description = "Ficha técnica de productos con descuento automático de insumos en cada venta", Category = "Stock", IsPremium = true, SortOrder = 10 },
        new Feature { Code = FeatureCodes.Kds, Name = "Monitor de Cocina (KDS)", Description = "Pantalla de comandas en tiempo real con SignalR y alertas de demora", Category = "Cocina", IsPremium = true, SortOrder = 11 },
        new Feature { Code = FeatureCodes.Reservations, Name = "Gestión de Reservas", Description = "Calendario de reservas de mesas por horario y cantidad de comensales", Category = "Salón", IsPremium = true, SortOrder = 12 },
        new Feature { Code = FeatureCodes.SplitBills, Name = "División de Cuentas y Propinas", Description = "Dividir mesa entre comensales por producto o parte igual y propinas", Category = "Salón", IsPremium = true, SortOrder = 13 },
        new Feature { Code = FeatureCodes.ModifiersCombos, Name = "Modificadores y Combos", Description = "Adicionales, puntos de cocción y armado de combos", Category = "Catálogo", IsPremium = true, SortOrder = 14 },
        new Feature { Code = FeatureCodes.EmployeeConsumption, Name = "Consumo de Empleados", Description = "Registro de comida/bebida para personal interno", Category = "Administración", IsPremium = true, SortOrder = 15 },
        new Feature { Code = FeatureCodes.AdvancedReports, Name = "Reportes Avanzados y Márgenes", Description = "Costos, margen de ganancia estimada, comparativa de períodos y exportación CSV", Category = "Reportes", IsPremium = true, SortOrder = 16 },
        new Feature { Code = FeatureCodes.AdvancedPermissions, Name = "Permisos y Roles Avanzados", Description = "Roles granulares de cajero, mozo, cocina y gerente", Category = "Seguridad", IsPremium = true, SortOrder = 17 },
        new Feature { Code = FeatureCodes.Audit, Name = "Auditoría Completa", Description = "Registro forense de todas las acciones del sistema", Category = "Seguridad", IsPremium = true, SortOrder = 18 }
    };

    private static LegalDocument NewLegal(LegalDocumentType type, string title, string body) => new()
    {
        Type = type,
        Version = "0.1-draft",
        Title = title,
        Body = body,
        EffectiveAtUtc = DateTime.UtcNow,
        IsCurrent = true
    };

    private static decimal ParsePrice(string? value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var n) ? n : fallback;
}
