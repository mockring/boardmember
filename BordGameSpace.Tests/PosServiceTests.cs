using Xunit;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BordGameSpace.Tests;

public class PosServiceTests
{
    private PosService CreateService(AppDbContext db)
    {
        var logger = new Mock<ILogger<PosService>>();
        return new PosService(db, logger.Object);
    }

    [Fact]
    public async Task SearchMemberByPhoneAsync_WithValidPhone_ReturnsMember()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        db.Members.Add(new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            Status = true,
            LevelId = 2
        });
        await db.SaveChangesAsync();

        // Act
        var member = await service.SearchMemberByPhoneAsync("0912345678");

        // Assert
        Assert.NotNull(member);
        Assert.Equal("王小明", member!.Name);
    }

    [Fact]
    public async Task SearchMemberByPhoneAsync_WithInactiveMember_ReturnsNull()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        db.Members.Add(new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            Status = false,
            LevelId = 2
        });
        await db.SaveChangesAsync();

        // Act
        var member = await service.SearchMemberByPhoneAsync("0912345678");

        // Assert
        Assert.Null(member);
    }

    [Fact]
    public void CalculateDiscount_WithNoMember_ReturnsOriginalPrice()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var cartItems = new List<CartItem>
        {
            new CartItem { ItemType = "Product", ItemId = 1, ItemName = "測試商品", UnitPrice = 100, Quantity = 1 }
        };

        // Act
        var result = service.CalculateDiscount(null, cartItems);

        // Assert
        Assert.Equal(100, result.Subtotal);
        Assert.Equal(0, result.ItemDiscount);
        Assert.Equal(0, result.LevelDiscount);
        Assert.Equal(100, result.FinalAmount);
    }

    [Fact]
    public void CalculateDiscount_WithPercentageDiscount_CalculatesCorrectly()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                ItemType = "Product",
                ItemId = 1,
                ItemName = "測試商品",
                UnitPrice = 100,
                Quantity = 1,
                DiscountType = "Percentage",
                DiscountValue = 10 // 10% off
            }
        };

        // Act
        var result = service.CalculateDiscount(null, cartItems);

        // Assert
        Assert.Equal(100, result.Subtotal);
        Assert.Equal(10, result.ItemDiscount);
        Assert.Equal(90, result.FinalAmount);
    }

    [Fact]
    public void CalculateDiscount_WithFixedAmountDiscount_CalculatesCorrectly()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                ItemType = "Product",
                ItemId = 1,
                ItemName = "測試商品",
                UnitPrice = 100,
                Quantity = 2,
                DiscountType = "FixedAmount",
                DiscountValue = 15 // $15 off per item
            }
        };

        // Act
        var result = service.CalculateDiscount(null, cartItems);

        // Assert
        Assert.Equal(200, result.Subtotal);
        Assert.Equal(30, result.ItemDiscount); // 15 * 2
        Assert.Equal(170, result.FinalAmount);
    }

    [Fact]
    public async Task CreatePosOrderAsync_WithValidData_CreatesOrder()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        db.Members.Add(new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            Status = true,
            LevelId = 2
        });

        db.Products.Add(new Product
        {
            Name = "測試商品",
            Category = "桌遊",
            Price = 500,
            Stock = 10,
            IsActive = true,
            IsService = false
        });
        await db.SaveChangesAsync();

        var member = await db.Members.FirstAsync();
        var product = await db.Products.FirstAsync();

        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                ItemType = "Product",
                ItemId = product.Id,
                ItemName = product.Name,
                UnitPrice = product.Price,
                Quantity = 1
            }
        };

        // Act
        var (success, message, order) = await service.CreatePosOrderAsync(
            member, cartItems, "測試備註", "Cash");

        // Assert
        Assert.True(success);
        Assert.NotNull(order);
        Assert.Equal(500, order.FinalAmount);
        Assert.Equal("Paid", order.PaymentStatus);

        // Verify stock was reduced
        var updatedProduct = await db.Products.FindAsync(product.Id);
        Assert.Equal(9, updatedProduct!.Stock);
    }

    [Fact]
    public async Task CreatePosOrderAsync_UpdatesMemberTotalSpending()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            Status = true,
            LevelId = 2,
            TotalSpending = 0
        };
        db.Members.Add(member);

        db.Products.Add(new Product
        {
            Name = "測試商品",
            Category = "桌遊",
            Price = 1000,
            Stock = 10,
            IsActive = true,
            IsService = false
        });
        await db.SaveChangesAsync();

        var product = await db.Products.FirstAsync();
        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                ItemType = "Product",
                ItemId = product.Id,
                ItemName = product.Name,
                UnitPrice = product.Price,
                Quantity = 1
            }
        };

        // Act
        await service.CreatePosOrderAsync(member, cartItems, null, "Cash");

        // Assert
        var updated = await db.Members.FindAsync(member.Id);
        Assert.Equal(1000, updated!.TotalSpending);
    }

    [Fact]
    public async Task CheckoutPlayAsync_CreatesOrderAndUpdatesMember()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            Status = true,
            LevelId = 2,
            TotalPlayHours = 10,
            TotalSpending = 0
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var playRecord = new PlayRecord
        {
            MemberId = member.Id,
            MemberName = member.Name,
            MemberPhone = member.Phone,
            StartTime = DateTime.Now.AddHours(-2),
            EndTime = DateTime.Now,
            TotalHours = 2,
            HourlyRate = 60,
            Amount = 120,
            Status = "Completed"
        };
        db.PlayRecords.Add(playRecord);
        await db.SaveChangesAsync();

        // Act
        var (success, message, order) = await service.CheckoutPlayAsync(playRecord, "Cash");

        // Assert
        Assert.True(success);
        Assert.NotNull(order);
        Assert.Equal(120, order.FinalAmount);

        var updated = await db.Members.FindAsync(member.Id);
        Assert.Equal(12, updated!.TotalPlayHours); // 10 + 2
        Assert.Equal(120, updated.TotalSpending);
    }
}
