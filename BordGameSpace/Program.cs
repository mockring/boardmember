using Microsoft.EntityFrameworkCore;
using BordGameSpace.Data;
using BordGameSpace.Services;

var builder = WebApplication.CreateBuilder(args);

// 設定時區為台北 (UTC+8)
try
{
    var taiwanZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time")
                  ?? TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")
                  ?? TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    TimeZoneInfo.ClearCachedData();
    TimeZoneInfo localZone = TimeZoneInfo.Local;
    // 將本機時區設為台北（確保所有 DateTime.Now 為台灣時間）
    // 注意：這只是讓 .NET 取用本機時間時是正確的，伺服器本身的 tzdata 必須正確
    Console.WriteLine($"[TimeZone] Current: {localZone.DisplayName}, Taiwan: {taiwanZone?.DisplayName}");
}
catch (Exception ex)
{
    Console.WriteLine($"[TimeZone] 設定失敗: {ex.Message}");
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure PostgreSQL with connection resilience
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connStr, npgsql =>
    {
        npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        npgsql.CommandTimeout(30);
    });
});

// Register services
builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<PosService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<SpaceReservationService>();
builder.Services.AddScoped<GameRentalService>();
builder.Services.AddScoped<ReportService>();

// Configure Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7); // Session timeout 7 days
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".BordGameSpace.Session";
});

var app = builder.Build();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 建立資料表（全新資料庫直接用 EnsureCreated 最穩定）
    try
    {
        db.Database.EnsureCreated();
        Console.WriteLine("[DB] 資料表建立完成");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] 建立失敗: {ex.Message}");
    }

    // Seed Levels
    if (!db.Levels.Any())
    {
        db.Levels.AddRange(
            new BordGameSpace.Models.Level
            {
                Name = "非會員",
                UpgradeThresholdHours = 0,
                UpgradeThresholdAmount = 0,
                GameDiscount = 1.00m,
                WeekdayHourlyRate = 60,
                HolidayHourlyRate = 70,
                SortOrder = 0,
                IsDefault = true,
                IsDeletable = false,
                CreatedAt = new DateTime(2024, 1, 1)
            },
            new BordGameSpace.Models.Level
            {
                Name = "會員",
                UpgradeThresholdHours = 1000,
                UpgradeThresholdAmount = 100000,
                GameDiscount = 0.90m,
                WeekdayHourlyRate = 50,
                HolidayHourlyRate = 60,
                SortOrder = 1,
                IsDefault = false,
                IsDeletable = true,
                CreatedAt = new DateTime(2024, 1, 1)
            }
        );
    }

    // Seed Admin
    if (!db.Admins.Any())
    {
        db.Admins.Add(new BordGameSpace.Models.Admin
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
            Name = "系統管理者",
            Role = "Owner",
            IsActive = true,
            CreatedAt = DateTime.Now
        });
    }

    // Seed Coupons
    if (!db.Coupons.Any())
    {
        db.Coupons.AddRange(
            new BordGameSpace.Models.Coupon
            {
                Name = "會員:購買桌遊9折",
                CouponType = "Percentage",
                DiscountValue = 10,
                MinPurchase = 0,
                ApplicableTo = "Product",
                TotalQuantity = null,
                UsedCount = 0,
                ValidFrom = new DateTime(2026, 1, 1),
                ValidUntil = null,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new BordGameSpace.Models.Coupon
            {
                Name = "生日禮:生日當月3人同行壽星免場地費",
                CouponType = "Percentage",
                DiscountValue = 100,
                MinPurchase = 0,
                ApplicableTo = "Play",
                TotalQuantity = null,
                UsedCount = 0,
                ValidFrom = new DateTime(2026, 1, 1),
                ValidUntil = null,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            }
        );
    }

    // Seed Products
    if (!db.Products.Any())
    {
        db.Products.Add(new BordGameSpace.Models.Product
        {
            Category = "服務",
            Name = "會員申請",
            Description = null,
            Price = 200,
            Stock = null,
            LowStockAlert = 0,
            ImageUrl = null,
            IsActive = true,
            IsService = true,
            CreatedAt = new DateTime(2026, 1, 1),
            UpdatedAt = new DateTime(2026, 1, 1)
        });
    }

    try { db.SaveChanges(); }
    catch (Exception ex) { Console.WriteLine($"[Seed] 錯誤: {ex.Message}"); }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Enable Session

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
