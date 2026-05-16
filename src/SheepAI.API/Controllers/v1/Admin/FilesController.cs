using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Responses.Admin;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Domain.Exceptions;

namespace SheepAI.API.Controllers.v1.Admin;

/// <summary>Admin endpoints for managing city PDF documents.</summary>
[ApiController]
[Authorize]
[Route("api/admin/files")]
public sealed class FilesController(IFileService fileService) : ControllerBase
{
    /// <summary>Returns all uploaded city documents.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<List<FileResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var files = await fileService.GetAllAsync(ct);
        return Ok(Api.Data("Files retrieved.", files));
    }

    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf", "application/pdf" },
        { ".txt", "text/plain" },
        { ".md",  "text/plain" },
    };

    /// <summary>
    /// Uploads a document to the Anthropic Files API and records it in the database.
    /// Accepted formats: PDF, TXT, MD.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<FileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string name, [FromForm] string? category, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("A non-empty file is required.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.TryGetValue(ext, out var contentType))
            throw new ValidationException("Unsupported file type. Allowed: .pdf, .txt, .md");

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Display name is required.");

        await using var stream = file.OpenReadStream();
        var result = await fileService.UploadAsync(stream, file.FileName, contentType, name, category ?? "General", file.Length, ct);
        return Ok(Api.Data("File uploaded.", result));
    }

    /// <summary>Deletes a city document from the Anthropic Files API and the database.</summary>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken ct)
    {
        await fileService.DeleteAsync(fileId, ct);
        return NoContent();
    }
}
