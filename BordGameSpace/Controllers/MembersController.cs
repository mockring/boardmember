using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.DTOs;

namespace BordGameSpace.Controllers;

public class MembersController : BaseController
{
    private readonly AdminService _adminService;

    public MembersController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// 會員列表
    /// </summary>
    public async Task<IActionResult> Index(string? search = null, int? levelId = null, bool? status = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var members = await _adminService.GetAllMembersAsync(search, levelId, status);
        var levels = await _adminService.GetAllLevelsAsync();

        ViewBag.Search = search;
        ViewBag.LevelId = levelId;
        ViewBag.Status = status;
        ViewBag.Levels = levels;

        return View(members);
    }

    /// <summary>
    /// 會員詳情
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var member = await _adminService.GetMemberByIdAsync(id);
        if (member == null)
            return NotFound();

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        return View(member);
    }

    /// <summary>
    /// 新增會員頁面
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        return View();
    }

    /// <summary>
    /// 處理新增會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMemberDto dto)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        if (!ModelState.IsValid)
            return View("Create");

        var member = new Member
        {
            Name = dto.Name.Trim(),
            Phone = dto.Phone.Trim(),
            Email = dto.Email.Trim(),
            Birthday = dto.Birthday
        };

        var (success, message) = await _adminService.CreateMemberAsync(member);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View("Create");
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯會員頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var member = await _adminService.GetMemberByIdAsync(id);
        if (member == null)
            return NotFound();

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        var dto = new EditMemberDto
        {
            Id = member.Id,
            Name = member.Name,
            Phone = member.Phone,
            Email = member.Email,
            Birthday = member.Birthday,
            LevelId = member.LevelId,
            Status = member.Status
        };

        return View(dto);
    }

    /// <summary>
    /// 處理編輯會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditMemberDto dto, bool changePassword = false, string? newPassword = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var levels = await _adminService.GetAllLevelsAsync();
            ViewBag.Levels = levels;
            return View(dto);
        }

        var model = new Member
        {
            Id = dto.Id,
            Name = dto.Name,
            Phone = dto.Phone,
            Email = dto.Email,
            Birthday = dto.Birthday,
            LevelId = dto.LevelId,
            Status = dto.Status,
            PasswordHash = ""
        };

        if (changePassword && !string.IsNullOrWhiteSpace(newPassword))
        {
            model.PasswordHash = newPassword;
        }

        var (success, message) = await _adminService.UpdateMemberAsync(model, changePassword);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var levels = await _adminService.GetAllLevelsAsync();
            ViewBag.Levels = levels;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 手動調整會員等級
    /// </summary>
    public async Task<IActionResult> ChangeLevel(int id, int levelId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.ChangeMemberLevelAsync(id, levelId);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Details", new { id });
    }

    /// <summary>
    /// 重設會員密碼
    /// </summary>
    public async Task<IActionResult> ResetPassword(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.ResetMemberPasswordAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Details", new { id });
    }

    /// <summary>
    /// 停用/啟用會員
    /// </summary>
    public async Task<IActionResult> ToggleStatus(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.ToggleMemberStatusAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 匯入會員頁面
    /// </summary>
    public IActionResult Import()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }

    /// <summary>
    /// 處理匯入會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "請選擇要上傳的檔案";
            return View();
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "請上傳 .xlsx 格式的檔案";
            return View();
        }

        var successCount = 0;
        var failureCount = 0;
        var failureDetails = new List<string>();

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();
            var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header row

            foreach (var row in rows)
            {
                try
                {
                    var name = row.Cell(1).GetString();
                    var phone = row.Cell(2).GetString();
                    var email = row.Cell(3).GetString();
                    var birthdayStr = row.Cell(4).GetString();
                    var levelName = row.Cell(5).GetString();

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email))
                    {
                        failureCount++;
                        failureDetails.Add($"列 {row.RowNumber()}: 必填欄位 (Name, Phone, Email) 空白");
                        continue;
                    }

                    // Check if phone already exists
                    var existingMember = await _adminService.GetMemberByPhoneAsync(phone);
                    if (existingMember != null)
                    {
                        failureCount++;
                        failureDetails.Add($"列 {row.RowNumber()}: 電話 {phone} 已存在");
                        continue;
                    }

                    DateTime? birthday = null;
                    if (!string.IsNullOrWhiteSpace(birthdayStr))
                    {
                        if (!DateTime.TryParse(birthdayStr, out var parsedBirthday))
                        {
                            failureCount++;
                            failureDetails.Add($"列 {row.RowNumber()}: Birthday 格式不正確，請使用 yyyy-MM-dd");
                            continue;
                        }
                        birthday = parsedBirthday;
                    }

                    int levelId = 2; // default 會員
                    if (!string.IsNullOrWhiteSpace(levelName))
                    {
                        var level = await _adminService.GetAllLevelsAsync();
                        var matchedLevel = level.FirstOrDefault(l => l.Name == levelName);
                        if (matchedLevel != null)
                            levelId = matchedLevel.Id;
                    }

                    var member = new Member
                    {
                        Name = name.Trim(),
                        Phone = phone.Trim(),
                        Email = email.Trim(),
                        Birthday = birthday,
                        LevelId = levelId,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(phone.Trim().Substring(phone.Trim().Length - 6)),
                        Status = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    await _adminService.CreateMemberAsync(member);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    failureDetails.Add($"列 {row.RowNumber()}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"讀取檔案失敗: {ex.Message}";
            return View();
        }

        TempData["SuccessCount"] = successCount;
        TempData["FailureCount"] = failureCount;
        if (failureDetails.Count > 0)
        {
            TempData["FailureDetails"] = string.Join("\n", failureDetails);
        }

        return View();
    }
}
