using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class OrdersController : BaseController
{
    private readonly AdminService _adminService;
    private readonly AppDbContext _db;

    public OrdersController(AdminService adminService, AppDbContext db)
    {
        _adminService = adminService;
        _db = db;
    }

    /// <summary>
    /// 訂單列表
    /// </summary>
    public async Task<IActionResult> Index(
        string? startDate = null,
        string? endDate = null,
        string? search = null,
        string? orderType = null,
        string? paymentStatus = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        DateTime? start = null, end = null;
        if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var s))
            start = s;
        if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var e))
            end = e;

        var orders = await _adminService.GetAllOrdersAsync(start, end, search, orderType, paymentStatus);

        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.Search = search;
        ViewBag.OrderType = orderType;
        ViewBag.PaymentStatus = paymentStatus;

        return View(orders);
    }

    /// <summary>
    /// 訂單詳情
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var order = await _adminService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound();

        return View(order);
    }

    /// <summary>
    /// 刪除訂單（僅狀態為 Paid 且未關聯關鍵資料者可刪）
    /// </summary>
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var order = await _adminService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound();

        // 檢查可否刪除（已結帳訂單不允許刪除，避免資料不一致）
        if (order.PaymentStatus == "Paid" && order.OrderItems.Any())
        {
            TempData["ErrorMessage"] = "已結帳訂單無法刪除，如有問題請聯絡系統管理員。";
            return RedirectToAction("Index");
        }

        // 移除訂單明細
        var items = _db.OrderItems.Where(oi => oi.OrderId == id);
        _db.OrderItems.RemoveRange(items);

        // 移除優惠券使用紀錄並回減使用數量
        if (order.CouponId.HasValue)
        {
            var mc = _db.MemberCoupons.FirstOrDefault(m => m.OrderId == id);
            if (mc != null)
            {
                var coupon = _db.Coupons.Find(order.CouponId.Value);
                if (coupon != null && coupon.TotalQuantity != null)
                {
                    coupon.UsedCount = Math.Max(0, coupon.UsedCount - 1);
                }
                _db.MemberCoupons.Remove(mc);
            }
        }

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"訂單 #{id} 已刪除";
        return RedirectToAction("Index");
    }
}
