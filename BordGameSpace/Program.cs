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

// Configure PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    db.Database.EnsureCreated();
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
