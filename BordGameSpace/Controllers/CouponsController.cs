using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class CouponsController : BaseController
{
    private readonly CouponService _couponService;

    public CouponsController(CouponService couponService)
    {
        _couponService = couponService;
    }

    /// <summary>
    /// 優惠券列表
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var coupons = await _couponService.GetAllCouponsAsync();
        var stats = await _couponService.GetCouponStatsAsync();

        // 注入統計資料到 ViewBag
        ViewBag.Stats = stats;

        return View(coupons);
    }

    /// <summary>
    /// 新增優惠券頁面
    /// </summary>
    public IActionResult Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }

    /// <summary>
    /// 處理新增優惠券
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Coupon model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message, _) = await _couponService.CreateCouponAsync(model);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯優惠券頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var coupon = await _couponService.GetCouponByIdAsync(id);
        if (coupon == null)
            return NotFound();

        return View(coupon);
    }

    /// <summary>
    /// 處理編輯優惠券
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Coupon model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _couponService.UpdateCouponAsync(model);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 刪除優惠券
    /// </summary>
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _couponService.DeleteCouponAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 發放優惠券頁面
    /// </summary>
    public async Task<IActionResult> Assign(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var coupon = await _couponService.GetCouponByIdAsync(id);
        if (coupon == null)
            return NotFound();

        return View(coupon);
    }

    /// <summary>
    /// 發放優惠券給指定會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int couponId, int memberId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _couponService.AssignCouponToMemberAsync(couponId, memberId);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 大量發放給所有會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignToAll(int couponId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message, _) = await _couponService.AssignCouponToAllMembersAsync(couponId);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 依等級發放
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignByLevel(int couponId, int levelId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message, _) = await _couponService.AssignCouponByLevelAsync(couponId, levelId);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 查看優惠券使用情形
    /// </summary>
    public async Task<IActionResult> Usage(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var coupon = await _couponService.GetCouponByIdAsync(id);
        if (coupon == null)
            return NotFound();

        var usage = await _couponService.GetCouponUsageAsync(id);

        ViewBag.Coupon = coupon;
        return View(usage);
    }

    /// <summary>
    /// 搜尋會員（AJAX）
    /// </summary>
    public async Task<IActionResult> SearchMembers(string? phone, string? name)
    {
        if (!IsAdminLoggedIn)
            return Unauthorized();

        var db = HttpContext.RequestServices.GetService<AppDbContext>();
        var query = db!.Members.Include(m => m.Level).AsQueryable();

        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(m => m.Phone.Contains(phone));
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(m => m.Name.Contains(name));

        var members = await query.Take(10).ToListAsync();

        return Json(members.Select(m => new
        {
            m.Id,
            m.Name,
            m.Phone,
            LevelName = m.Level != null ? m.Level.Name : "未知"
        }));
    }
}
