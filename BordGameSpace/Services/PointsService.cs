using Microsoft.EntityFrameworkCore;
using BordGameSpace.Data;
using BordGameSpace.Models;

namespace BordGameSpace.Services;

public class PointsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PointsService> _logger;

    public PointsService(AppDbContext db, ILogger<PointsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 取得積分設定
    /// </summary>
    public async Task<PointSetting?> GetSettingsAsync()
    {
        return await _db.PointSettings.FirstOrDefaultAsync();
    }

    /// <summary>
    /// 更新積分設定
    /// </summary>
    public async Task<bool> UpdateSettingsAsync(PointSetting settings)
    {
        var existing = await _db.PointSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            settings.Id = 1;
            settings.CreatedAt = DateTime.Now;
            settings.UpdatedAt = DateTime.Now;
            _db.PointSettings.Add(settings);
        }
        else
        {
            existing.EarnRate = settings.EarnRate;
            existing.RedeemRate = settings.RedeemRate;
            existing.MinRedeemPoints = settings.MinRedeemPoints;
            existing.ApplicableLevelId = settings.ApplicableLevelId;
            existing.IsEnabled = settings.IsEnabled;
            existing.Description = settings.Description;
            existing.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 取得會員積分餘額
    /// </summary>
    public async Task<int> GetBalanceAsync(int memberId)
    {
        var lastTx = await _db.PointTransactions
            .Where(pt => pt.MemberId == memberId)
            .OrderByDescending(pt => pt.CreatedAt)
            .FirstOrDefaultAsync();

        return lastTx?.Balance ?? 0;
    }

    /// <summary>
    /// 取得會員積分異動紀錄
    /// </summary>
    public async Task<List<PointTransaction>> GetTransactionsAsync(int memberId, int limit = 50)
    {
        return await _db.PointTransactions
            .Include(pt => pt.Order)
            .Where(pt => pt.MemberId == memberId)
            .OrderByDescending(pt => pt.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 根據消費金額計算可獲得積分（使用資料庫設定）
    /// </summary>
    public async Task<int> CalculateEarnPointsAsync(decimal amount)
    {
        var settings = await GetSettingsAsync();
        var rate = settings?.EarnRate ?? 1m;
        return (int)Math.Floor(amount * rate);
    }

    /// <summary>
    /// 根據消費金額計算可獲得積分（可自訂比率）
    /// </summary>
    public int CalculateEarnPoints(decimal amount, decimal earnRate)
    {
        return (int)Math.Floor(amount * earnRate);
    }

    /// <summary>
    /// 計算積分可折抵的金額（使用資料庫設定）
    /// </summary>
    public async Task<decimal> CalculateRedeemValueAsync(int points)
    {
        var settings = await GetSettingsAsync();
        var rate = settings?.RedeemRate ?? 1m;
        return points * rate;
    }

    /// <summary>
    /// 計算積分可折抵的金額（可自訂比率）
    /// </summary>
    public decimal CalculateRedeemValue(int points, decimal redeemRate)
    {
        return points * redeemRate;
    }

    /// <summary>
    /// 檢查積分折抵是否達到最低門檻
    /// </summary>
    public async Task<bool> CanRedeemAsync(int points)
    {
        var settings = await GetSettingsAsync();
        var minPoints = settings?.MinRedeemPoints ?? 100;
        return points >= minPoints;
    }

    /// <summary>
    /// 檢查會員是否符合積分資格
    /// </summary>
    public async Task<bool> IsEligibleForPointsAsync(int memberId)
    {
        var settings = await GetSettingsAsync();
        if (settings == null || !settings.IsEnabled)
            return false;

        if (settings.ApplicableLevelId == 0)
            return true;

        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return false;

        // 檢查會員等級是否為指定等級或更高
        var applicableLevel = await _db.Levels.FindAsync(settings.ApplicableLevelId);
        if (applicableLevel == null)
            return true;

        return member.Level != null && member.Level.SortOrder >= applicableLevel.SortOrder;
    }

    /// <summary>
    /// 新增積分（發放/獎勵）
    /// </summary>
    public async Task<(bool Success, string Message)> AddPointsAsync(int memberId, int points, string description, int? orderId = null)
    {
        if (points <= 0)
            return (false, "積分數量需大於 0");

        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到該會員");

        var balance = await GetBalanceAsync(memberId);
        var newBalance = balance + points;

        var tx = new PointTransaction
        {
            MemberId = memberId,
            OrderId = orderId,
            Type = "Adjust",
            Points = points,
            Balance = newBalance,
            Description = description,
            CreatedAt = DateTime.Now
        };

        _db.PointTransactions.Add(tx);
        await _db.SaveChangesAsync();

        _logger.LogInformation("發放積分: MemberId={MemberId}, Points={Points}, Balance={Balance}",
            memberId, points, newBalance);
        return (true, $"成功發放 {points} 積分");
    }

    /// <summary>
    /// 扣回積分（錯誤調整）
    /// </summary>
    public async Task<(bool Success, string Message)> DeductPointsAsync(int memberId, int points, string description)
    {
        if (points <= 0)
            return (false, "積分數量需大於 0");

        var balance = await GetBalanceAsync(memberId);
        if (balance < points)
            return (false, $"會員積分餘額不足（目前: {balance}）");

        var newBalance = balance - points;

        var tx = new PointTransaction
        {
            MemberId = memberId,
            Type = "Adjust",
            Points = -points,
            Balance = newBalance,
            Description = description,
            CreatedAt = DateTime.Now
        };

        _db.PointTransactions.Add(tx);
        await _db.SaveChangesAsync();

        _logger.LogInformation("扣回積分: MemberId={MemberId}, Points={Points}, Balance={Balance}",
            memberId, points, newBalance);
        return (true, $"成功扣回 {points} 積分");
    }

    /// <summary>
    /// 大量發放積分給所有會員
    /// </summary>
    public async Task<(bool Success, string Message, int Count)> AddPointsToAllMembersAsync(int points, string description)
    {
        var members = await _db.Members.Where(m => m.Status).ToListAsync();

        foreach (var member in members)
        {
            var balance = await GetBalanceAsync(member.Id);
            var tx = new PointTransaction
            {
                MemberId = member.Id,
                Type = "Adjust",
                Points = points,
                Balance = balance + points,
                Description = description,
                CreatedAt = DateTime.Now
            };
            _db.PointTransactions.Add(tx);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("大量發放積分: Points={Points}, Count={Count}", points, members.Count);
        return (true, $"成功發放給 {members.Count} 位會員", members.Count);
    }

    /// <summary>
    /// 大量發放積分給符合等級的會員
    /// </summary>
    public async Task<(bool Success, string Message, int Count)> AddPointsByLevelAsync(int points, int levelId, string description)
    {
        var members = await _db.Members.Where(m => m.Status && m.LevelId == levelId).ToListAsync();

        foreach (var member in members)
        {
            var balance = await GetBalanceAsync(member.Id);
            var tx = new PointTransaction
            {
                MemberId = member.Id,
                Type = "Adjust",
                Points = points,
                Balance = balance + points,
                Description = description,
                CreatedAt = DateTime.Now
            };
            _db.PointTransactions.Add(tx);
        }

        await _db.SaveChangesAsync();

        return (true, $"成功發放給 {members.Count} 位符合等級的會員", members.Count);
    }

    /// <summary>
    /// 取得積分排行榜（TOP N）
    /// </summary>
    public async Task<List<(Member Member, int Balance)>> GetLeaderboardAsync(int top = 10)
    {
        var memberIds = await _db.PointTransactions
            .GroupBy(pt => pt.MemberId)
            .Select(g => new
            {
                MemberId = g.Key,
                Balance = g.OrderByDescending(pt => pt.CreatedAt).First().Balance
            })
            .OrderByDescending(x => x.Balance)
            .Take(top)
            .ToListAsync();

        var result = new List<(Member, int)>();
        foreach (var item in memberIds)
        {
            var member = await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == item.MemberId);
            if (member != null)
                result.Add((member, item.Balance));
        }

        return result;
    }
}
