using LexCore.Application.DTOs;
using LexCore.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "Lawyer")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("generate-client-update")]
    public async Task<ActionResult<ApiResponse<string>>> GenerateClientUpdate(
        [FromBody] GenerateClientUpdateRequest request)
    {
        var result = await _aiService.GenerateClientUpdateAsync(
            request.HearingNote,
            request.CaseTitle,
            request.ClientName);

        return Ok(ApiResponse<string>.SuccessResponse(result));
    }

    [HttpPost("analyze-document")]
    public async Task<ActionResult<ApiResponse<string>>> AnalyzeDocument(
        [FromBody] AnalyzeDocumentRequest request)
    {
        var result = await _aiService.AnalyzeDocumentAsync(request.DocumentText);
        return Ok(ApiResponse<string>.SuccessResponse(result));
    }

    [HttpPost("draft-document")]
    public async Task<ActionResult<ApiResponse<string>>> DraftDocument(
        [FromBody] DraftDocumentRequest request)
    {
        var result = await _aiService.DraftDocumentAsync(
            request.CaseDetails,
            request.DocumentType,
            request.Instructions);

        return Ok(ApiResponse<string>.SuccessResponse(result));
    }
}

// Request DTOs
public record GenerateClientUpdateRequest(
    string HearingNote,
    string CaseTitle,
    string ClientName);

public record AnalyzeDocumentRequest(string DocumentText);

public record DraftDocumentRequest(
    string CaseDetails,
    string DocumentType,
    string Instructions);