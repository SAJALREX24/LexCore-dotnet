namespace LexCore.Application.DTOs.Analytics;

public class OverviewDto
{
    public int TotalCases { get; set; }
    public int ActiveCases { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public int HearingsThisWeek { get; set; }
    public int TotalClients { get; set; }
    public int TotalLawyers { get; set; }
    public int PendingInvoices { get; set; }
}

public class CasesAnalyticsDto
{
    public List<StatusBreakdown> ByStatus { get; set; } = new();
    public List<TypeBreakdown> ByType { get; set; } = new();
}

public class StatusBreakdown
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TypeBreakdown
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class RevenueAnalyticsDto
{
    public List<MonthlyRevenue> MonthlyData { get; set; } = new();
    public decimal TotalRevenue { get; set; }
}

public class MonthlyRevenue
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class LawyerPerformanceDto
{
    public Guid LawyerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ActiveCases { get; set; }
    public int ClosedCases { get; set; }
    public int TotalHearings { get; set; }
    public int UpcomingHearings { get; set; }
}

public class HearingsAnalyticsDto
{
    public List<MonthlyHearings> MonthlyData { get; set; } = new();
    public int TotalHearings { get; set; }
    public int CompletedHearings { get; set; }
    public int PendingHearings { get; set; }
}

public class MonthlyHearings
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Count { get; set; }
}
