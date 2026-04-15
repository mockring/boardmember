using BordGameSpace.Data;
using BordGameSpace.Models;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Services;

public class SpaceReservationService
{
    private readonly AppDbContext _db;

    public SpaceReservationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 計算時數（向下取整到小時，最少1小時）
    /// </summary>
    public int CalculateHours(TimeSpan start, TimeSpan end)
    {
        var diff = end - start;
        if (diff.TotalHours < 1) return 1;
        return (int)Math.Floor(diff.TotalHours);
    }

    /// <summary>
    /// 取得指定日期的時單價（平日/假日）
    /// </summary>
    public async Task<decimal> GetHourlyRateAsync(int memberId, DateTime date)
    {
        var member = await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null) return 60; // 預設非會員

        bool isHoliday = IsHoliday(date);
        return isHoliday ? member.Level.HolidayHourlyRate : member.Level.WeekdayHourlyRate;
    }

    /// <summary>
    /// 是否為假日（周六、日）
    /// </summary>
    public bool IsHoliday(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// 檢查時段是否與現有預約衝突
    /// </summary>
    public async Task<bool> HasTimeConflictAsync(DateTime reservationDate, TimeSpan startTime, TimeSpan endTime, int? excludeId = null)
    {
        // 先用日期過濾，再取出在記憶體中比對時段（EF Core InMemory 不支援 DateTime.Date 和 TimeSpan 比較）
        var dateOnly = reservationDate.Date;
        var reservations = await _db.SpaceReservations
            .Where(r => r.ReservationDate >= dateOnly && r.ReservationDate < dateOnly.AddDays(1) &&
                        r.Status != "Cancelled" && r.Status != "Rejected")
            .ToListAsync();

        if (excludeId.HasValue)
            reservations = reservations.Where(r => r.Id != excludeId.Value).ToList();

        return reservations.Any(r =>
            startTime < r.EndTime && endTime > r.StartTime);
    }

    /// <summary>
    /// 會員提交預約申請
    /// </summary>
    public async Task<(bool Success, string Message, SpaceReservation? Reservation)> CreateAsync(
        int memberId, string name, string phone,
        DateTime reservationDate, TimeSpan startTime, TimeSpan endTime, string? notes,
        int peopleCount = 2, string spaceType = "訂位")
    {
        var member = await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null)
            return (false, "會員不存在", null);

        if (startTime >= endTime)
            return (false, "開始時間必須早於結束時間", null);

        // 檢查時段衝突
        if (await HasTimeConflictAsync(reservationDate, startTime, endTime))
            return (false, "此時段已有其他預約，請選擇其他時段", null);

        var reservation = new SpaceReservation
        {
            MemberId = memberId,
            Name = name,
            Phone = phone,
            ReservationDate = reservationDate,
            StartTime = startTime,
            EndTime = endTime,
            Hours = 0,
            HourlyRate = 0,
            TotalAmount = 0,
            PeopleCount = peopleCount,
            SpaceType = spaceType,
            Status = "Pending",
            Notes = notes,
            CreatedAt = DateTime.Now
        };

        _db.SpaceReservations.Add(reservation);
        await _db.SaveChangesAsync();
        return (true, "預約申請已送出，等待管理員審核", reservation);
    }

    /// <summary>
    /// 取得會員的預約列表
    /// </summary>
    public async Task<List<SpaceReservation>> GetByMemberAsync(int memberId)
    {
        return await _db.SpaceReservations
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 取得所有預約（後台）
    /// </summary>
    public async Task<List<SpaceReservation>> GetAllAsync(string? status = null)
    {
        var query = _db.SpaceReservations.Include(r => r.Member).AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// 審核預約（通過或拒絕）
    /// </summary>
    public async Task<(bool Success, string Message)> ReviewAsync(int id, bool approved)
    {
        var reservation = await _db.SpaceReservations.FindAsync(id);
        if (reservation == null)
            return (false, "預約不存在");

        reservation.Status = approved ? "Approved" : "Rejected";
        await _db.SaveChangesAsync();
        return (true, approved ? "已通過審核" : "已拒絕");
    }

    /// <summary>
    /// 取消預約
    /// </summary>
    public async Task<(bool Success, string Message)> CancelAsync(int id, int memberId)
    {
        var reservation = await _db.SpaceReservations.FindAsync(id);
        if (reservation == null)
            return (false, "預約不存在");
        if (reservation.MemberId != memberId)
            return (false, "無權限取消此預約");
        if (reservation.Status == "Cancelled")
            return (false, "已取消");

        reservation.Status = "Cancelled";
        await _db.SaveChangesAsync();
        return (true, "已取消預約");
    }

    /// <summary>
    /// 依 ID 取得預約
    /// </summary>
    public async Task<SpaceReservation?> GetByIdAsync(int id)
    {
        return await _db.SpaceReservations
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// 更新預約
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateAsync(SpaceReservation reservation)
    {
        var existing = await _db.SpaceReservations.FindAsync(reservation.Id);
        if (existing == null)
            return (false, "預約不存在");

        existing.Name = reservation.Name;
        existing.Phone = reservation.Phone;
        existing.ReservationDate = reservation.ReservationDate;
        existing.StartTime = reservation.StartTime;
        existing.EndTime = reservation.EndTime;
        existing.Status = reservation.Status;
        existing.Notes = reservation.Notes;

        await _db.SaveChangesAsync();
        return (true, "預約已更新");
    }

    /// <summary>
    /// 刪除預約
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        var reservation = await _db.SpaceReservations.FindAsync(id);
        if (reservation == null)
            return (false, "預約不存在");

        _db.SpaceReservations.Remove(reservation);
        await _db.SaveChangesAsync();
        return (true, "預約已刪除");
    }
}
