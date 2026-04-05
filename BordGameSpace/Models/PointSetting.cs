using System.ComponentModel.DataAnnotations;

namespace BordGameSpace.Models;

/// <summary>
/// 積分系統設定
/// </summary>
public class PointSetting
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 消費獲得積分比率（每消費 1 元獲得多少積分）
    /// </summary>
    [Range(0, 100)]
    public decimal EarnRate { get; set; } = 1m;

    /// <summary>
    /// 積分折抵比率（每 1 積分折抵多少元）
    /// </summary>
    [Range(0, 100)]
    public decimal RedeemRate { get; set; } = 1m;

    /// <summary>
    /// 最低折抵積分門檻
    /// </summary>
    [Range(0, 10000)]
    public int MinRedeemPoints { get; set; } = 100;

    /// <summary>
    /// 適用此設定的等級 ID（0 = 全部會員）
    /// </summary>
    public int ApplicableLevelId { get; set; } = 0;

    /// <summary>
    /// 是否啟用積分功能
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 說明備註
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
