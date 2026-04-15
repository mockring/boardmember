using Xunit;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BordGameSpace.Tests;

public class GameRentalServiceTests
{
    private GameRentalService CreateService(AppDbContext db)
    {
        return new GameRentalService(db);
    }

    [Theory]
    [InlineData(500, 100)]   // <= 1000 -> 100
    [InlineData(1000, 100)]  // <= 1000 -> 100
    [InlineData(1001, 200)]  // <= 2000 -> 200
    [InlineData(2000, 200)]  // <= 2000 -> 200
    [InlineData(2001, 300)]  // <= 3000 -> 300
    [InlineData(3000, 300)]  // <= 3000 -> 300
    [InlineData(3001, 0)]    // > 3000 -> 0
    public void CalculateRentalFee_ReturnsCorrectFee(decimal price, decimal expectedFee)
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Act
        var fee = service.CalculateRentalFee(price);

        // Assert
        Assert.Equal(expectedFee, fee);
    }

    [Fact]
    public void CalculateDeposit_WithNonMember_ReturnsFullPrice()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Act
        var deposit = service.CalculateDeposit(null, 1000);

        // Assert
        Assert.Equal(1000, deposit);
    }

    [Fact]
    public void CalculateDeposit_WithMember_ReturnsZero()
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
            LevelId = 2 // 會員 level
        };

        // Act
        var deposit = service.CalculateDeposit(member, 1000);

        // Assert
        Assert.Equal(0, deposit);
    }

    [Fact]
    public void CalculateDeposit_WithNonMemberLevel_ReturnsFullPrice()
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
            LevelId = 1 // 非會員 level
        };

        // Act
        var deposit = service.CalculateDeposit(member, 1000);

        // Assert
        Assert.Equal(1000, deposit);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithValidData_CreatesApplication()
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
            LevelId = 2
        };
        db.Members.Add(member);

        var product = new Product
        {
            Name = "測試桌遊",
            Category = "桌遊",
            Price = 1500,
            Stock = 5,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Act
        var (success, message, rental) = await service.CreateApplicationAsync(member.Id, product.Id, 1);

        // Assert
        Assert.True(success);
        Assert.NotNull(rental);
        Assert.Equal("Pending", rental.Status);
        Assert.Equal(200, rental.RentalFee); // 1500 -> 200
    }

    [Fact]
    public async Task CreateApplicationAsync_WithInactiveProduct_ReturnsFailure()
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
            LevelId = 2
        };
        db.Members.Add(member);

        var product = new Product
        {
            Name = "下架遊戲",
            Category = "桌遊",
            Price = 1500,
            Stock = 5,
            IsActive = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Act
        var (success, message, _) = await service.CreateApplicationAsync(member.Id, product.Id, 1);

        // Assert
        Assert.False(success);
        Assert.Contains("已下架", message);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithInsufficientStock_ReturnsFailure()
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
            LevelId = 2
        };
        db.Members.Add(member);

        var product = new Product
        {
            Name = "熱門遊戲",
            Category = "桌遊",
            Price = 1500,
            Stock = 1,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Act
        var (success, message, _) = await service.CreateApplicationAsync(member.Id, product.Id, 3);

        // Assert
        Assert.False(success);
        Assert.Contains("庫存不足", message);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithNonGameProduct_ReturnsFailure()
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
            LevelId = 2
        };
        db.Members.Add(member);

        var product = new Product
        {
            Name = "零食",
            Category = "零食",
            Price = 100,
            Stock = 10,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Act
        var (success, message, _) = await service.CreateApplicationAsync(member.Id, product.Id, 1);

        // Assert
        Assert.False(success);
        Assert.Contains("僅支援桌遊類商品租借", message);
    }

    [Fact]
    public async Task ApproveAsync_WithPendingRental_ChangesStatusToBorrowed()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var rental = new GameRental
        {
            MemberId = 1,
            ProductId = 1,
            BorrowDate = DateTime.Now,
            DueDate = DateTime.Now.AddDays(7),
            Deposit = 0,
            RentalFee = 100,
            Status = "Pending"
        };
        db.GameRentals.Add(rental);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.ApproveAsync(rental.Id);

        // Assert
        Assert.True(success);
        var updated = await db.GameRentals.FindAsync(rental.Id);
        Assert.Equal("Borrowed", updated!.Status);
    }

    [Fact]
    public async Task RejectAsync_WithPendingRental_ChangesStatusToRejected()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var rental = new GameRental
        {
            MemberId = 1,
            ProductId = 1,
            BorrowDate = DateTime.Now,
            DueDate = DateTime.Now.AddDays(7),
            Deposit = 0,
            RentalFee = 100,
            Status = "Pending"
        };
        db.GameRentals.Add(rental);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.RejectAsync(rental.Id);

        // Assert
        Assert.True(success);
        var updated = await db.GameRentals.FindAsync(rental.Id);
        Assert.Equal("Rejected", updated!.Status);
    }

    [Fact]
    public async Task RenewAsync_ExtendsDueDate()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var originalDueDate = DateTime.Now.AddDays(5);
        var rental = new GameRental
        {
            MemberId = 1,
            ProductId = 1,
            BorrowDate = DateTime.Now,
            DueDate = originalDueDate,
            Deposit = 0,
            RentalFee = 100,
            Status = "Borrowed"
        };
        db.GameRentals.Add(rental);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.RenewAsync(rental.Id);

        // Assert
        Assert.True(success);
        var updated = await db.GameRentals.FindAsync(rental.Id);
        Assert.Equal("Renewed", updated!.Status);
        Assert.Equal(originalDueDate.AddDays(7), updated.DueDate);
    }

    [Fact]
    public async Task ReturnAsync_ChangesStatusToReturned()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var rental = new GameRental
        {
            MemberId = 1,
            ProductId = 1,
            BorrowDate = DateTime.Now.AddDays(-3),
            DueDate = DateTime.Now,
            Deposit = 500,
            RentalFee = 100,
            Status = "Borrowed"
        };
        db.GameRentals.Add(rental);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.ReturnAsync(rental.Id);

        // Assert
        Assert.True(success);
        var updated = await db.GameRentals.FindAsync(rental.Id);
        Assert.Equal("Returned", updated!.Status);
        Assert.NotNull(updated.ReturnDate);
    }

    [Fact]
    public async Task UpdateOverdueAsync_MarksOverdueRentals()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Add overdue rental with DueDate clearly in the past (year 2000)
        var overdueRental = new GameRental
        {
            MemberId = 1,
            ProductId = 1,
            BorrowDate = new DateTime(1999, 12, 1),
            DueDate = new DateTime(2000, 1, 1), // Jan 1, 2000 - definitely in the past
            Deposit = 0,
            RentalFee = 100,
            Status = "Borrowed"
        };
        db.GameRentals.Add(overdueRental);
        await db.SaveChangesAsync();

        // Add current rental with DueDate clearly in the future (year 2099)
        var currentRental = new GameRental
        {
            MemberId = 1,
            ProductId = 1,
            BorrowDate = new DateTime(2099, 1, 1),
            DueDate = new DateTime(2099, 12, 31), // Dec 31, 2099 - definitely in the future
            Deposit = 0,
            RentalFee = 100,
            Status = "Borrowed"
        };
        db.GameRentals.Add(currentRental);
        await db.SaveChangesAsync();

        // Act - use the actual service method
        await service.UpdateOverdueAsync();

        // Assert - verify status was changed
        var overdue = await db.GameRentals.Where(r => r.Id == overdueRental.Id).FirstOrDefaultAsync();
        var current = await db.GameRentals.Where(r => r.Id == currentRental.Id).FirstOrDefaultAsync();

        Assert.NotNull(overdue);
        Assert.NotNull(current);
        Assert.Equal("Overdue", overdue.Status);
        Assert.Equal("Borrowed", current.Status);
    }
}
