using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;

namespace BordGameSpace.Controllers;

public class ProductsController : BaseController
{
    private readonly AdminService _adminService;

    public ProductsController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// 商品列表
    /// </summary>
    public async Task<IActionResult> Index(string? category = null, bool? isActive = null, string? search = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var products = await _adminService.GetAllProductsAsync(category, isActive, search);
        var categories = await _adminService.GetCategoriesAsync();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategory = category;
        ViewBag.SelectedIsActive = isActive;
        ViewBag.Search = search;

        return View(products);
    }

    /// <summary>
    /// 新增商品頁面
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var categories = await _adminService.GetCategoriesAsync();
        ViewBag.Categories = categories;
        return View();
    }

    /// <summary>
    /// 處理新增商品
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var cats = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = cats;
            return View(model);
        }

        var (success, message, _) = await _adminService.CreateProductAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var categories = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯商品頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var product = await _adminService.GetProductByIdAsync(id);
        if (product == null)
            return NotFound();

        var categories = await _adminService.GetCategoriesAsync();
        ViewBag.Categories = categories;

        return View(product);
    }

    /// <summary>
    /// 處理編輯商品
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Product model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var cats = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = cats;
            return View(model);
        }

        var (success, message) = await _adminService.UpdateProductAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var categories = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 刪除商品
    /// </summary>
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.DeleteProductAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 快速調整庫存
    /// </summary>
    public async Task<IActionResult> AdjustStock(int id, int quantity)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.UpdateStockAsync(id, quantity);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 匯入商品頁面
    /// </summary>
    public IActionResult Import()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }

    /// <summary>
    /// 處理匯入商品
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
                    var category = row.Cell(2).GetString();
                    var priceStr = row.Cell(3).GetString();
                    var stockStr = row.Cell(4).GetString();
                    var lowStockAlertStr = row.Cell(5).GetString();
                    var description = row.Cell(6).GetString();

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
                    {
                        failureCount++;
                        failureDetails.Add($"列 {row.RowNumber()}: 必填欄位 (Name 或 Category) 空白");
                        continue;
                    }

                    if (!decimal.TryParse(priceStr, out var price) || price < 0)
                    {
                        failureCount++;
                        failureDetails.Add($"列 {row.RowNumber()}: Price 格式不正確或為負數");
                        continue;
                    }

                    int? stock = string.IsNullOrWhiteSpace(stockStr) ? null : (int.TryParse(stockStr, out var s) ? s : null);
                    int lowStockAlert = string.IsNullOrWhiteSpace(lowStockAlertStr) ? 1 : (int.TryParse(lowStockAlertStr, out var l) ? l : 1);

                    // Check if product with same name exists
                    var existingProduct = await _adminService.GetProductByNameAsync(name);

                    if (existingProduct != null)
                    {
                        // Update existing product
                        existingProduct.Category = category;
                        existingProduct.Price = price;
                        existingProduct.Stock = stock;
                        existingProduct.LowStockAlert = lowStockAlert;
                        existingProduct.Description = string.IsNullOrWhiteSpace(description) ? null : description;
                        existingProduct.UpdatedAt = DateTime.Now;

                        await _adminService.UpdateProductAsync(existingProduct);
                    }
                    else
                    {
                        // Create new product
                        var newProduct = new Product
                        {
                            Name = name,
                            Category = category,
                            Price = price,
                            Stock = stock,
                            LowStockAlert = lowStockAlert,
                            Description = string.IsNullOrWhiteSpace(description) ? null : description,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };

                        await _adminService.CreateProductAsync(newProduct);
                    }

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
