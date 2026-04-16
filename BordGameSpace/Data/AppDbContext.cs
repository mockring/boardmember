using Microsoft.EntityFrameworkCore;
using BordGameSpace.Models;

namespace BordGameSpace.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members { get; set; }
    public DbSet<Level> Levels { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<MemberCoupon> MemberCoupons { get; set; }
    public DbSet<PlayRecord> PlayRecords { get; set; }
    public DbSet<GameRental> GameRentals { get; set; }
    public DbSet<SpaceReservation> SpaceReservations { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventRegistration> EventRegistrations { get; set; }
    public DbSet<RestockRecord> RestockRecords { get; set; }
    public DbSet<Admin> Admins { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Member - Unique constraints
        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Phone)
            .IsUnique();

        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Email)
            .IsUnique();

        // Member - Level relationship
        modelBuilder.Entity<Member>()
            .HasOne(m => m.Level)
            .WithMany(l => l.Members)
            .HasForeignKey(m => m.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Admin - Unique username
        modelBuilder.Entity<Admin>()
            .HasIndex(a => a.Username)
            .IsUnique();

        // Order - Member relationship (optional)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Member)
            .WithMany(m => m.Orders)
            .HasForeignKey(o => o.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order - Coupon relationship (optional)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Coupon)
            .WithMany()
            .HasForeignKey(o => o.CouponId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrderItem - Order relationship
        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // MemberCoupon - Member relationship
        modelBuilder.Entity<MemberCoupon>()
            .HasOne(mc => mc.Member)
            .WithMany(m => m.MemberCoupons)
            .HasForeignKey(mc => mc.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // MemberCoupon - Coupon relationship
        modelBuilder.Entity<MemberCoupon>()
            .HasOne(mc => mc.Coupon)
            .WithMany(c => c.MemberCoupons)
            .HasForeignKey(mc => mc.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        // PlayRecord - Member relationship (optional)
        modelBuilder.Entity<PlayRecord>()
            .HasOne(pr => pr.Member)
            .WithMany(m => m.PlayRecords)
            .HasForeignKey(pr => pr.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        // PlayRecord - Order relationship (optional)
        modelBuilder.Entity<PlayRecord>()
            .HasOne(pr => pr.Order)
            .WithMany()
            .HasForeignKey(pr => pr.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // GameRental - Member relationship
        modelBuilder.Entity<GameRental>()
            .HasOne(gr => gr.Member)
            .WithMany(m => m.GameRentals)
            .HasForeignKey(gr => gr.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // GameRental - Product relationship
        modelBuilder.Entity<GameRental>()
            .HasOne(gr => gr.Product)
            .WithMany(p => p.GameRentals)
            .HasForeignKey(gr => gr.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // GameRental - Order relationship (optional)
        modelBuilder.Entity<GameRental>()
            .HasOne(gr => gr.Order)
            .WithMany()
            .HasForeignKey(gr => gr.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // SpaceReservation - Member relationship
        modelBuilder.Entity<SpaceReservation>()
            .HasOne(sr => sr.Member)
            .WithMany(m => m.SpaceReservations)
            .HasForeignKey(sr => sr.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        // SpaceReservation - Order relationship (optional)
        modelBuilder.Entity<SpaceReservation>()
            .HasOne(sr => sr.Order)
            .WithMany()
            .HasForeignKey(sr => sr.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // EventRegistration - Event relationship
        modelBuilder.Entity<EventRegistration>()
            .HasOne(er => er.Event)
            .WithMany(e => e.Registrations)
            .HasForeignKey(er => er.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // RestockRecord - Product relationship
        modelBuilder.Entity<RestockRecord>()
            .HasOne(rr => rr.Product)
            .WithMany(p => p.RestockRecords)
            .HasForeignKey(rr => rr.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed Data - Levels
        modelBuilder.Entity<Level>().HasData(
            new Level
            {
                Id = 1,
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
            new Level
            {
                Id = 2,
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

        // Seed Data - Default Admin (password: admin123)
        modelBuilder.Entity<Admin>().HasData(
            new Admin
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
                Name = "管理者",
                Role = "Owner",
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1)
            }
        );

        // Seed Data - Coupons
        modelBuilder.Entity<Coupon>().HasData(
            new Coupon
            {
                Id = 1,
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
            new Coupon
            {
                Id = 2,
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

        // Seed Data - Products
        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = 7,
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
            }
        );

    }
}
