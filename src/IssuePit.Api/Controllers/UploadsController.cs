using IssuePit.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController(ImageStorageService imageStorage, TenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (tenantContext.CurrentTenant is null) return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 10 MB size limit." });

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only JPEG, PNG, GIF and WebP images are allowed." });

        var fileName = Path.GetFileName(file.FileName);
        await using var stream = file.OpenReadStream();
        var url = await imageStorage.UploadImageAsync(stream, fileName, file.ContentType, ct);

        return Ok(new { url });
    }
}
