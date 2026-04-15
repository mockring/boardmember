using BordGameSpace.Data;
using BordGameSpace.Models;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Services;

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 取得指定月份的總營收（只看 Paid 訂單）
    /// </summary>
    public async Task<decimal> GetMonthlyRevenueAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0);
        var end = start.AddMonths(1);

        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end && o.PaymentStatus == "Paid")
            .ToListAsync();

        return orders.Sum(o => o.FinalAmount);
    }

    /// <summary>
    /// 暢銷商品 TOP10（依銷售數量）
    /// </summary>
    public async Task<List<ProductSalesRank>> GetTopProductsAsync(int year, int month, int top = 10)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0);
        var end = start.AddMonths(1);

        var items = await _db.OrderItems
            .Where(oi => oi.Order.PaymentStatus == "Paid"
                         && oi.Order.CreatedAt >= start
                         && oi.Order.CreatedAt < end
                         && oi.ItemType == "Product")
            .Select(oi => new { oi.ItemId, oi.ItemName, oi.Quantity, oi.Subtotal })
            .ToListAsync();

        return items
            .GroupBy(oi => new { oi.ItemId, oi.ItemName })
            .Select(g => new ProductSalesRank
            {
                ProductId = g.Key.ItemId,
                ProductName = g.Key.ItemName,
                TotalQuantity = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.Subtotal)
            })
            .OrderByDescending(p => p.TotalQuantity)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// 會員消費排行 TOP10（依消費金額）
    /// </summary>
    public async Task<List<MemberSpendRank>> GetTopMembersAsync(int year, int month, int top = 10)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0);
        var end = start.AddMonths(1);

        var orders = await _db.Orders
            .Include(o => o.Member)
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end
                       && o.PaymentStatus == "Paid" && o.MemberId != null)
            .Select(o => new { o.MemberId, o.Member.Name, o.FinalAmount })
            .ToListAsync();

        return orders
            .GroupBy(o => new { o.MemberId, o.Name })
            .Select(g => new MemberSpendRank
            {
                MemberId = g.Key.MemberId!.Value,
                MemberName = g.Key.Name ?? "未知",
                TotalSpending = g.Sum(o => o.FinalAmount),
                OrderCount = g.Count()
            })
            .OrderByDescending(m => m.TotalSpending)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// 等級分布統計
    /// </summary>
    public async Task<List<LevelDistribution>> GetLevelDistributionAsync()
    {
        var result = await _db.Members
            .Include(m => m.Level)
            .Where(m => m.Status == true)
            .GroupBy(m => m.Level.Name)
            .Select(g => new LevelDistribution
            {
                LevelName = g.Key,
                MemberCount = g.Count()
            })
            .OrderByDescending(l => l.MemberCount)
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 當月總營收（今日用）
    /// </summary>
    public async Task<decimal> GetCurrentMonthRevenueAsync()
    {
        var now = DateTime.Now;
        return await GetMonthlyRevenueAsync(now.Year, now.Month);
    }
}
