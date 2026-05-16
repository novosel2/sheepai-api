using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Requests.Documents;
using SheepAI.Application.DTOs.Responses.AI;
using SheepAI.Application.DTOs.Responses.Documents;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Domain.Exceptions;

namespace SheepAI.API.Controllers.v1.Documents;

/// <summary>Manages PDF documents via the Anthropic Files API.</summary>
[ApiController]
[Route("api/v1/documents")]
// [Authorize]
public sealed class DocumentsController(IClaudeService claudeService, ILogger<DocumentsController> logger) : ControllerBase
{
    /// <summary>Uploads a PDF file to the Anthropic Files API and returns a stable file ID.</summary>
    /// <remarks>The returned <c>file_id</c> can be passed to the analyze endpoint multiple times without re-uploading the file.</remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<UploadDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("A non-empty PDF file is required.");

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Only PDF files are supported.");

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Upload request: {FileName} ({Size} bytes)", file.FileName, file.Length);

        await using var stream = file.OpenReadStream();
        var result = await claudeService.UploadDocumentAsync(stream, file.FileName, file.Length, cancellationToken);

        var dto = new UploadDocumentResponse(result.FileId, result.FileName, result.SizeBytes);
        return Ok(Api.Data("Document uploaded.", dto));
    }

    /// <summary>Analyzes a previously uploaded document using a prompt.</summary>
    [HttpPost("{fileId}/analyze")]
    [ProducesResponseType<ApiResponse<PromptResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Analyze(string fileId, [FromBody] AnalyzeDocumentRequest request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Analyze request for file {FileId}", fileId);

        var result = await claudeService.AnalyzeDocumentAsync(fileId, request.Prompt, cancellationToken);
        var dto = new PromptResponse(result.Text, result.InputTokens, result.OutputTokens);
        return Ok(Api.Data("Document analyzed.", dto));
    }

    /// <summary>Deletes a document from the Anthropic Files API.</summary>
    [HttpDelete("{fileId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string fileId, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Delete request for file {FileId}", fileId);

        await claudeService.DeleteDocumentAsync(fileId, cancellationToken);
        return NoContent();
    }

    private ILogger<DocumentsController> _logger => logger;
}
