using Xunit;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BordGameSpace.Tests;

public class CouponServiceTests
{
    private CouponService CreateService(AppDbContext db)
    {
        var logger = new Mock<ILogger<CouponService>>();
        return new CouponService(db, logger.Object);
    }

    [Fact]
    public async Task CreateCouponAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "新優惠券",
            CouponType = "Percentage",
            DiscountValue = 10,
            MinPurchase = 100,
            ApplicableTo = "All",
            ValidFrom = DateTime.Now,
            ValidUntil = DateTime.Now.AddMonths(1),
            TotalQuantity = 100
        };

        // Act
        var (success, message, created) = await service.CreateCouponAsync(coupon);

        // Assert
        Assert.True(success);
        Assert.NotNull(created);
        Assert.Equal("新優惠券", created.Name);
        Assert.Equal(10, created.DiscountValue);
    }

    [Fact]
    public async Task CreateCouponAsync_WithZeroTotalQuantity_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "測試",
            CouponType = "Percentage",
            DiscountValue = 10,
            TotalQuantity = 0, // 0 means "sold out"
            ValidFrom = DateTime.Now
        };

        // Act
        var (success, message, _) = await service.CreateCouponAsync(coupon);

        // Assert
        Assert.False(success);
        Assert.Contains("發行數量 0", message);
    }

    [Fact]
    public async Task CreateCouponAsync_WithInvalidDateRange_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "測試",
            CouponType = "Percentage",
            DiscountValue = 10,
            ValidFrom = DateTime.Now.AddMonths(1),
            ValidUntil = DateTime.Now // ValidUntil before ValidFrom
        };

        // Act
        var (success, message, _) = await service.CreateCouponAsync(coupon);

        // Assert
        Assert.False(success);
        Assert.Contains("開始日期需早於截止日期", message);
    }

    [Fact]
    public async Task AssignCouponToMemberAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "測試優惠券",
            CouponType = "Percentage",
            DiscountValue = 10,
            ValidFrom = DateTime.Now.AddDays(-1),
            ValidUntil = DateTime.Now.AddMonths(1),
            TotalQuantity = 100,
            IsActive = true
        };
        db.Coupons.Add(coupon);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash"
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.AssignCouponToMemberAsync(coupon.Id, member.Id);

        // Assert
        Assert.True(success);

        var memberCoupon = await db.MemberCoupons
            .FirstOrDefaultAsync(mc => mc.MemberId == member.Id && mc.CouponId == coupon.Id);
        Assert.NotNull(memberCoupon);
    }

    [Fact]
    public async Task AssignCouponToMemberAsync_WithExpiredCoupon_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "過期優惠券",
            CouponType = "Percentage",
            DiscountValue = 10,
            ValidFrom = DateTime.Now.AddMonths(-2),
            ValidUntil = DateTime.Now.AddMonths(-1), // Expired
            IsActive = true
        };
        db.Coupons.Add(coupon);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash"
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.AssignCouponToMemberAsync(coupon.Id, member.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("已過期", message);
    }

    [Fact]
    public async Task AssignCouponToMemberAsync_WhenAlreadyHas_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "測試優惠券",
            CouponType = "Percentage",
            DiscountValue = 10,
            ValidFrom = DateTime.Now.AddDays(-1),
            ValidUntil = DateTime.Now.AddMonths(1),
            TotalQuantity = 100,
            IsActive = true
        };
        db.Coupons.Add(coupon);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash"
        };
        db.Members.Add(member);

        // Already has the coupon
        db.MemberCoupons.Add(new MemberCoupon
        {
            MemberId = member.Id,
            CouponId = coupon.Id,
            ReceivedAt = DateTime.Now.AddDays(-1),
            UsedAt = null
        });
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.AssignCouponToMemberAsync(coupon.Id, member.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("已有此優惠券且尚未使用", message);
    }

    [Fact]
    public async Task AssignCouponToAllMembersAsync_AssignsToAllActiveMembers()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "全員優惠券",
            CouponType = "Percentage",
            DiscountValue = 5,
            ValidFrom = DateTime.Now.AddDays(-1),
            ValidUntil = DateTime.Now.AddMonths(1),
            TotalQuantity = 1000,
            IsActive = true
        };
        db.Coupons.Add(coupon);

        db.Members.Add(new Member { Name = "會員1", Phone = "111", Email = "1@test.com", PasswordHash = "hash", Status = true });
        db.Members.Add(new Member { Name = "會員2", Phone = "222", Email = "2@test.com", PasswordHash = "hash", Status = true });
        db.Members.Add(new Member { Name = "停用會員", Phone = "333", Email = "3@test.com", PasswordHash = "hash", Status = false });
        await db.SaveChangesAsync();

        // Act
        var (success, message, count) = await service.AssignCouponToAllMembersAsync(coupon.Id);

        // Assert
        Assert.True(success);
        Assert.Equal(2, count); // Only active members

        var memberCoupons = await db.MemberCoupons.Where(mc => mc.CouponId == coupon.Id).ToListAsync();
        Assert.Equal(2, memberCoupons.Count);
    }

    [Fact]
    public async Task GetCouponStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var coupon = new Coupon
        {
            Name = "統計測試",
            CouponType = "Percentage",
            DiscountValue = 10,
            TotalQuantity = 100,
            UsedCount = 30
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();

        // Add member coupons
        for (int i = 0; i < 50; i++)
        {
            db.MemberCoupons.Add(new MemberCoupon
            {
                MemberId = i + 1,
                CouponId = coupon.Id,
                ReceivedAt = DateTime.Now,
                UsedAt = i < 30 ? DateTime.Now : null // 30 used, 20 unused
            });
        }
        // Need actual members for FK
        for (int i = 0; i < 50; i++)
        {
            db.Members.Add(new Member
            {
                Name = $"會員{i}",
                Phone = $"09{i:D8}",
                Email = $"{i}@test.com",
                PasswordHash = "hash"
            });
        }
        await db.SaveChangesAsync();

        // Act
        var stats = await service.GetCouponStatsAsync();

        // Assert
        Assert.True(stats.ContainsKey(coupon.Id));
        var (held, used) = stats[coupon.Id];
        Assert.Equal(20, held); // 50 issued - 30 used
        Assert.Equal(30, used);
    }
}
