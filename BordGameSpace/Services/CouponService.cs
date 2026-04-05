using Microsoft.EntityFrameworkCore;
using BordGameSpace.Data;
using BordGameSpace.Models;

namespace BordGameSpace.Services;

public class CouponService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CouponService> _logger;

    public CouponService(AppDbContext db, ILogger<CouponService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region 優惠券管理

    /// <summary>
    /// 取得所有優惠券
    /// </summary>
    public async Task<List<Coupon>> GetAllCouponsAsync()
    {
        return await _db.Coupons
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 取得優惠券 ById
    /// </summary>
    public async Task<Coupon?> GetCouponByIdAsync(int id)
    {
        return await _db.Coupons.FindAsync(id);
    }

    /// <summary>
    /// 新增優惠券
    /// </summary>
    public async Task<(bool Success, string Message, Coupon? Coupon)> CreateCouponAsync(Coupon model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return (false, "優惠券名稱為必填", null);

        if (model.TotalQuantity <= 0)
            return (false, "發行數量需大於 0", null);

        if (model.DiscountValue <= 0)
            return (false, "折扣值需大於 0", null);

        if (model.ValidFrom >= model.ValidUntil)
            return (false, "開始日期需早於截止日期", null);

        model.CreatedAt = DateTime.Now;
        model.UsedCount = 0;
        model.IsActive = true;

        _db.Coupons.Add(model);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增優惠券: {Name}, 數量: {Qty}", model.Name, model.TotalQuantity);
        return (true, "新增成功", model);
    }

    /// <summary>
    /// 更新優惠券
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateCouponAsync(Coupon model)
    {
        var existing = await _db.Coupons.FindAsync(model.Id);
        if (existing == null)
            return (false, "找不到該優惠券");

        if (string.IsNullOrWhiteSpace(model.Name))
            return (false, "優惠券名稱為必填");

        if (model.ValidFrom >= model.ValidUntil)
            return (false, "開始日期需早於截止日期");

        existing.Name = model.Name;
        existing.CouponType = model.CouponType;
        existing.DiscountValue = model.DiscountValue;
        existing.MinPurchase = model.MinPurchase;
        existing.ApplicableTo = model.ApplicableTo;
        existing.ValidFrom = model.ValidFrom;
        existing.ValidUntil = model.ValidUntil;
        existing.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新優惠券: {Name}", model.Name);
        return (true, "更新成功");
    }

    /// <summary>
    /// 刪除優惠券（需確認未使用）
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteCouponAsync(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null)
            return (false, "找不到該優惠券");

        // 檢查是否已被會員持有
        var hasHolders = await _db.MemberCoupons.AnyAsync(mc => mc.CouponId == id);
        if (hasHolders)
            return (false, "已有會員持有此優惠券，無法刪除");

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();

        _logger.LogInformation("刪除優惠券: {Name}", coupon.Name);
        return (true, "刪除成功");
    }

    #endregion

    #region 發放優惠券

    /// <summary>
    /// 發放優惠券給指定會員
    /// </summary>
    public async Task<(bool Success, string Message)> AssignCouponToMemberAsync(int couponId, int memberId)
    {
        var coupon = await _db.Coupons.FindAsync(couponId);
        if (coupon == null)
            return (false, "找不到該優惠券");

        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到該會員");

        if (!coupon.IsActive)
            return (false, "此優惠券已停用");

        var now = DateTime.Now;
        if (now > coupon.ValidUntil || now < coupon.ValidFrom)
            return (false, "此優惠券已過期或尚未生效");

        // 檢查發行數量
        var issuedCount = await _db.MemberCoupons.CountAsync(mc => mc.CouponId == couponId);
        if (issuedCount >= coupon.TotalQuantity)
            return (false, "此優惠券已全數發放完畢");

        // 檢查會員是否已有此優惠券（未使用）
        var alreadyHas = await _db.MemberCoupons
            .AnyAsync(mc => mc.MemberId == memberId && mc.CouponId == couponId && mc.UsedAt == null);
        if (alreadyHas)
            return (false, "該會員已有此優惠券且尚未使用");

        var memberCoupon = new MemberCoupon
        {
            MemberId = memberId,
            CouponId = couponId,
            ReceivedAt = DateTime.Now
        };

        _db.MemberCoupons.Add(memberCoupon);
        await _db.SaveChangesAsync();

        _logger.LogInformation("發放優惠券: CouponId={CouponId}, MemberId={MemberId}", couponId, memberId);
        return (true, $"成功發放「{coupon.Name}」給 {member.Name}");
    }

    /// <summary>
    /// 大量發放優惠券給所有會員
    /// </summary>
    public async Task<(bool Success, string Message, int AssignedCount)> AssignCouponToAllMembersAsync(int couponId)
    {
        var coupon = await _db.Coupons.FindAsync(couponId);
        if (coupon == null)
            return (false, "找不到該優惠券", 0);

        if (!coupon.IsActive)
            return (false, "此優惠券已停用", 0);

        var now = DateTime.Now;
        if (now > coupon.ValidUntil || now < coupon.ValidFrom)
            return (false, "此優惠券已過期或尚未生效", 0);

        // 找出所有啟用的會員
        var activeMembers = await _db.Members
            .Where(m => m.Status)
            .ToListAsync();

        int count = 0;
        foreach (var member in activeMembers)
        {
            var alreadyHas = await _db.MemberCoupons
                .AnyAsync(mc => mc.MemberId == member.Id && mc.CouponId == couponId && mc.UsedAt == null);
            if (!alreadyHas)
            {
                var mc = new MemberCoupon
                {
                    MemberId = member.Id,
                    CouponId = couponId,
                    ReceivedAt = DateTime.Now
                };
                _db.MemberCoupons.Add(mc);
                count++;
            }
        }

        if (count > 0)
            await _db.SaveChangesAsync();

        _logger.LogInformation("大量發放優惠券: CouponId={CouponId}, 發放給 {Count} 位會員", couponId, count);
        return (true, $"成功發放給 {count} 位會員", count);
    }

    /// <summary>
    /// 發放優惠券給符合條件的會員（根據等級）
    /// </summary>
    public async Task<(bool Success, string Message, int AssignedCount)> AssignCouponByLevelAsync(int couponId, int levelId)
    {
        var coupon = await _db.Coupons.FindAsync(couponId);
        if (coupon == null)
            return (false, "找不到該優惠券", 0);

        var members = await _db.Members
            .Where(m => m.Status && m.LevelId == levelId)
            .ToListAsync();

        int count = 0;
        foreach (var member in members)
        {
            var alreadyHas = await _db.MemberCoupons
                .AnyAsync(mc => mc.MemberId == member.Id && mc.CouponId == couponId && mc.UsedAt == null);
            if (!alreadyHas)
            {
                var mc = new MemberCoupon
                {
                    MemberId = member.Id,
                    CouponId = couponId,
                    ReceivedAt = DateTime.Now
                };
                _db.MemberCoupons.Add(mc);
                count++;
            }
        }

        if (count > 0)
            await _db.SaveChangesAsync();

        return (true, $"成功發放給 {count} 位符合等級的會員", count);
    }

    #endregion

    #region 優惠券檢視

    /// <summary>
    /// 取得優惠券的使用情形
    /// </summary>
    public async Task<List<MemberCoupon>> GetCouponUsageAsync(int couponId)
    {
        return await _db.MemberCoupons
            .Include(mc => mc.Member)
            .Where(mc => mc.CouponId == couponId)
            .OrderByDescending(mc => mc.ReceivedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 統計每張優惠券的持有/使用人數
    /// </summary>
    public async Task<Dictionary<int, (int Held, int Used)>> GetCouponStatsAsync()
    {
        var stats = await _db.MemberCoupons
            .GroupBy(mc => mc.CouponId)
            .Select(g => new
            {
                CouponId = g.Key,
                Total = g.Count(),
                Used = g.Count(mc => mc.UsedAt != null)
            })
            .ToListAsync();

        return stats.ToDictionary(
            s => s.CouponId,
            s => (s.Total - s.Used, s.Used)
        );
    }

    #endregion
}
