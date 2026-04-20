using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using BordGameSpace.Data;
using BordGameSpace.Services;

var builder = WebApplication.CreateBuilder(args);

Console.Error.WriteLine("[Startup] Application building...");

// Data Protection keys stored on filesystem (Azure App Service has persistent local storage)
var keysDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data", "protection-keys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .SetApplicationName("BoardMemberApp")
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

// 設定時區為台北 (UTC+8)
try
{
    var taiwanZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time")
                  ?? TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")
                  ?? TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    TimeZoneInfo.ClearCachedData();
    TimeZoneInfo localZone = TimeZoneInfo.Local;
    Console.WriteLine($"[TimeZone] Current: {localZone.DisplayName}, Taiwan: {taiwanZone?.DisplayName}");
}
catch (Exception ex)
{
    Console.WriteLine($"[TimeZone] 設定失敗: {ex.Message}");
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connStr);
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
Console.Error.WriteLine("[Startup] App built successfully, starting middleware...");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Console.WriteLine("[DB] Migration complete");
}

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

try
{
    Console.Error.WriteLine("[Startup] Application listening on http://0.0.0.0:8080");
    Console.Error.Flush();
    app.Run();
    Console.Error.WriteLine("[Startup] app.Run() returned (should never reach here)");
    Console.Error.Flush();
}
catch (Exception ex)
{
    Console.WriteLine("[Startup] FATAL: " + ex.GetType().Name + ": " + ex.Message);
    Console.WriteLine("[Startup] StackTrace: " + ex.StackTrace);
    Console.Out.Flush();
    throw;
}
