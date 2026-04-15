namespace LexCore.Application.Interfaces;

public class SmartExtract
{
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public string? JudgeName { get; set; }
    public string? ClientName { get; set; }
    public string? OppositeParty { get; set; }
    public string? IpcSections { get; set; }
    public string? Stage { get; set; }
    public string? NextDate { get; set; }
    public string? KeyFacts { get; set; }
    public string? ContradictionFlags { get; set; }
    public List<string> DocumentTypes { get; set; } = new();
    public string? LastOrderSummary { get; set; }
    public string? AdditionalInstructions { get; set; }
}

public interface ISmartExtractService
{
    Task<SmartExtract> ExtractFromCaseContextAsync(Guid caseId, string purpose, Guid userId);
}
