using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class GameRentalsController : BaseController
{
    private readonly GameRentalService _service;
    private readonly AppDbContext _db;

    public GameRentalsController(GameRentalService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    #region 會員端

    /// <summary>
    /// 會員 - 申請租借
    /// </summary>
    public async Task<IActionResult> MemberCreate()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var games = await _service.GetAvailableGamesAsync();
        return View(games);
    }

    /// <summary>
    /// 會員 - 送出租借申請
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MemberCreate(int productId, int quantity = 1, DateTime? pickupDate = null, string pickupTime = null)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        DateTime? pickupDateTime = null;
        if (pickupDate.HasValue)
        {
            if (!string.IsNullOrEmpty(pickupTime))
                pickupDateTime = pickupDate.Value.Date.Add(TimeSpan.Parse(pickupTime));
            else
                pickupDateTime = pickupDate.Value.Date;
        }

        var (success, message, _) = await _service.CreateApplicationAsync(CurrentMemberId.Value, productId, quantity, pickupDateTime);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("Index");
    }

    /// <summary>
    /// 會員 - 我的租借列表
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var rentals = await _service.GetByMemberAsync(CurrentMemberId.Value);
        return View(rentals);
    }

    /// <summary>
    /// 會員 - 申請續借
    /// </summary>
    public async Task<IActionResult> Renew(int id)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null || rental.MemberId != CurrentMemberId)
            return RedirectToAction("Index");

        var (success, message) = await _service.RenewAsync(id);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("Index");
    }

    #endregion

    #region 後台

    /// <summary>
    /// 後台 - 租借列表
    /// </summary>
    public async Task<IActionResult> AdminIndex(string? status)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var rentals = await _service.GetAllAsync(status);
        ViewBag.Status = status;
        return View(rentals);
    }

    /// <summary>
    /// 後台 - 新增租借（直接借出，跳過審核）
    /// </summary>
    public async Task<IActionResult> AdminCreate()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var games = await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return View(games);
    }

    /// <summary>
    /// 後台 - 建立租借
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminCreate([FromForm] CreateRentalRequest req)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var product = await _db.Products.FindAsync(req.ProductId);
        if (product == null)
        {
            TempData["ErrorMessage"] = "遊戲不存在";
            return RedirectToAction("AdminCreate");
        }

        // 租金計算邏輯
        decimal rentalFee = product.Price switch
        {
            <= 1000 => 100m,
            <= 2000 => 200m,
            <= 3000 => 300m,
            _ => 400m
        };
        if (req.RentalFee < rentalFee) req.RentalFee = rentalFee;

        // 押金計算邏輯
        // 押金計算：LevelId == 2 為已註冊會員，免押金；其餘（含非會員）需押金
        decimal deposit = 0;
        string renterName;
        string renterPhone;

        if (req.MemberId.HasValue)
        {
            var member = await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == req.MemberId.Value);
            if (member == null)
            {
                TempData["ErrorMessage"] = "找不到會員";
                return RedirectToAction("AdminCreate");
            }
            renterName = member.Name;
            renterPhone = member.Phone;
            deposit = member.Level.Name != "非會員" ? 0m : product.Price;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.RenterName) || string.IsNullOrWhiteSpace(req.RenterPhone))
            {
                TempData["ErrorMessage"] = "請填寫姓名與電話";
                return RedirectToAction("AdminCreate");
            }
            renterName = req.RenterName;
            renterPhone = req.RenterPhone;
            deposit = product.Price; // 非會員押金
        }

        var rental = new GameRental
        {
            MemberId = req.MemberId,
            ProductId = req.ProductId,
            BorrowDate = req.BorrowDate,
            DueDate = req.DueDate,
            Deposit = deposit,
            RentalFee = req.RentalFee,
            RenterName = renterName,
            RenterPhone = renterPhone,
            Status = "Borrowed",
            CreatedAt = DateTime.Now,
            Member = req.MemberId.HasValue
                ? await _db.Members.FindAsync(req.MemberId.Value)
                : null,
            Product = product
        };

        _db.GameRentals.Add(rental);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"租借建立成功：{product.Name}，租金 {rental.RentalFee:N0} 元" + (deposit > 0 ? $"，押金 {deposit:N0} 元" : "");
        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 審核（通過 = 借出）
    /// </summary>
    public async Task<IActionResult> Approve(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _service.ApproveAsync(id);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 拒絕申請
    /// </summary>
    public async Task<IActionResult> Reject(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _service.RejectAsync(id);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 確認借出
    /// </summary>
    public async Task<IActionResult> Borrow(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _service.BorrowAsync(id);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 歸還
    /// </summary>
    public async Task<IActionResult> Return(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _service.ReturnAsync(id);
        if (!success)
            TempData["ErrorMessage"] = message;
        else
            TempData["SuccessMessage"] = message;

        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 建立訂單
    /// </summary>
    public async Task<IActionResult> CreateOrder(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var rental = await _db.GameRentals.Include(r => r.Product).FirstOrDefaultAsync(r => r.Id == id);
        if (rental == null)
        {
            TempData["ErrorMessage"] = "租借記錄不存在";
            return RedirectToAction("AdminIndex");
        }

        if (rental.Status != "Borrowed" && rental.Status != "Renewed" && rental.Status != "Overdue" && rental.Status != "Returned")
        {
            TempData["ErrorMessage"] = "此租借狀態無法建立訂單";
            return RedirectToAction("AdminIndex");
        }

        decimal total = rental.RentalFee;

        var order = new Order
        {
            OrderType = "GameRental",
            MemberId = rental.MemberId,
            TotalAmount = total,
            DiscountAmount = 0,
            FinalAmount = total,
            PaymentStatus = "Paid",
            PaymentMethod = "Cash",
            Notes = $"遊戲租借：{rental.Product.Name}，租金 {rental.RentalFee:N0} 元",
            CreatedAt = DateTime.Now
        };

        var orderItem = new OrderItem
        {
            Order = order,
            ItemType = "GameRental",
            ItemId = rental.Id,
            ItemName = $"遊戲租借：{rental.Product.Name}",
            UnitPrice = rental.RentalFee,
            Quantity = 1,
            Subtotal = rental.RentalFee
        };

        _db.Orders.Add(order);
        _db.OrderItems.Add(orderItem);
        rental.OrderId = order.Id;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"訂單已建立，金額：{total:N0} 元";
        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 編輯
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var rental = await _db.GameRentals.Include(r => r.Member).Include(r => r.Product).FirstOrDefaultAsync(r => r.Id == id);
        if (rental == null)
        {
            TempData["ErrorMessage"] = "找不到租借記錄";
            return RedirectToAction("AdminIndex");
        }
        return View(rental);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GameRental model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");
        var rental = await _db.GameRentals.FindAsync(model.Id);
        if (rental == null)
        {
            TempData["ErrorMessage"] = "找不到租借記錄";
            return RedirectToAction("AdminIndex");
        }
        rental.DueDate = model.DueDate;
        rental.Deposit = model.Deposit;
        rental.RentalFee = model.RentalFee;
        rental.Status = model.Status;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "更新成功";
        return RedirectToAction("AdminIndex");
    }

    /// <summary>
    /// 後台 - 刪除
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");
        var rental = await _db.GameRentals.FindAsync(id);
        if (rental == null)
        {
            TempData["ErrorMessage"] = "找不到租借記錄";
            return RedirectToAction("AdminIndex");
        }
        if (rental.Status == "Borrowed" || rental.Status == "Renewed")
        {
            TempData["ErrorMessage"] = "借出中無法刪除，請先完成歸還";
            return RedirectToAction("AdminIndex");
        }
        _db.GameRentals.Remove(rental);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "刪除成功";
        return RedirectToAction("AdminIndex");
    }

    #endregion
}

/// <summary>
/// 後台建立租借請求
/// </summary>
public class CreateRentalRequest
{
    public int? MemberId { get; set; }
    public string? RenterName { get; set; }
    public string? RenterPhone { get; set; }
    public int ProductId { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal RentalFee { get; set; }
}