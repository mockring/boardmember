using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.DTOs;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class MemberController : BaseController
{
    private readonly MemberService _memberService;
    private readonly ILogger<MemberController> _logger;

    public MemberController(MemberService memberService, ILogger<MemberController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    /// <summary>
    /// 會員專區首頁
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var member = await _memberService.GetMemberByIdAsync(CurrentMemberId.Value);
        if (member == null)
            return RedirectToAction("Logout", "Account");

        return View(member);
    }

    /// <summary>
    /// 我的資料
    /// </summary>
    public async Task<IActionResult> Profile()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var member = await _memberService.GetMemberByIdAsync(CurrentMemberId.Value);
        if (member == null)
            return RedirectToAction("Logout", "Account");

        return View(member);
    }

    /// <summary>
    /// 更新資料頁面
    /// </summary>
    public async Task<IActionResult> Edit()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var member = await _memberService.GetMemberByIdAsync(CurrentMemberId.Value);
        if (member == null)
            return RedirectToAction("Logout", "Account");

        return View(member);
    }

    /// <summary>
    /// 處理更新資料
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string name, string phone, string email, DateTime? birthday)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var (success, message) = await _memberService.UpdateProfileAsync(
            CurrentMemberId.Value, name, phone, email, birthday);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("Edit");
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Profile");
    }

    /// <summary>
    /// 修改密碼頁面
    /// </summary>
    public IActionResult ChangePassword()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        return View();
    }

    /// <summary>
    /// 處理修改密碼
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
        {
            TempData["ErrorMessage"] = "請填寫所有欄位";
            return View();
        }

        if (newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "新密碼與確認密碼不符";
            return View();
        }

        if (newPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "新密碼至少6個字元";
            return View();
        }

        var (success, message) = await _memberService.ChangePasswordAsync(
            CurrentMemberId.Value, oldPassword, newPassword);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
            return View();
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Profile");
    }

    /// <summary>
    /// 我的訂單
    /// </summary>
    public async Task<IActionResult> Orders()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var db = HttpContext.RequestServices.GetService<AppDbContext>();
        var orders = await db!.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.MemberId == CurrentMemberId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .ToListAsync();

        return View(orders);
    }

    /// <summary>
    /// 我的優惠券
    /// </summary>
    public async Task<IActionResult> Coupons()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var db = HttpContext.RequestServices.GetService<AppDbContext>();
        var memberCoupons = await db!.MemberCoupons
            .Include(mc => mc.Coupon)
            .Where(mc => mc.MemberId == CurrentMemberId.Value)
            .OrderByDescending(mc => mc.ReceivedAt)
            .ToListAsync();

        return View(memberCoupons);
    }

}
