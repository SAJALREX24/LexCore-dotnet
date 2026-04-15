using System.Text.RegularExpressions;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class SmartExtractService : ISmartExtractService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SmartExtractService> _logger;

    private static readonly Regex AadhaarRegex = new(@"\d{4}\s\d{4}\s\d{4}", RegexOptions.Compiled);
    private static readonly Regex PanRegex = new(@"[A-Z]{5}[0-9]{4}[A-Z]", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"\+91\d{10}|\d{10}", RegexOptions.Compiled);

    public SmartExtractService(AppDbContext db, ILogger<SmartExtractService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SmartExtract> ExtractFromCaseContextAsync(Guid caseId, string purpose, Guid userId)
    {
        // Security: verify the requesting user owns this case before extracting.
        // v1 solo-only: ownership is established via CaseLawyers junction table.
        var caseEntity = await _db.Cases
            .Include(c => c.CaseLawyers).ThenInclude(cl => cl.Lawyer)
            .Include(c => c.CaseClients).ThenInclude(cc => cc.Client)
            .Include(c => c.Hearings.OrderByDescending(h => h.HearingDate))
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c =>
                c.Id == caseId &&
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (caseEntity == null)
        {
            _logger.LogWarning("SmartExtract denied: user {UserId} attempted to access case {CaseId} without ownership", userId, caseId);
            return new SmartExtract { CaseId = caseId, CaseTitle = "Access Denied" };
        }

        var nextHearing = caseEntity.Hearings.FirstOrDefault(h => h.HearingDate > DateTime.UtcNow);
        var lastHearing = caseEntity.Hearings.FirstOrDefault(h => h.HearingDate <= DateTime.UtcNow);
        var clientName = caseEntity.CaseClients.FirstOrDefault()?.Client?.Name;
        var documentTypes = caseEntity.Documents.Select(d => d.MimeType ?? "document").Distinct().ToList();

        var extract = new SmartExtract
        {
            CaseId = caseId,
            CaseTitle = StripPii(caseEntity.Title),
            CaseType = caseEntity.CaseType,
            CourtName = caseEntity.CourtName ?? nextHearing?.CourtName,
            JudgeName = nextHearing?.JudgeName ?? lastHearing?.JudgeName,
            ClientName = clientName != null ? StripPii(clientName) : null,
            Stage = caseEntity.Status.ToString(),
            NextDate = nextHearing?.HearingDate.ToString("dd MMM yyyy"),
            KeyFacts = caseEntity.CaseBackground != null ? StripPii(caseEntity.CaseBackground) : null,
            LastOrderSummary = lastHearing?.Notes != null ? StripPii(lastHearing.Notes) : null,
            DocumentTypes = documentTypes,
            AdditionalInstructions = purpose
        };

        _logger.LogInformation("SmartExtract built for case {CaseId} by user {UserId} for {Purpose}", caseId, userId, purpose);

        return extract;
    }

    private static string StripPii(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = AadhaarRegex.Replace(input, "[AADHAAR REDACTED]");
        result = PanRegex.Replace(result, "[PAN REDACTED]");
        result = PhoneRegex.Replace(result, "[PHONE REDACTED]");

        return result;
    }
}
