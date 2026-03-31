using IssuePit.Notes.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IssuePit.Notes.Api.Controllers;

[ApiController]
[Route("api/notes/uploads")]
public class UploadsController(NotesTenantContext ctx, NotesImageStorageService storage) : ControllerBase
{
    private static readonly HashSet<string> AllowedImageTypes =
    [
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    ];

    private const long MaxImageSize = 10 * 1024 * 1024; // 10 MB

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (ctx.TenantId is null) return Unauthorized();

        if (!storage.IsConfigured)
            return StatusCode(503, new UploadErrorResponse("Image storage is not configured."));

        if (file.Length == 0)
            return BadRequest(new UploadErrorResponse("File is empty."));

        if (file.Length > MaxImageSize)
            return BadRequest(new UploadErrorResponse($"File exceeds maximum size of {MaxImageSize / 1024 / 1024} MB."));

        if (!AllowedImageTypes.Contains(file.ContentType))
            return BadRequest(new UploadErrorResponse("Unsupported image file type. Allowed: JPEG, PNG, GIF, WebP, SVG."));

        await using var stream = file.OpenReadStream();
        var url = await storage.UploadImageAsync(stream, file.FileName, file.ContentType, ct);
        return Ok(new UploadResponse(url));
    }
}

public record UploadResponse(string Url);
public record UploadErrorResponse(string Error);
