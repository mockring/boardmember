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
    public async Task<IActionResult> SearchMember(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Json(new { success = false, message = "請輸入電話號碼" });

        var member = await _posService.SearchMemberByPhoneAsync(phone);

        if (member == null)
            return Json(new { success = true, isMember = false, message = "非會員" });

        var points = await _posService.GetMemberPointsAsync(member.Id);
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
                GameDiscount = member.Level?.GameDiscount,
                WeekdayRate = member.Level?.WeekdayHourlyRate,
                HolidayRate = member.Level?.HolidayHourlyRate
            },
            points,
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
    /// 計算折扣
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CalculateDiscount([FromBody] CalculateDiscountRequest request)
    {
        Member? member = null;
        List<Coupon> coupons = new();

        if (request.MemberId.HasValue)
        {
            member = await _db.Members
                .Include(m => m.Level)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value);
            coupons = await _posService.GetAvailableCouponsAsync(request.MemberId.Value);
        }

        var cartItems = request.CartItems.Select(c => new CartItem
        {
            ItemType = c.ItemType,
            ItemId = c.ItemId,
            ItemName = c.ItemName,
            UnitPrice = c.UnitPrice,
            Quantity = c.Quantity
        }).ToList();

        var result = _posService.CalculateDiscount(member, cartItems, request.CouponId, request.PointsToUse, coupons);

        return Json(new
        {
            success = true,
            subtotal = result.Subtotal,
            levelDiscount = result.LevelDiscount,
            couponDiscount = result.CouponDiscount,
            pointsDiscount = result.PointsDiscount,
            finalAmount = result.FinalAmount,
            pointsEarned = member != null ? (int)result.FinalAmount : 0
        });
    }

    /// <summary>
    /// 建立訂單
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
            Quantity = c.Quantity
        }).ToList();

        var (success, message, order) = await _posService.CreatePosOrderAsync(
            member,
            cartItems,
            request.CouponId,
            request.PointsToUse,
            request.Notes,
            request.PaymentMethod ?? "Cash");

        if (!success)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = "訂單建立成功",
            orderId = order!.Id,
            finalAmount = order.FinalAmount,
            pointsEarned = order.PointsEarned
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

        // 計算遊玩費用
        var hours = play.TotalHours ?? 0;
        var rate = play.HourlyRate;
        play.Amount = hours * rate;

        return View("Index");
    }

    /// <summary>
    /// 遊玩結帳確認
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmCheckoutPlay([FromBody] ConfirmCheckoutRequest request)
    {
        var play = await _db.PlayRecords.FindAsync(request.PlayId);
        if (play == null)
            return Json(new { success = false, message = "找不到遊玩紀錄" });

        var (success, message, order) = await _posService.CheckoutPlayAsync(play, request.PaymentMethod ?? "Cash");

        if (!success)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = "結帳成功",
            orderId = order!.Id,
            finalAmount = order.FinalAmount,
            pointsEarned = order.PointsEarned
        });
    }
}

public class CalculateDiscountRequest
{
    public int? MemberId { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
    public int? CouponId { get; set; }
    public int PointsToUse { get; set; }
}

public class CartItemDto
{
    public string ItemType { get; set; } = "Product";
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CreateOrderRequest
{
    public int? MemberId { get; set; }
    public List<CartItemDto> CartItems { get; set; } = new();
    public int? CouponId { get; set; }
    public int PointsToUse { get; set; }
    public string? Notes { get; set; }
    public string? PaymentMethod { get; set; }
}

public class ConfirmCheckoutRequest
{
    public int PlayId { get; set; }
    public string? PaymentMethod { get; set; }
}
