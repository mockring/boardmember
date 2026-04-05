using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BordGameSpace.Models;
using BordGameSpace.Services;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class BaseController : Controller
{
    protected const string MEMBER_SESSION_KEY = "MemberId";
    protected const string ADMIN_SESSION_KEY = "AdminId";

    /// <summary>
    /// 取得目前登入的會員ID
    /// </summary>
    protected int? CurrentMemberId => HttpContext.Session.GetInt32(MEMBER_SESSION_KEY);

    /// <summary>
    /// 檢查是否已登入
    /// </summary>
    protected bool IsMemberLoggedIn => CurrentMemberId.HasValue;

    /// <summary>
    /// 取得目前登入的管理者ID
    /// </summary>
    protected int? CurrentAdminId => HttpContext.Session.GetInt32(ADMIN_SESSION_KEY);

    /// <summary>
    /// 檢查是否已登入管理者
    /// </summary>
    protected bool IsAdminLoggedIn => CurrentAdminId.HasValue;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        // 嘗試從 Session 取得會員資訊並設定到 ViewBag
        if (CurrentMemberId.HasValue)
        {
            var db = context.HttpContext.RequestServices.GetService<AppDbContext>();
            if (db != null)
            {
                var member = db.Members
                    .Include(m => m.Level)
                    .FirstOrDefault(m => m.Id == CurrentMemberId.Value);

                if (member != null)
                {
                    ViewBag.IsLoggedIn = true;
                    ViewBag.MemberName = member.Name;
                    ViewBag.MemberLevel = member.Level?.Name ?? "未知";
                }
            }
        }
        else
        {
            ViewBag.IsLoggedIn = false;
        }
    }
}
