using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;

namespace BordGameSpace.Controllers;

public class PosController : BaseController
{
    private readonly PosService _posService;
    private readonly AppDbContext _db;
    private readonly ILogger<PosController> _logger;

    public PosController(PosService posService, AppDbContext db, ILogger<PosController> logger)
    {
        _posService = posService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// POS 首頁
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        // 載入商品列表（只取上架的）
        var products = await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewBag.Products = products;
        ViewBag.Categories = products.Select(p => p.Category).Distinct().ToList();

        return View();
    }

    /// <summary>
    /// 電話搜尋會員
    /// </summary>
    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> SearchMember(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Json(new { success = false, message = "請輸入電話號碼" });

        var member = await _posService.SearchMemberByPhoneAsync(phone);

        if (member == null)
            return Json(new { success = true, isMember = false, message = "非會員" });

        var coupons = await _posService.GetAvailableCouponsAsync(member.Id);

        return Json(new
        {
            success = true,
            isMember = true,
            member = new
            {
                member.Id,
                member.Name,
                member.Phone,
                member.TotalSpending,
                member.TotalPlayHours,
                LevelName = member.Level?.Name,
                LevelId = member.LevelId,
                GameDiscount = member.Level?.GameDiscount,
                WeekdayRate = member.Level?.WeekdayHourlyRate,
                HolidayRate = member.Level?.HolidayHourlyRate
            },
            coupons = coupons.Select(c => new
            {
                c.Id,
                c.Name,
                c.CouponType,
                c.DiscountValue,
                c.MinPurchase,
                c.ApplicableTo
            })
        });
    }

    /// <summary>
    /// 計算折扣（per-item discount）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CalculateDiscount([FromBody] CalculateDiscountRequest request)
    {
        Member? member = null;

        if (request.MemberId.HasValue)
        {
            member = await _db.Members
                .Include(m => m.Level)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value);
        }

        var cartItems = request.CartItems.Select(c => new CartItem
        {
            ItemType = c.ItemType,
            ItemId = c.ItemId,
            ItemName = c.ItemName,
            UnitPrice = c.UnitPrice,
            Quantity = c.Quantity,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue
        }).ToList();

        var result = _posService.CalculateDiscount(member, cartItems);

