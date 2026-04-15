using System.Diagnostics;
using System.Text.Json;
using LexCore.Application.DTOs;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Infrastructure.Data;
using LexCore.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "Lawyer")]
public class AiController : ControllerBase
{
    private readonly IAiOrchestrationService _orchestration;
    private readonly ISmartExtractService _smartExtract;
    private readonly IResearchCacheService _researchCache;
    private readonly LegalQueryValidator _legalValidator;
    private readonly ITenantService _tenant;
    private readonly AppDbContext _db;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiOrchestrationService orchestration,
        ISmartExtractService smartExtract,
        IResearchCacheService researchCache,
        LegalQueryValidator legalValidator,
        ITenantService tenant,
        AppDbContext db,
        ILogger<AiController> logger)
    {
        _orchestration = orchestration;
        _smartExtract = smartExtract;
        _researchCache = researchCache;
        _legalValidator = legalValidator;
        _tenant = tenant;
        _db = db;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> Chat([FromBody] ChatRequest request)
    {
        var userId = _tenant.GetCurrentUserId();
        var firmId = _tenant.GetCurrentFirmId();
        var sw = Stopwatch.StartNew();

        // Quota check — prevents unlimited API usage
        var quotaError = await CheckAndIncrementQuotaAsync(userId, firmId, "chat");
        if (quotaError != null)
            return StatusCode(429, ApiResponse<ChatResponse>.ErrorResponse(
                quotaError, "QUOTA_EXCEEDED", 429));

        // Validate legal query
        var isLegal = await _legalValidator.IsLegalAsync(request.Message);
        if (!isLegal)
        {
            await LogAuditAsync(userId, firmId, "chat_non_legal", null, 0, 0, null, (int)sw.ElapsedMilliseconds, true);
            return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse(
                "Main sirf kanoon aur legal matters mein madad kar sakta hoon. Koi legal sawaal ho toh zaroor poochein. ⚖️",
                request.ConversationId ?? Guid.NewGuid(),
                false,
                request.Language)));
        }

        // Get or create conversation
        AiConversation conversation;
        if (request.ConversationId.HasValue)
        {
            conversation = await _db.AiConversations
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.UserId == userId)
                ?? CreateNewConversation(userId, firmId, request.CaseId);
        }
        else
        {
            conversation = CreateNewConversation(userId, firmId, request.CaseId);
            _db.AiConversations.Add(conversation);
            await _db.SaveChangesAsync();
        }

        // Compress if needed
        string? summary = conversation.Summary;
        var activeMessages = conversation.Messages.Where(m => !m.IsCompressed).ToList();
        if (activeMessages.Count >= 10)
        {
            var (compressedContent, ci, co) = await _orchestration.CompressAsync(conversation.Messages.ToList());
            conversation.Summary = compressedContent;
            conversation.SummaryTokenCount = ci + co;
            foreach (var msg in conversation.Messages)
                msg.IsCompressed = true;
            summary = compressedContent;
            await _db.SaveChangesAsync();
            activeMessages = new List<AiMessage>();
        }

        // Optional SmartExtract
        SmartExtract? extract = null;
        if (request.CaseId.HasValue)
        {
            try { extract = await _smartExtract.ExtractFromCaseContextAsync(request.CaseId.Value, "chat", userId); }
            catch (Exception ex) { _logger.LogWarning(ex, "SmartExtract failed for chat"); }
        }

        var ctx = new AiTaskContext
        {
            Task = "chat",
            UserMessage = request.Message,
            History = activeMessages,
            ConversationSummary = summary,
            SmartExtract = extract,
            Language = request.Language
        };

        var (content, inputTokens, outputTokens) = await _orchestration.ChatAsync(ctx);
        sw.Stop();

        // Save user and AI messages
        var userMsg = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Message,
            ContentLanguage = request.Language,
            TokenCount = inputTokens
        };
        var aiMsg = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = content,
            ContentLanguage = request.Language,
            TokenCount = outputTokens
        };
        _db.AiMessages.AddRange(userMsg, aiMsg);

        conversation.TotalTokensUsed += inputTokens + outputTokens;
        conversation.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(conversation.Title))
            conversation.Title = request.Message.Length > 60 ? request.Message[..60] + "…" : request.Message;

        await _db.SaveChangesAsync();
        await LogAuditAsync(userId, firmId, "chat", request.CaseId, inputTokens, outputTokens, null, (int)sw.ElapsedMilliseconds, true);

        return Ok(ApiResponse<ChatResponse>.SuccessResponse(new ChatResponse(
            content,
            conversation.Id,
            true,
            request.Language)));
    }

    [HttpPost("draft")]
    public async Task<ActionResult<ApiResponse<DraftResponse>>> Draft([FromBody] DraftRequest request)
    {
        var userId = _tenant.GetCurrentUserId();
        var firmId = _tenant.GetCurrentFirmId();
        var sw = Stopwatch.StartNew();

        // Quota check
        var quotaError = await CheckAndIncrementQuotaAsync(userId, firmId, "draft");
        if (quotaError != null)
            return StatusCode(429, ApiResponse<DraftResponse>.ErrorResponse(
                quotaError, "QUOTA_EXCEEDED", 429));

        SmartExtract? extract = null;
        if (request.CaseId.HasValue)
        {
            try { extract = await _smartExtract.ExtractFromCaseContextAsync(request.CaseId.Value, $"draft_{request.DocumentType}", userId); }
            catch (Exception ex) { _logger.LogWarning(ex, "SmartExtract failed for draft"); }
        }

        var ctx = new AiTaskContext
        {
            Task = "draft",
            UserMessage = $"Draft a {request.DocumentType} document. {request.Instructions ?? string.Empty}",
            SmartExtract = extract,
            Language = request.Language
        };

        var (content, inputTokens, outputTokens) = await _orchestration.DraftAsync(ctx);
        sw.Stop();

        var draft = new AiDraft
        {
            TenantId = firmId ?? userId,
            UserId = userId,
            CaseId = request.CaseId,
            DocumentType = request.DocumentType,
            Title = $"{request.DocumentType} - {DateTime.UtcNow:dd MMM yyyy}",
            Content = content,
            SmartExtractUsed = extract != null ? JsonSerializer.Serialize(extract) : null,
            TokenCount = inputTokens + outputTokens,
            PrintReady = true
        };
        _db.AiDrafts.Add(draft);
        await _db.SaveChangesAsync();

        await LogAuditAsync(userId, firmId, "draft", request.CaseId, inputTokens, outputTokens, null, (int)sw.ElapsedMilliseconds, true);

        return Ok(ApiResponse<DraftResponse>.SuccessResponse(new DraftResponse(draft.Id, content, request.DocumentType, true)));
    }

    [HttpPost("research")]
    public async Task<ActionResult<ApiResponse<ResearchResponse>>> Research([FromBody] ResearchRequest request)
    {
        var userId = _tenant.GetCurrentUserId();
        var firmId = _tenant.GetCurrentFirmId();
        var sw = Stopwatch.StartNew();

        // Quota check
        var quotaError = await CheckAndIncrementQuotaAsync(userId, firmId, "research");
        if (quotaError != null)
            return StatusCode(429, ApiResponse<ResearchResponse>.ErrorResponse(
                quotaError, "QUOTA_EXCEEDED", 429));

        // Check cache
        var cached = await _researchCache.GetCachedResultAsync(request.Query);
        if (cached != null)
        {
            var cachedCitations = ParseJsonList(cached.Citations);
            await LogAuditAsync(userId, firmId, "research_cache_hit", request.CaseId, 0, 0, null, (int)sw.ElapsedMilliseconds, true);
            return Ok(ApiResponse<ResearchResponse>.SuccessResponse(new ResearchResponse(
                cached.Result, cachedCitations, new List<string>(), true)));
        }

        SmartExtract? extract = null;
        if (request.CaseId.HasValue)
        {
            try { extract = await _smartExtract.ExtractFromCaseContextAsync(request.CaseId.Value, "research", userId); }
            catch (Exception ex) { _logger.LogWarning(ex, "SmartExtract failed for research"); }
        }

        var userMessage = string.IsNullOrEmpty(request.IpcSections)
            ? request.Query
            : $"{request.Query}\nRelevant IPC Sections: {request.IpcSections}";

        var ctx = new AiTaskContext
        {
            Task = "research",
            UserMessage = userMessage,
            SmartExtract = extract,
            Language = "en"
        };

        var (content, inputTokens, outputTokens) = await _orchestration.ResearchAsync(ctx);
        sw.Stop();

        var citations = ExtractCitations(content);
        var citationsJson = JsonSerializer.Serialize(citations);

        // Save research record
        var research = new AiResearch
        {
            TenantId = firmId ?? userId,
            UserId = userId,
            CaseId = request.CaseId,
            Query = request.Query,
            Result = content,
            Citations = citationsJson,
            TokenCount = inputTokens + outputTokens,
            IsCached = false
        };
        _db.AiResearches.Add(research);
        await _db.SaveChangesAsync();

        // Save to cache
        await _researchCache.SaveToCacheAsync(request.Query, content, citationsJson);

        await LogAuditAsync(userId, firmId, "research", request.CaseId, inputTokens, outputTokens, null, (int)sw.ElapsedMilliseconds, true);

        return Ok(ApiResponse<ResearchResponse>.SuccessResponse(new ResearchResponse(
            content, citations, new List<string>(), false)));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<ConversationListItem>>>> GetHistory()
    {
        var userId = _tenant.GetCurrentUserId();

        var conversations = await _db.AiConversations
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(50)
            .Select(c => new ConversationListItem(
                c.Id,
                c.Title,
                c.ConversationType,
                null,
                c.CreatedAt,
                c.Messages.Count))
            .ToListAsync();

        return Ok(ApiResponse<List<ConversationListItem>>.SuccessResponse(conversations));
    }

    [HttpGet("drafts")]
    public async Task<ActionResult<ApiResponse<List<DraftListItem>>>> GetDrafts()
    {
        var userId = _tenant.GetCurrentUserId();

        var drafts = await _db.AiDrafts
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(50)
            .Select(d => new DraftListItem(d.Id, d.Title, d.DocumentType, d.Status, d.PrintReady, d.CreatedAt))
            .ToListAsync();

        return Ok(ApiResponse<List<DraftListItem>>.SuccessResponse(drafts));
    }

    [HttpGet("draft/{id}")]
    public async Task<ActionResult<ApiResponse<DraftDetailResponse>>> GetDraft(Guid id)
    {
        var userId = _tenant.GetCurrentUserId();

        var draft = await _db.AiDrafts.FirstOrDefaultAsync(d => d.Id == id);
        if (draft == null) return NotFound(ApiResponse<DraftDetailResponse>.ErrorResponse("Draft not found", "NOT_FOUND", 404));
        if (draft.UserId != userId) return StatusCode(403, ApiResponse<DraftDetailResponse>.ErrorResponse("Access denied", "FORBIDDEN", 403));

        return Ok(ApiResponse<DraftDetailResponse>.SuccessResponse(new DraftDetailResponse(
            draft.Id, draft.Title, draft.Content, draft.DocumentType,
            draft.Status, draft.PrintReady, draft.SmartExtractUsed,
            draft.CreatedAt, draft.UpdatedAt)));
    }

    [HttpPatch("draft/{id}")]
    public async Task<ActionResult<ApiResponse<string>>> UpdateDraftStatus(Guid id, [FromBody] UpdateDraftStatusRequest request)
    {
        var userId = _tenant.GetCurrentUserId();

        var draft = await _db.AiDrafts.FirstOrDefaultAsync(d => d.Id == id);
        if (draft == null) return NotFound(ApiResponse<string>.ErrorResponse("Draft not found", "NOT_FOUND", 404));
        if (draft.UserId != userId) return StatusCode(403, ApiResponse<string>.ErrorResponse("Access denied", "FORBIDDEN", 403));

        draft.Status = request.Status;
        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<string>.SuccessResponse("Status updated"));
    }

    private static AiConversation CreateNewConversation(Guid userId, Guid? firmId, Guid? caseId) =>
        new()
        {
            UserId = userId,
            TenantId = firmId ?? userId,
            CaseId = caseId,
            ConversationType = "chat"
        };

    /// Checks and increments quota for the given action.
    /// Returns an error message if quota exceeded, null if allowed.
    private async Task<string?> CheckAndIncrementQuotaAsync(
        Guid userId, Guid? firmId, string action)
    {
        var tenantId = firmId ?? userId;
        var monthYear = DateTime.UtcNow.ToString("yyyy-MM");

        // Step 1: Ensure a quota row exists for this tenant+month.
        // If two concurrent first-requests both find null, one INSERT will
        // succeed and the other will hit the unique index constraint.
        // The DbUpdateException catch handles that gracefully.
        var existing = await _db.AiUsageQuotas
            .FirstOrDefaultAsync(q =>
                q.TenantId == tenantId &&
                q.MonthYear == monthYear);

        if (existing == null)
        {
            try
            {
                _db.AiUsageQuotas.Add(new AiUsageQuota
                {
                    TenantId = tenantId,
                    MonthYear = monthYear,
                    PlanTier = "basic"
                });
                await _db.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // Concurrent request already inserted — safe to continue.
                _db.ChangeTracker.Clear();
            }
        }

        // Step 2: Read current state with AsNoTracking (bypass EF cache).
        var quota = await _db.AiUsageQuotas.AsNoTracking()
            .FirstOrDefaultAsync(q =>
                q.TenantId == tenantId &&
                q.MonthYear == monthYear);

        if (quota == null)
            return "Quota record unavailable. Please try again.";

        var (chatLimit, draftLimit, researchLimit) =
            AiUsageQuota.GetLimits(quota.PlanTier);

        // Step 3: Pre-check limit to avoid unnecessary DB write.
        var limitError = action switch
        {
            "chat" when quota.ChatCount >= chatLimit =>
                $"Monthly AI chat limit reached ({chatLimit} messages). " +
                "Upgrade your plan for more.",
            "draft" when quota.DraftCount >= draftLimit =>
                $"Monthly AI draft limit reached ({draftLimit} drafts). " +
                "Upgrade your plan for more.",
            "research" when quota.ResearchCount >= researchLimit =>
                $"Monthly AI research limit reached ({researchLimit} queries). " +
                "Upgrade your plan for more.",
            _ => null
        };

        if (limitError != null)
            return limitError;

        // Step 4: ATOMIC increment with WHERE guard — race-condition-safe.
        // If another concurrent request consumed the last slot between
        // our read (Step 2) and this update, rowsAffected will be 0.
        var rowsAffected = action switch
        {
            "chat" => await _db.AiUsageQuotas
                .Where(q =>
                    q.TenantId == tenantId &&
                    q.MonthYear == monthYear &&
                    q.ChatCount < chatLimit)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.ChatCount, q => q.ChatCount + 1)
                    .SetProperty(q => q.UpdatedAt, DateTime.UtcNow)),

            "draft" => await _db.AiUsageQuotas
                .Where(q =>
                    q.TenantId == tenantId &&
                    q.MonthYear == monthYear &&
                    q.DraftCount < draftLimit)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.DraftCount, q => q.DraftCount + 1)
                    .SetProperty(q => q.UpdatedAt, DateTime.UtcNow)),

            "research" => await _db.AiUsageQuotas
                .Where(q =>
                    q.TenantId == tenantId &&
                    q.MonthYear == monthYear &&
                    q.ResearchCount < researchLimit)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.ResearchCount, q => q.ResearchCount + 1)
                    .SetProperty(q => q.UpdatedAt, DateTime.UtcNow)),

            _ => 0
        };

        // rowsAffected == 0 means the slot was taken by a concurrent request.
        if (rowsAffected == 0)
        {
            return action switch
            {
                "chat" =>
                    $"Monthly AI chat limit reached ({chatLimit} messages). " +
                    "Upgrade your plan for more.",
                "draft" =>
                    $"Monthly AI draft limit reached ({draftLimit} drafts). " +
                    "Upgrade your plan for more.",
                "research" =>
                    $"Monthly AI research limit reached ({researchLimit} queries). " +
                    "Upgrade your plan for more.",
                _ => "Quota limit reached."
            };
        }

        return null; // slot reserved successfully
    }

    private async Task LogAuditAsync(Guid userId, Guid? firmId, string action, Guid? caseId,
        int inputTokens, int outputTokens, string? model, int latencyMs, bool success)
    {
        _db.AiAuditLogs.Add(new AiAuditLog
        {
            TenantId = firmId ?? userId,
            UserId = userId,
            ActionType = action,
            CaseId = caseId,
            TokensInput = inputTokens,
            TokensOutput = outputTokens,
            ModelUsed = model,
            LatencyMs = latencyMs,
            Success = success,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        // Roll up token usage to the quota row atomically.
        // Skip cache hits and non-legal responses (0 tokens).
        if (inputTokens + outputTokens > 0)
        {
            var tenantId = firmId ?? userId;
            var monthYear = DateTime.UtcNow.ToString("yyyy-MM");
            var totalTokens = inputTokens + outputTokens;

            await _db.AiUsageQuotas
                .Where(q =>
                    q.TenantId == tenantId &&
                    q.MonthYear == monthYear)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.TotalTokensUsed,
                                 q => q.TotalTokensUsed + totalTokens)
                    .SetProperty(q => q.UpdatedAt, DateTime.UtcNow));
        }
    }

    private static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private static List<string> ExtractCitations(string content)
    {
        var citations = new List<string>();
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if ((trimmed.Contains("v.") || trimmed.Contains("vs.") || trimmed.Contains("versus")) &&
                (trimmed.Contains("AIR") || trimmed.Contains("SCC") || trimmed.Contains("SCR") || trimmed.Contains("HC") || trimmed.Contains("SC")))
            {
                citations.Add(trimmed.TrimStart('-', '*', ' '));
            }
        }
        return citations;
    }
}
