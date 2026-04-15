namespace BordGameSpace.Models;

public class ProductSalesRank
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class MemberSpendRank
{
    public int MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public decimal TotalSpending { get; set; }
    public int OrderCount { get; set; }
}

public class LevelDistribution
{
    public string LevelName { get; set; } = "";
    public int MemberCount { get; set; }
}
