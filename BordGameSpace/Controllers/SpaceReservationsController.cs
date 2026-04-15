using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class SpaceReservationsController : BaseController
{
    private readonly SpaceReservationService _service;
    private readonly AppDbContext _db;

    public SpaceReservationsController(SpaceReservationService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    // ==================== 會員端 ====================

    /// <summary>
    /// 會員 - 空間預約申請
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var member = await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == CurrentMemberId);
        ViewBag.Member = member;
        return View();
    }

    /// <summary>
    /// 會員 - 送出預約申請
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DateTime reservationDate, TimeSpan startTime, TimeSpan endTime, string? notes, int peopleCount = 2, string spaceType = "訂位")
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var member = await _db.Members.FindAsync(CurrentMemberId.Value);
        if (member == null)
            return RedirectToAction("Login", "Account");

        var (success, message, _) = await _service.CreateAsync(
            CurrentMemberId.Value, member.Name, member.Phone,
            reservationDate, startTime, endTime, notes, peopleCount, spaceType);

        if (!success)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("Create");
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 會員 - 我的預約列表
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var reservations = await _service.GetByMemberAsync(CurrentMemberId.Value);
        return View(reservations);
    }

    /// <summary>
    /// 會員 - 取消預約
    /// </summary>
    public async Task<IActionResult> Cancel(int id)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var (success, message) = await _service.CancelAsync(id, CurrentMemberId.Value);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("Index");
    }

    // ==================== 後台 ====================

    /// <summary>
    /// 後台 - 預約列表
    /// </summary>
    public async Task<IActionResult> AdminIndex(string? status)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var reservations = await _service.GetAllAsync(status);
        ViewBag.Status = status;
        return View(reservations);
    }

    /// <summary>
    /// 後台 - 新增預約（直接建立，跳過審核）
    /// </summary>
    public async Task<IActionResult> AdminCreate()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var members = await _db.Members
            .Include(m => m.Level)
            .Where(m => m.Level.Name != "非會員")
            .OrderBy(m => m.Name)
            .ToListAsync();

        ViewBag.Members = members;
        return View();
    }

    /// <summary>
    /// 後台 - 建立預約
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminCreate(int? memberId, DateTime reservationDate, TimeSpan startTime, TimeSpan endTime, string? notes, string? name, string? phone, int peopleCount = 2, string spaceType = "訂位")
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        string reservationName;
        string reservationPhone;

        if (memberId.HasValue && memberId.Value > 0)
        {
            // 會員模式
            var member = await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == memberId.Value);
            if (member == null)
            {
                TempData["ErrorMessage"] = "會員不存在";
                return RedirectToAction("AdminCreate");
            }
            reservationName = member.Name;
            reservationPhone = member.Phone;
        }
        else
        {
            // 非會員模式：檢查姓名電話
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            {
                TempData["ErrorMessage"] = "非會員請填寫姓名和電話";
                return RedirectToAction("AdminCreate");
            }
            reservationName = name.Trim();
            reservationPhone = phone.Trim();
            memberId = null; // 明確設為 null
        }

        if (startTime >= endTime)
        {
            TempData["ErrorMessage"] = "開始時間必須早於結束時間";
            return RedirectToAction("AdminCreate");
        }

        // 檢查時段衝突
        if (await _service.HasTimeConflictAsync(reservationDate, startTime, endTime))
        {
            TempData["ErrorMessage"] = "此時段已有其他預約，請選擇其他時段";
            return RedirectToAction("AdminCreate");
        }

        var existingMember = memberId.HasValue ? await _db.Members.FindAsync(memberId.Value) : null;

        var reservation = new SpaceReservation
        {
            MemberId = memberId,
            Name = reservationName,
            Phone = reservationPhone,
            ReservationDate = reservationDate,
            StartTime = startTime,
            EndTime = endTime,
            Hours = 0,
            HourlyRate = 0,
            TotalAmount = 0,
            PeopleCount = peopleCount,
            SpaceType = spaceType,
            Status = "Approved", // 後台直接建立為已通過
            Notes = notes,
            CreatedAt = DateTime.Now,
            Member = existingMember ?? null!
        };

        _db.SpaceReservations.Add(reservation);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"預約建立成功：{reservationName} - {reservationDate:yyyy-MM-dd} {startTime}~{endTime}";
        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 審核預約
    /// </summary>
    public async Task<IActionResult> Review(int id, bool approved)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _service.ReviewAsync(id, approved);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 編輯預約頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var reservation = await _service.GetByIdAsync(id);
        if (reservation == null)
        {
            TempData["ErrorMessage"] = "預約不存在";
            return RedirectToAction("AdminIndex");
        }

        var members = await _db.Members
            .Include(m => m.Level)
            .Where(m => m.Level.Name != "非會員")
            .OrderBy(m => m.Name)
            .ToListAsync();

        ViewBag.Members = members;
        return View(reservation);
    }

    /// <summary>
    /// 後台 - 處理編輯預約
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DateTime reservationDate, TimeSpan startTime, TimeSpan endTime, string name, string phone, string status, string? notes)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            TempData["ErrorMessage"] = "預約不存在";
            return RedirectToAction("AdminIndex");
        }

        if (startTime >= endTime)
        {
            TempData["ErrorMessage"] = "開始時間必須早於結束時間";
            return RedirectToAction("Edit", new { id });
        }

        // 檢查時段衝突（排除自己）
        if (await _service.HasTimeConflictAsync(reservationDate, startTime, endTime, id))
        {
            TempData["ErrorMessage"] = "此時段已有其他預約，請選擇其他時段";
            return RedirectToAction("Edit", new { id });
        }

        existing.ReservationDate = reservationDate;
        existing.StartTime = startTime;
        existing.EndTime = endTime;
        existing.Name = name;
        existing.Phone = phone;
        existing.Status = status;
        existing.Notes = notes;

        var (success, message) = await _service.UpdateAsync(existing);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 刪除預約（確認）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _service.DeleteAsync(id);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 建立訂單（審核通過後）
    /// </summary>
    public async Task<IActionResult> CreateOrder(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var reservation = await _db.SpaceReservations.FindAsync(id);
        if (reservation == null)
        {
            TempData["ErrorMessage"] = "預約不存在";
            return RedirectToAction("AdminIndex");
        }

        // 防重複：檢查是否已有訂單
        if (reservation.OrderId.HasValue)
        {
            TempData["ErrorMessage"] = "此預約已有訂單，請勿重複建立";
            return RedirectToAction("AdminIndex");
        }

        // 檢查會員是否存在，若不存在則 MemberId 設為 null，並清除導航屬性
        int? memberId = null;
        if (reservation.MemberId > 0)
        {
            var memberExists = await _db.Members.AnyAsync(m => m.Id == reservation.MemberId);
            if (memberExists)
                memberId = reservation.MemberId;
            else
            {
                reservation.MemberId = null;
                reservation.Member = null!; // 清除導航屬性
            }
        }

        var order = new Order
        {
            OrderType = "Space",
            MemberId = memberId,
            MemberName = reservation.Name,
            MemberPhone = reservation.Phone,
            TotalAmount = 0,
            DiscountAmount = 0,
            FinalAmount = 0,
            PaymentStatus = "Paid",
            PaymentMethod = "Cash",
            Notes = $"空間租借：{reservation.ReservationDate:yyyy-MM-dd} {reservation.StartTime}-{reservation.EndTime}",
            CreatedAt = DateTime.Now
        };

        var orderItem = new OrderItem
        {
            Order = order,
            ItemType = "Space",
            ItemId = reservation.Id,
            ItemName = $"空間租借 ({reservation.ReservationDate:yyyy-MM-dd} {reservation.StartTime}-{reservation.EndTime})",
            UnitPrice = 0,
            Quantity = 0,
            Subtotal = 0
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            reservation.OrderId = order.Id;
            reservation.Status = "Approved";
            _db.OrderItems.Add(orderItem);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"訂單已建立";
        }
        catch
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = "建立訂單失敗，請稍後再試";
        }
        return RedirectToAction("AdminIndex");
    }

}
