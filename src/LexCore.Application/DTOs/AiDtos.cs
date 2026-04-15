namespace LexCore.Application.DTOs;

public record ChatRequest(
    string Message,
    Guid? ConversationId,
    Guid? CaseId,
    string Language = "hi");

public record ChatResponse(
    string LexcoreResponse,
    Guid ConversationId,
    bool IsLegalQuery,
    string LanguageDetected);

public record DraftRequest(
    string DocumentType,
    Guid? CaseId,
    string? Instructions,
    string Language = "hi");

public record DraftResponse(
    Guid DraftId,
    string Content,
    string DocumentType,
    bool PrintReady);

public record ResearchRequest(
    string Query,
    Guid? CaseId,
    string? ResearchType,
    string? IpcSections);

public record ResearchResponse(
    string Result,
    List<string> Citations,
    List<string> RelevantSections,
    bool FromCache);

public record ConversationListItem(
    Guid Id,
    string? Title,
    string ConversationType,
    string? CaseName,
    DateTime CreatedAt,
    int MessageCount);

public record DraftListItem(
    Guid Id,
    string Title,
    string DocumentType,
    string Status,
    bool PrintReady,
    DateTime CreatedAt);

public record DraftDetailResponse(
    Guid Id,
    string Title,
    string Content,
    string DocumentType,
    string Status,
    bool PrintReady,
    string? SmartExtractUsed,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpdateDraftStatusRequest(string Status);
