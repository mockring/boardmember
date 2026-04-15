using BordGameSpace.Data;
using BordGameSpace.Models;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Services;

public class GameRentalService
{
    private readonly AppDbContext _db;

    public GameRentalService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 計算租金：遊戲定價 1000 以內 → 100；2000 以內 → 200；3000 以內 → 300；超額 → 0
    /// </summary>
    public decimal CalculateRentalFee(decimal price)
    {
        if (price <= 1000) return 100m;
        if (price <= 2000) return 200m;
        if (price <= 3000) return 300m;
        return 0m; // 超過3000不提供租借
    }

    /// <summary>
    /// 計算押金：會員免押金（0），非會員 = 定價
    /// </summary>
    public decimal CalculateDeposit(Member? member, decimal price)
    {
        if (member == null) return price; // 非會員
        // 如果是會員（LevelId > 1），免押金
        var level = _db.Levels.Find(member.LevelId);
        if (level != null && level.Name != "非會員")
            return 0m;
        return price;
    }

    /// <summary>
    /// 會員申請租借（建立預約，狀態 Pending）
    /// </summary>
    public async Task<(bool Success, string Message, GameRental? Rental)> CreateApplicationAsync(
        int memberId, int productId, int quantity, DateTime? pickupDate)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null)
            return (false, "遊戲商品不存在", null);
        if (!product.IsActive)
            return (false, "此遊戲已下架", null);
        if (product.Category != "桌遊")
            return (false, "僅支援桌遊類商品租借", null);
        if (product.Stock == null || product.Stock < quantity)
            return (false, $"庫存不足，目前庫存：{product.Stock ?? 0}", null);

        var member = await _db.Members.FindAsync(memberId);
        decimal deposit = CalculateDeposit(member, product.Price);
        decimal rentalFee = CalculateRentalFee(product.Price) * quantity;

        var rental = new GameRental
        {
            MemberId = memberId,
            ProductId = productId,
            BorrowDate = DateTime.Now,
            PickupDate = pickupDate,
            DueDate = DateTime.Now.AddDays(7),
            Deposit = deposit,
            RentalFee = rentalFee,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };

        _db.GameRentals.Add(rental);
        await _db.SaveChangesAsync();
        return (true, "租借申請已送出，等待管理員審核", rental);
    }

    /// <summary>
    /// 取得會員的租借列表
    /// </summary>
    public async Task<List<GameRental>> GetByMemberAsync(int memberId)
    {
        return await _db.GameRentals
            .Include(r => r.Product)
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 取得所有租借（後台）
    /// </summary>
    public async Task<List<GameRental>> GetAllAsync(string? status = null)
    {
        var query = _db.GameRentals.Include(r => r.Member).Include(r => r.Product).AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// 審核通過（借出）
    /// </summary>
    public async Task<(bool Success, string Message)> ApproveAsync(int id)
    {
        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null)
            return (false, "租借記錄不存在");
        if (rental.Status != "Pending")
            return (false, "目前狀態無法借出");

        rental.Status = "Approved";
        await _db.SaveChangesAsync();
        return (true, "已審核通過，等待取貨");
    }

    /// <summary>
    /// 拒絕申請
    /// </summary>
    public async Task<(bool Success, string Message)> RejectAsync(int id)
    {
        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null)
            return (false, "租借記錄不存在");

        rental.Status = "Rejected";
        await _db.SaveChangesAsync();
        return (true, "已拒絕申請");
    }

    /// <summary>
    /// 後台 - 確認借出（Approved → Borrowed）
    /// </summary>
    public async Task<(bool Success, string Message)> BorrowAsync(int id)
    {
        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null)
            return (false, "租借記錄不存在");
        if (rental.Status != "Approved")
            return (false, "目前狀態無法借出");

        rental.Status = "Borrowed";
        await _db.SaveChangesAsync();
        return (true, "已確認借出");
    }

    /// <summary>
    /// 續借（延長7天）
    /// </summary>
    public async Task<(bool Success, string Message)> RenewAsync(int id)
    {
        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null)
            return (false, "租借記錄不存在");
        if (rental.Status != "Borrowed" && rental.Status != "Renewed")
            return (false, "目前狀態無法續借");

        rental.DueDate = rental.DueDate.AddDays(7);
        rental.Status = "Renewed";
        await _db.SaveChangesAsync();
        return (true, $"已續借至 {rental.DueDate:yyyy-MM-dd}");
    }

    /// <summary>
    /// 歸還
    /// </summary>
    public async Task<(bool Success, string Message)> ReturnAsync(int id)
    {
        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null)
            return (false, "租借記錄不存在");
        if (rental.Status == "Returned")
            return (false, "已完成歸還");

        rental.ReturnDate = DateTime.Now;
        rental.Status = "Returned";
        await _db.SaveChangesAsync();
        return (true, "已完成歸還");
    }

    /// <summary>
    /// 更新逾期狀態（排程或定時更新）
    /// </summary>
    public async Task UpdateOverdueAsync(DateTime? referenceTime = null)
    {
        var now = referenceTime ?? DateTime.Now;
        var overdueRentals = await _db.GameRentals
            .Where(r => (r.Status == "Borrowed" || r.Status == "Renewed") && r.DueDate < now)
            .ToListAsync();

        foreach (var rental in overdueRentals)
        {
            rental.Status = "Overdue";
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// 取得可租借的遊戲列表
    /// </summary>
    public async Task<List<Product>> GetAvailableGamesAsync()
    {
        return await _db.Products
            .Where(p => p.Category == "桌遊" && p.IsActive == true)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
