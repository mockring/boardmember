using Microsoft.EntityFrameworkCore;
using BordGameSpace.Data;
using BordGameSpace.Models;

namespace BordGameSpace.Services;

public class PosService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PosService> _logger;

    public PosService(AppDbContext db, ILogger<PosService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 根據電話號碼搜尋會員
    /// </summary>
    public async Task<Member?> SearchMemberByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        return await _db.Members
            .Include(m => m.Level)
            .Include(m => m.MemberCoupons)
                .ThenInclude(mc => mc.Coupon)
            .FirstOrDefaultAsync(m => m.Phone == phone && m.Status);
    }

    /// <summary>
    /// 取得會員的可用優惠券
    /// </summary>
    public async Task<List<Coupon>> GetAvailableCouponsAsync(int memberId)
    {
        var now = DateTime.Now;
        return await _db.MemberCoupons
            .Where(mc => mc.MemberId == memberId && mc.Coupon != null && mc.Coupon.IsActive
                && mc.Coupon.ValidFrom <= now && (mc.Coupon.ValidUntil == null || mc.Coupon.ValidUntil >= now) && mc.UsedAt == null)
            .Select(mc => mc.Coupon!)
            .ToListAsync();
    }

    /// <summary>
    /// 計算訂單折扣（per-item discount）
    /// </summary>
    public DiscountCalculationResult CalculateDiscount(
        Member? member,
        List<CartItem> cartItems)
    {
        var result = new DiscountCalculationResult();

        // 原始金額
        result.Subtotal = cartItems.Sum(c => c.UnitPrice * c.Quantity);

        // 每項商品的折扣
        decimal itemDiscount = 0;
        foreach (var item in cartItems)
        {
            if (item.DiscountType == "Percentage")
            {
                itemDiscount += (item.UnitPrice * item.DiscountValue / 100) * item.Quantity;
            }
            else if (item.DiscountType == "FixedAmount")
            {
                itemDiscount += item.DiscountValue * item.Quantity;
            }
        }
        result.ItemDiscount = itemDiscount;

        // 會員等級折扣（遊戲商品 95/90 折）- 僅對無折扣的商品
        decimal levelDiscount = 0;
        if (member != null && member.Level != null)
        {
            foreach (var item in cartItems.Where(c => c.ItemType == "Product" && c.DiscountType == "None"))
            {
                var product = _db.Products.Find(item.ItemId);
                if (product != null && product.Category == "桌遊")
                {
                    levelDiscount += (product.Price * (1 - member.Level.GameDiscount)) * item.Quantity;
                }
            }
        }
        result.LevelDiscount = levelDiscount;

        // 最終金額
        result.FinalAmount = Math.Max(0, result.Subtotal - result.ItemDiscount - levelDiscount);

        return result;
    }

    /// <summary>
    /// 建立 POS 訂單（per-item discount）
    /// </summary>
    public async Task<(bool Success, string Message, Order? Order)> CreatePosOrderAsync(
        Member? member,
        List<CartItem> cartItems,
        string? notes,
        string paymentMethod = "Cash")
    {
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 計算折扣（per-item）
            var discount = CalculateDiscount(member, cartItems);

            // 建立訂單
            var order = new Order
            {
                OrderType = "Product",
                MemberId = member?.Id,
                MemberName = member?.Name ?? "非會員",
                MemberPhone = member?.Phone,
                TotalAmount = discount.Subtotal,
                DiscountAmount = discount.ItemDiscount + discount.LevelDiscount,
                FinalAmount = discount.FinalAmount,
                PaymentStatus = "Paid",
                PaymentMethod = paymentMethod,
                Notes = notes,
                CreatedAt = DateTime.Now
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // 建立訂單明細（寫入每項商品的折扣）
            foreach (var item in cartItems)
            {
                // 計算該項目的小計（定價 × 數量 − 折扣）
                decimal itemSubtotal = item.UnitPrice * item.Quantity;
                if (item.DiscountType == "Percentage")
                {
                    itemSubtotal -= (item.UnitPrice * item.DiscountValue / 100) * item.Quantity;
                }
                else if (item.DiscountType == "FixedAmount")
                {
                    itemSubtotal -= item.DiscountValue * item.Quantity;
                }

                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ItemType = item.ItemType,
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    DiscountType = item.DiscountType,
                    DiscountValue = item.DiscountValue,
                    CouponId = item.CouponId,
                    Subtotal = Math.Max(0, itemSubtotal)
                };
                _db.OrderItems.Add(orderItem);

                // 扣減會員優惠券（不限張數的 coupon 不標記已使用，可重複使用）
                if (item.CouponId.HasValue && member != null)
                {
                    var memberCoupon = await _db.MemberCoupons
                        .FirstOrDefaultAsync(mc => mc.MemberId == member.Id && mc.CouponId == item.CouponId.Value && mc.UsedAt == null);
                    if (memberCoupon != null)
                    {
                        var coupon = await _db.Coupons.FindAsync(item.CouponId.Value);
                        if (coupon != null && coupon.TotalQuantity != null)
                        {
                            // 限張數 coupon：標記已使用
                            memberCoupon.UsedAt = DateTime.Now;
                            memberCoupon.OrderId = order.Id;
                            coupon.UsedCount++;
                        }
                        else if (coupon != null && coupon.TotalQuantity == null)
                        {
                            // 不限張數 coupon：僅累積使用次數，不標記已使用
                            coupon.UsedCount++;
                        }
                    }
                }

                // 庫存扣減（實體商品）
                if (item.ItemType == "Product")
                {
                    var product = await _db.Products.FindAsync(item.ItemId);
                    if (product != null && !product.IsService)
                    {
                        product.Stock = (product.Stock ?? 0) - item.Quantity;
                        if (product.Stock < 0) product.Stock = 0;
                    }
                }
            }

            // 更新會員累積消費金額（會員）
            if (member != null)
            {
                var dbMember = await _db.Members.FindAsync(member.Id);
                if (dbMember != null)
                {
                    dbMember.TotalSpending += discount.FinalAmount;

                    // 檢查是否需要升級
                    await CheckAndUpgradeLevelAsync(dbMember);
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("POS 訂單建立成功: OrderId={OrderId}, MemberId={MemberId}, FinalAmount={FinalAmount}",
                order.Id, member?.Id, discount.FinalAmount);

            return (true, "訂單建立成功", order);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "POS 訂單建立失敗");
            return (false, $"訂單建立失敗: {ex.Message}", null);
        }
    }

    /// <summary>
    /// 檢查並升級會員等級
    /// </summary>
    public async Task CheckAndUpgradeLevelAsync(Member member)
    {
        // 先取得會員目前等級的 SortOrder
        var currentLevel = await _db.Levels.FirstOrDefaultAsync(l => l.Id == member.LevelId);
        var currentSortOrder = currentLevel?.SortOrder ?? 0;

        var levels = await _db.Levels
            .Where(l => l.SortOrder > currentSortOrder)
            .OrderByDescending(l => l.SortOrder)
            .ToListAsync();

        foreach (var level in levels)
        {
            if (member.TotalPlayHours >= level.UpgradeThresholdHours &&
                member.TotalSpending >= level.UpgradeThresholdAmount)
            {
                member.LevelId = level.Id;
                _logger.LogInformation("會員 {MemberId} 升級為 {LevelName}", member.Id, level.Name);
                break;
            }
        }
    }

    /// <summary>
    /// 遊玩結束結帳
    /// </summary>
    public async Task<(bool Success, string Message, Order? Order)> CheckoutPlayAsync(PlayRecord playRecord, string paymentMethod = "Cash")
    {
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 建立遊玩訂單
            var order = new Order
            {
                OrderType = "Play",
                MemberId = playRecord.MemberId,
                MemberName = playRecord.MemberName ?? "非會員",
                MemberPhone = playRecord.MemberPhone,
                TotalAmount = playRecord.Amount,
                DiscountAmount = 0,
                FinalAmount = playRecord.Amount,
                PaymentStatus = "Paid",
                PaymentMethod = paymentMethod,
                Notes = $"遊玩時段: {playRecord.StartTime:yyyy-MM-dd HH:mm} ~ {playRecord.EndTime:yyyy-MM-dd HH:mm}",
                CreatedAt = DateTime.Now
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // 建立訂單明細
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ItemType = "Play",
                ItemId = playRecord.Id,
                ItemName = $"場地費 ({playRecord.TotalHours} 小時)",
                UnitPrice = playRecord.HourlyRate,
                Quantity = 1,
                Subtotal = playRecord.Amount
            };
            _db.OrderItems.Add(orderItem);

            // 更新遊玩紀錄
            playRecord.OrderId = order.Id;
            playRecord.Status = "CheckedOut";

            // 更新會員累積時數與消費
            if (playRecord.MemberId.HasValue)
            {
                var member = await _db.Members.FindAsync(playRecord.MemberId.Value);
                if (member != null)
                {
                    member.TotalPlayHours += playRecord.TotalHours ?? 0;
                    member.TotalSpending += playRecord.Amount;

                    // 檢查升級
                    await CheckAndUpgradeLevelAsync(member);
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("遊玩結帳成功: PlayId={PlayId}, OrderId={OrderId}, Amount={Amount}",
                playRecord.Id, order.Id, playRecord.Amount);

            return (true, "結帳成功", order);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "遊玩結帳失敗: PlayId={PlayId}", playRecord.Id);
            return (false, $"結帳失敗: {ex.Message}", null);
        }
    }
}

/// <summary>
/// 購物車項目
/// </summary>
public class CartItem
{
    public string ItemType { get; set; } = "Product";
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    // 折扣欄位
    public string DiscountType { get; set; } = "None"; // None/Percentage/FixedAmount
    public decimal DiscountValue { get; set; } = 0;
    // 優惠券Id（用於扣減會員優惠券）
    public int? CouponId { get; set; }
}

/// <summary>
/// 折扣計算結果
/// </summary>
public class DiscountCalculationResult
{
    public decimal Subtotal { get; set; }
    public decimal ItemDiscount { get; set; } // 每項商品折扣
    public decimal LevelDiscount { get; set; } // 會員等級折扣
    public decimal FinalAmount { get; set; }
}
