using IssuePit.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController(ImageStorageService imageStorage, VoiceTranscriptionService voiceTranscription, TenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];
    private static readonly string[] AllowedVoiceContentTypes = ["audio/wav", "audio/wave", "audio/x-wav"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxVoiceFileSizeBytes = 25 * 1024 * 1024; // 25 MB

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

        await using var stream = file.OpenReadStream();
        var url = await imageStorage.UploadImageAsync(stream, file.FileName, file.ContentType, ct);

        return Ok(new { url });
    }

    /// <summary>
    /// Accepts a WAV audio recording, stores it in S3/B2, transcribes it using Vosk,
    /// and returns the storage URL together with the recognised text.
    /// </summary>
    [HttpPost("voice")]
    public async Task<IActionResult> UploadVoice(IFormFile file, CancellationToken ct)
    {
        if (tenantContext.CurrentTenant is null) return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxVoiceFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 25 MB size limit." });

        if (!AllowedVoiceContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only WAV audio files are allowed." });

        // Buffer the file so it can be read twice (upload + transcription)
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Upload to S3/B2
        var voiceUrl = await imageStorage.UploadFileAsync(ms, file.FileName, file.ContentType, "voice", ct);

        // Transcribe
        ms.Position = 0;
        var transcription = await voiceTranscription.TranscribeAsync(ms, ct);

        return Ok(new { voiceUrl, transcription });
    }
}
