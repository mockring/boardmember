using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace BordGameSpace.Controllers;

public class RestockController : BaseController
{
    private readonly AppDbContext _db;

    public RestockController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 進貨記錄列表
    /// </summary>
    public async Task<IActionResult> Index(int? productId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var query = _db.RestockRecords
            .Include(r => r.Product)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        if (productId.HasValue)
            query = query.Where(r => r.ProductId == productId.Value);

        var records = await query.Take(100).ToListAsync();
        var products = await _db.Products.Where(p => p.IsActive == true).OrderBy(p => p.Name).ToListAsync();

        ViewBag.Records = records;
        ViewBag.Products = products;
        ViewBag.SelectedProductId = productId;
        return View();
    }

    /// <summary>
    /// 新增進貨記錄
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var products = await _db.Products.Where(p => p.IsActive == true).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Products = products;
        return View();
    }

    /// <summary>
    /// 儲存進貨記錄
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int productId, int quantity, string? supplier, string? supplierPhone, string? notes)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
        {
            TempData["ErrorMessage"] = "商品不存在";
            return RedirectToAction("Create");
        }

        if (quantity <= 0)
        {
            TempData["ErrorMessage"] = "數量必須大於 0";
            return RedirectToAction("Create");
        }

        var record = new RestockRecord
        {
            ProductId = productId,
            Quantity = quantity,
            Supplier = supplier,
            Phone = supplierPhone,
            Notes = notes,
            CreatedAt = DateTime.Now
        };

        // 更新庫存
        product.Stock = (product.Stock ?? 0) + quantity;
        product.UpdatedAt = DateTime.Now;

        _db.RestockRecords.Add(record);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"已新增進貨記錄，{product.Name} 庫存更新為 {product.Stock}";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Excel 批次匯入進貨記錄
    /// </summary>
    public IActionResult Import()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "請選擇 Excel 檔案";
            return View();
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "只支援 .xlsx 格式";
            return View();
        }

        int successCount = 0;
        int failureCount = 0;
        var failureDetails = new List<string>();

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                int rowNum = row.RowNumber();
                var productName = row.Cell(1).GetValue<string>()?.Trim();
                var quantityStr = row.Cell(2).GetValue<string>()?.Trim();
                var supplier = row.Cell(3).GetValue<string>()?.Trim();
                var phone = row.Cell(4).GetValue<string>()?.Trim();
                var notes = row.Cell(5).GetValue<string>()?.Trim();

                if (string.IsNullOrEmpty(productName))
                {
                    failureCount++;
                    failureDetails.Add($"第 {rowNum} 行：商品名稱為必填");
                    continue;
                }

                if (!int.TryParse(quantityStr, out int quantity) || quantity <= 0)
                {
                    failureCount++;
                    failureDetails.Add($"第 {rowNum} 行（{productName}）：數量必須為正整數");
                    continue;
                }

                var product = await _db.Products.FirstOrDefaultAsync(p => p.Name == productName);
                if (product == null)
                {
                    failureCount++;
                    failureDetails.Add($"第 {rowNum} 行：找不到商品「{productName}」");
                    continue;
                }

                var record = new RestockRecord
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    Supplier = string.IsNullOrEmpty(supplier) ? null : supplier,
                    Phone = string.IsNullOrEmpty(phone) ? null : phone,
                    Notes = string.IsNullOrEmpty(notes) ? null : notes,
                    CreatedAt = DateTime.Now
                };

                product.Stock = (product.Stock ?? 0) + quantity;
                product.UpdatedAt = DateTime.Now;

                _db.RestockRecords.Add(record);
                successCount++;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"匯入失敗：{ex.Message}";
            return View();
        }

        TempData["SuccessCount"] = successCount;
        TempData["FailureCount"] = failureCount;
        TempData["FailureDetails"] = string.Join("\n", failureDetails);
        return View();
    }
}