        return Json(new
        {
            success = true,
            subtotal = result.Subtotal,
            itemDiscount = result.ItemDiscount,
            levelDiscount = result.LevelDiscount,
            finalAmount = result.FinalAmount
        });
    }

    /// <summary>
    /// 取得可用優惠券列表（for dropdown）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableCoupons(int? memberId)
    {
        var now = DateTime.Now;

        // 若有 memberId：回傳該會員的可用優惠券（從 MemberCoupon）
        if (memberId.HasValue && memberId.Value > 0)
        {
            var coupons = await _db.MemberCoupons
                .Where(mc => mc.MemberId == memberId.Value && mc.Coupon != null && mc.Coupon.IsActive
                    && mc.Coupon.ValidFrom <= now
                    && (mc.Coupon.ValidUntil == null || mc.Coupon.ValidUntil >= now)
                    && mc.UsedAt == null)
                .Select(mc => mc.Coupon!)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.CouponType,
                    c.DiscountValue
                })
                .ToListAsync();
            return Json(new { success = true, coupons });
        }

        // 無 memberId：回傳所有可用優惠券（一般 POS 模式）
        var allCoupons = await _db.Coupons
            .Where(c => c.IsActive
                && c.ValidFrom <= now
                && (c.ValidUntil == null || c.ValidUntil >= now)
                && (c.TotalQuantity == null || c.TotalQuantity > c.UsedCount))
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.CouponType,
                c.DiscountValue
            })
            .ToListAsync();

        return Json(new { success = true, coupons = allCoupons });
    }

    /// <summary>
    /// 建立訂單（per-item discount）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        Member? member = null;
        if (request.MemberId.HasValue)
        {
            member = await _db.Members
                .Include(m => m.Level)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value);
        }

        var cartItems = request.CartItems.Select(c => new CartItem
        {
            ItemType = c.ItemType,
            ItemId = c.ItemId,
            ItemName = c.ItemName,
            UnitPrice = c.UnitPrice,
            Quantity = c.Quantity,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue,
            CouponId = c.CouponId
        }).ToList();

        var (success, message, order) = await _posService.CreatePosOrderAsync(
            member,
            cartItems,
            request.Notes,
            request.PaymentMethod ?? "Cash");

        if (!success)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = "訂單建立成功",
            orderId = order!.Id,
            finalAmount = order.FinalAmount
        });
    }

    /// <summary>
    /// 遊玩結帳（從 PlayRecords 跳轉）
    /// </summary>
    public async Task<IActionResult> CheckoutPlay(int playId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var play = await _db.PlayRecords
            .Include(p => p.Member)
                .ThenInclude(m => m!.Level)
            .FirstOrDefaultAsync(p => p.Id == playId);

        if (play == null)
            return RedirectToAction("Index", "PlayRecords");

        // 載入商品列表
        var products = await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewBag.Products = products;
        ViewBag.Categories = products.Select(p => p.Category).Distinct().ToList();
        ViewBag.PlayRecord = play;
        ViewBag.IsPlayCheckout = true;

        // 計算遊玩費用（含每日上限）
        var hours = play.TotalHours ?? 0;
        var dailyCap = play.Member != null
            ? (IsHoliday(play.StartTime) ? 220m : 180m)
            : (IsHoliday(play.StartTime) ? 280m : 240m);
        play.Amount = Math.Min(hours * play.HourlyRate, dailyCap);

        return View("Index");
    }

    /// <summary>
    /// 判斷是否為假日
    /// </summary>
    private bool IsHoliday(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// 遊玩結帳確認
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmCheckoutPlay([FromBody] ConfirmCheckoutRequest request)
    {
        var play = await _db.PlayRecords
            .Include(p => p.Member)
            .FirstOrDefaultAsync(p => p.Id == request.PlayId);
        if (play == null)
            return Json(new { success = false, message = "找不到遊玩紀錄" });

        // 直接使用前端購物車的項目（含數量、折扣）
        var cartItems = request.CartItems.Select(c => new CartItem
        {
            ItemType = c.ItemType,
            ItemId = c.ItemId,
            ItemName = c.ItemName,
            UnitPrice = c.UnitPrice,
            Quantity = c.Quantity,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue,
            CouponId = c.CouponId
        }).ToList();

        var (success, message, order) = await _posService.CreatePosOrderAsync(
            play.Member,
            cartItems,
            $"遊玩結帳 #{play.Id}",
            request.PaymentMethod ?? "Cash");

        if (!success)
            return Json(new { success = false, message });

        // 更新遊玩紀錄狀態
        play.Status = "CheckedOut";
        play.OrderId = order!.Id;

        // 更新會員累積時數與消費
        if (play.MemberId.HasValue)
        {
            var member = await _db.Members.FindAsync(play.MemberId.Value);
            if (member != null)
            {
                member.TotalPlayHours += play.TotalHours ?? 0;
                member.TotalSpending += play.Amount;
                await _posService.CheckAndUpgradeLevelAsync(member);
            }
        }

        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "結帳成功",
            orderId = order.Id,
            finalAmount = order.FinalAmount
        });
    }
}

public class CalculateDiscountRequest
{
    public int? MemberId { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
}

public class CartItemDto
{
    public string ItemType { get; set; } = "Product";
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    // 折扣欄位
    public string DiscountType { get; set; } = "None";
    public decimal DiscountValue { get; set; } = 0;
    // 優惠券Id（用於扣減會員優惠券）
    public int? CouponId { get; set; }
}

public class CreateOrderRequest
{
    public int? MemberId { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
    public string? Notes { get; set; }
    public string? PaymentMethod { get; set; }
}

public class ConfirmCheckoutRequest
{
    public int PlayId { get; set; }
    public string? PaymentMethod { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
}
