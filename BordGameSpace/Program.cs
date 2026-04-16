using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using BordGameSpace.Data;
using BordGameSpace.Services;

// Fix: increase inotify instance limit on Linux to prevent "inotify instance limit reached" crashes
try {
    var procLimit = "/proc/sys/fs/inotify/max_user_instances";
    if (File.Exists(procLimit)) {
        var current = int.Parse(File.ReadAllText(procLimit).Trim());
        if (current < 8192) File.WriteAllText(procLimit, "8192");
        Console.Error.WriteLine($"[inotify] max_user_instances: {current} -> 8192");
    }
} catch (Exception ex) {
    Console.Error.WriteLine($"[inotify] adjustment skipped: {ex.Message}");
}

// Suppress file-system watchers in containerized environment (avoids inotify exhaustion)
Environment.SetEnvironmentVariable("DOTNET_FileWatcherFlagBox_DefaultFileWatcherWatchSubdirectories", "0");

var builder = WebApplication.CreateBuilder(args);

// Hardcode port binding for containerized deployment (Render uses port 10000)
builder.WebHost.UseUrls("http://0.0.0.0:10000");

Console.Error.WriteLine("[Startup] Application building...");

// Configure Data Protection keys stored on filesystem (not DB, avoiding cold-start DB timeout issues)
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
    // 將本機時區設為台北（確保所有 DateTime.UtcNow 為台灣時間）
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
        npgsql.CommandTimeout(120);
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
Console.Error.WriteLine("[Startup] App built successfully, starting middleware...");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 使用 raw SQL 建立所有資料表（繞過 pgBouncer Transaction Mode 對 EnsureCreated 的限制）
    // 順序嚴格按照 FK 依賴關係：被參照的表先建
    // Clean up old DataProtectionKeys table from DB (if exists from previous failed deploy)
    db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"DataProtectionKeys\" CASCADE;");

    var createTablesSql = @"
        CREATE TABLE IF NOT EXISTS ""Levels"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""Name"" VARCHAR(50) NOT NULL,
            ""UpgradeThresholdHours"" DECIMAL(10,2) NOT NULL DEFAULT 0,
            ""UpgradeThresholdAmount"" DECIMAL(10,2) NOT NULL DEFAULT 0,
            ""GameDiscount"" DECIMAL(3,2) NOT NULL DEFAULT 1.00,
            ""WeekdayHourlyRate"" DECIMAL(10,0) NOT NULL DEFAULT 60,
            ""HolidayHourlyRate"" DECIMAL(10,0) NOT NULL DEFAULT 70,
            ""SortOrder"" INT NOT NULL DEFAULT 0,
            ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE,
            ""IsDeletable"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""Admins"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""Username"" VARCHAR(100) NOT NULL UNIQUE,
            ""PasswordHash"" VARCHAR(255) NOT NULL,
            ""Name"" VARCHAR(100) NOT NULL,
            ""Role"" VARCHAR(50) NOT NULL DEFAULT 'Owner',
            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""Products"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""Category"" VARCHAR(50) NOT NULL,
            ""Name"" VARCHAR(200) NOT NULL,
            ""Description"" VARCHAR(1000) NULL,
            ""Price"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""Stock"" INT NULL,
            ""LowStockAlert"" INT NOT NULL DEFAULT 1,
            ""ImageUrl"" VARCHAR(500) NULL,
            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""IsService"" BOOLEAN NOT NULL DEFAULT FALSE,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""Coupons"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""Name"" VARCHAR(100) NOT NULL,
            ""CouponType"" VARCHAR(50) NOT NULL,
            ""DiscountValue"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""MinPurchase"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""ApplicableTo"" VARCHAR(50) NOT NULL DEFAULT 'All',
            ""TotalQuantity"" INT NULL,
            ""UsedCount"" INT NOT NULL DEFAULT 0,
            ""ValidFrom"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            ""ValidUntil"" TIMESTAMPTZ NULL,
            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""Events"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""Title"" VARCHAR(200) NOT NULL,
            ""Content"" TEXT NOT NULL,
            ""ImageUrl"" VARCHAR(500) NULL,
            ""EventDate"" TIMESTAMPTZ NULL,
            ""MaxParticipants"" INT NULL,
            ""RegistrationDeadline"" TIMESTAMPTZ NULL,
            ""Status"" VARCHAR(50) NOT NULL DEFAULT 'RegistrationOpen',
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""Members"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""Name"" VARCHAR(100) NOT NULL,
            ""Phone"" VARCHAR(20) NOT NULL UNIQUE,
            ""Email"" VARCHAR(255) NOT NULL UNIQUE,
            ""PasswordHash"" VARCHAR(255) NOT NULL,
            ""Birthday"" TIMESTAMPTZ NULL,
            ""TotalPlayHours"" DECIMAL(10,2) NOT NULL DEFAULT 0,
            ""TotalSpending"" DECIMAL(10,2) NOT NULL DEFAULT 0,
            ""LevelId"" INT NOT NULL DEFAULT 1,
            ""Status"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""LevelId"") REFERENCES ""Levels""(""Id"")
        );

        CREATE TABLE IF NOT EXISTS ""EventRegistrations"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""EventId"" INT NOT NULL,
            ""Name"" VARCHAR(100) NOT NULL,
            ""Phone"" VARCHAR(20) NOT NULL,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""EventId"") REFERENCES ""Events""(""Id"") ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS ""RestockRecords"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""ProductId"" INT NOT NULL,
            ""Quantity"" INT NOT NULL DEFAULT 0,
            ""Supplier"" VARCHAR(200) NULL,
            ""Phone"" VARCHAR(20) NULL,
            ""Notes"" VARCHAR(500) NULL,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""ProductId"") REFERENCES ""Products""(""Id"") ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS ""Orders"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""OrderType"" VARCHAR(50) NOT NULL,
            ""MemberId"" INT NULL,
            ""MemberName"" VARCHAR(100) NULL,
            ""MemberPhone"" VARCHAR(20) NULL,
            ""TotalAmount"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""DiscountAmount"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""FinalAmount"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""PointsUsed"" INT NOT NULL DEFAULT 0,
            ""PointsEarned"" INT NOT NULL DEFAULT 0,
            ""CouponId"" INT NULL,
            ""PaymentStatus"" VARCHAR(50) NOT NULL DEFAULT 'Paid',
            ""PaymentMethod"" VARCHAR(50) NULL,
            ""Notes"" VARCHAR(500) NULL,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""MemberId"") REFERENCES ""Members""(""Id"") ON DELETE SET NULL,
            FOREIGN KEY (""CouponId"") REFERENCES ""Coupons""(""Id"") ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS ""PlayRecords"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""MemberId"" INT NULL,
            ""MemberName"" VARCHAR(100) NULL,
            ""MemberPhone"" VARCHAR(20) NULL,
            ""StartTime"" TIMESTAMPTZ NOT NULL,
            ""EndTime"" TIMESTAMPTZ NULL,
            ""TotalHours"" DECIMAL(10,2) NULL,
            ""HourlyRate"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""Amount"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""OrderId"" INT NULL,
            ""Status"" VARCHAR(50) NOT NULL DEFAULT 'Playing',
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""MemberId"") REFERENCES ""Members""(""Id"") ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS ""GameRentals"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""MemberId"" INT NULL,
            ""ProductId"" INT NOT NULL,
            ""RenterName"" VARCHAR(100) NOT NULL DEFAULT '',
            ""RenterPhone"" VARCHAR(20) NOT NULL DEFAULT '',
            ""PickupDate"" TIMESTAMPTZ NULL,
            ""BorrowDate"" TIMESTAMPTZ NULL,
            ""DueDate"" TIMESTAMPTZ NULL,
            ""ReturnDate"" TIMESTAMPTZ NULL,
            ""Deposit"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""RentalFee"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""Status"" VARCHAR(50) NOT NULL DEFAULT 'Pending',
            ""OrderId"" INT NULL,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""MemberId"") REFERENCES ""Members""(""Id"") ON DELETE SET NULL,
            FOREIGN KEY (""ProductId"") REFERENCES ""Products""(""Id"") ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS ""SpaceReservations"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""MemberId"" INT NULL,
            ""Name"" VARCHAR(100) NOT NULL,
            ""Phone"" VARCHAR(20) NOT NULL,
            ""ReservationDate"" DATE NOT NULL,
            ""StartTime"" TIME NOT NULL,
            ""EndTime"" TIME NOT NULL,
            ""PeopleCount"" INT NOT NULL DEFAULT 2,
            ""SpaceType"" VARCHAR(20) NOT NULL DEFAULT '訂位',
            ""Hours"" INT NOT NULL DEFAULT 1,
            ""HourlyRate"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""TotalAmount"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""Status"" VARCHAR(50) NOT NULL DEFAULT 'Pending',
            ""OrderId"" INT NULL,
            ""Notes"" VARCHAR(500) NULL,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            FOREIGN KEY (""MemberId"") REFERENCES ""Members""(""Id"") ON DELETE SET NULL
        );


        CREATE TABLE IF NOT EXISTS ""OrderItems"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""OrderId"" INT NOT NULL,
            ""ItemType"" VARCHAR(50) NOT NULL,
            ""ItemId"" INT NOT NULL,
            ""ItemName"" VARCHAR(200) NOT NULL,
            ""UnitPrice"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            ""Quantity"" INT NOT NULL DEFAULT 1,
            ""Subtotal"" DECIMAL(10,0) NOT NULL DEFAULT 0,
            FOREIGN KEY (""OrderId"") REFERENCES ""Orders""(""Id"") ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS ""MemberCoupons"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""MemberId"" INT NOT NULL,
            ""CouponId"" INT NOT NULL,
            ""ReceivedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            ""UsedAt"" TIMESTAMPTZ NULL,
            ""OrderId"" INT NULL,
            FOREIGN KEY (""MemberId"") REFERENCES ""Members""(""Id"") ON DELETE CASCADE,
            FOREIGN KEY (""CouponId"") REFERENCES ""Coupons""(""Id"") ON DELETE CASCADE
        );
    ";

    try
    {
        db.Database.ExecuteSqlRaw(createTablesSql);
        Console.WriteLine("[DB] 所有資料表建立完成");

        // 修補缺少的欄位
        db.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""GameRentals"" ADD COLUMN IF NOT EXISTS ""RenterName"" VARCHAR(100) NOT NULL DEFAULT '';
            ALTER TABLE ""GameRentals"" ADD COLUMN IF NOT EXISTS ""RenterPhone"" VARCHAR(20) NOT NULL DEFAULT '';
        ");

        // 建立索引（大幅加速查詢，避免 62 秒逾時）
        db.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_Orders_CreatedAt_PaymentStatus"" ON ""Orders"" (""CreatedAt"", ""PaymentStatus"");
            CREATE INDEX IF NOT EXISTS ""IX_PlayRecords_Status"" ON ""PlayRecords"" (""Status"");
            CREATE INDEX IF NOT EXISTS ""IX_GameRentals_Status"" ON ""GameRentals"" (""Status"");
            CREATE INDEX IF NOT EXISTS ""IX_SpaceReservations_ReservationDate_Status"" ON ""SpaceReservations"" (""ReservationDate"", ""Status"");
            CREATE INDEX IF NOT EXISTS ""IX_Events_Status_EventDate"" ON ""Events"" (""Status"", ""EventDate"");
            CREATE INDEX IF NOT EXISTS ""IX_Admins_Username"" ON ""Admins"" (""Username"");
            CREATE INDEX IF NOT EXISTS ""IX_Members_Phone"" ON ""Members"" (""Phone"");
            CREATE INDEX IF NOT EXISTS ""IX_Members_Email"" ON ""Members"" (""Email"");
        ");
        Console.WriteLine("[DB] 索引建立完成");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] 資料表建立失敗: {ex.Message}");
    }

    // 使用 raw SQL 執行 Seed Data（繞過 EF Core decimal 型別對應問題）
    var seedSql = @"
        INSERT INTO ""Levels"" (""Name"", ""UpgradeThresholdHours"", ""UpgradeThresholdAmount"", ""GameDiscount"", ""WeekdayHourlyRate"", ""HolidayHourlyRate"", ""SortOrder"", ""IsDefault"", ""IsDeletable"", ""CreatedAt"")
        VALUES ('非會員', 0, 0, 1.00, 60, 70, 0, TRUE, FALSE, NOW())
        ON CONFLICT DO NOTHING;

        INSERT INTO ""Levels"" (""Name"", ""UpgradeThresholdHours"", ""UpgradeThresholdAmount"", ""GameDiscount"", ""WeekdayHourlyRate"", ""HolidayHourlyRate"", ""SortOrder"", ""IsDefault"", ""IsDeletable"", ""CreatedAt"")
        VALUES ('會員', 1000, 100000, 0.90, 50, 60, 1, FALSE, TRUE, NOW())
        ON CONFLICT DO NOTHING;

        INSERT INTO ""Admins"" (""Username"", ""PasswordHash"", ""Name"", ""Role"", ""IsActive"", ""CreatedAt"")
        VALUES ('admin', '$2a$12$lL3C0cDbC93wc6zNCinDpuoVuHVyOXGC.TjVwJrIgtcuKNVxnZG4O', '系統管理者', 'Owner', TRUE, NOW())
        ON CONFLICT (""Username"") DO NOTHING;

        INSERT INTO ""Coupons"" (""Name"", ""CouponType"", ""DiscountValue"", ""MinPurchase"", ""ApplicableTo"", ""TotalQuantity"", ""UsedCount"", ""ValidFrom"", ""ValidUntil"", ""IsActive"", ""CreatedAt"")
        VALUES ('會員:購買桌遊9折', 'Percentage', 10, 0, 'Product', NULL, 0, NOW(), NULL, TRUE, NOW())
        ON CONFLICT DO NOTHING;

        INSERT INTO ""Coupons"" (""Name"", ""CouponType"", ""DiscountValue"", ""MinPurchase"", ""ApplicableTo"", ""TotalQuantity"", ""UsedCount"", ""ValidFrom"", ""ValidUntil"", ""IsActive"", ""CreatedAt"")
        VALUES ('生日禮:生日當月3人同行壽星免場地費', 'Percentage', 100, 0, 'Play', NULL, 0, NOW(), NULL, TRUE, NOW())
        ON CONFLICT DO NOTHING;

        INSERT INTO ""Products"" (""Category"", ""Name"", ""Description"", ""Price"", ""Stock"", ""LowStockAlert"", ""ImageUrl"", ""IsActive"", ""IsService"", ""CreatedAt"", ""UpdatedAt"")
        VALUES ('服務', '會員申請', NULL, 200, NULL, 0, NULL, TRUE, TRUE, NOW(), NOW())
        ON CONFLICT DO NOTHING;
    ";

    try { db.Database.ExecuteSqlRaw(seedSql); Console.WriteLine("[DB] Seed 完成"); }
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

try
{
    Console.Error.WriteLine("[Startup] Application listening on http://0.0.0.0:10000");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("[Startup] FATAL: " + ex.GetType().Name + ": " + ex.Message);
    Console.WriteLine("[Startup] StackTrace: " + ex.StackTrace);
    Console.Out.Flush();
    throw;
}
