namespace LexCore.Application.Interfaces;

public interface IAiService
{
    Task<string> GenerateClientUpdateAsync(string hearingNote, string caseTitle, string clientName);
    Task<string> AnalyzeDocumentAsync(string documentText);
    Task<string> DraftDocumentAsync(string caseDetails, string documentType, string instructions);
}
