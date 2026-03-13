using IssuePit.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController(ImageStorageService imageStorage, VoiceTranscriptionService voiceTranscription, TenantContext tenantContext, ILogger<UploadsController> logger) : ControllerBase
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];
    private static readonly string[] AllowedVoiceContentTypes = ["audio/wav", "audio/wave", "audio/x-wav"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxVoiceFileSizeBytes = 25 * 1024 * 1024; // 25 MB
    private const long MaxGeneralFileSizeBytes = 50 * 1024 * 1024; // 50 MB

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
        if (tenantContext.CurrentTenant is null || tenantContext.CurrentUser is null) return Unauthorized();

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
        string voiceUrl;
        try
        {
            voiceUrl = await imageStorage.UploadFileAsync(ms, file.FileName, file.ContentType, "voice", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice file upload to S3 failed");
            var msg = ex.InnerException is null ? ex.Message : $"{ex.Message} | {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            return StatusCode(500, new { error = ex.GetType().Name, message = msg });
        }

        // Transcribe — best-effort; a failure here must not prevent the upload from succeeding
        var transcription = string.Empty;
        string? transcriptionWarning = null;
        try
        {
            ms.Position = 0;
            transcription = await voiceTranscription.TranscribeAsync(ms, ct);
            if (string.IsNullOrEmpty(transcription))
                transcriptionWarning = voiceTranscription.IsAvailable
                    ? "No speech detected in the recording."
                    : "Voice transcription model is not configured on this server.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Voice transcription failed; returning empty transcription");
            transcriptionWarning = ex.Message;
        }

        return Ok(new { voiceUrl, transcription, transcriptionWarning });
    }

    /// <summary>
    /// Accepts any file and stores it in S3/B2, returning the public URL.
    /// </summary>
    [HttpPost("file")]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        if (tenantContext.CurrentTenant is null) return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxGeneralFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 50 MB size limit." });

        await using var stream = file.OpenReadStream();
        string url;
        try
        {
            url = await imageStorage.UploadFileAsync(stream, file.FileName, file.ContentType, "attachments", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File upload to S3 failed");
            var msg = ex.InnerException is null ? ex.Message : $"{ex.Message} | {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            return StatusCode(500, new { error = ex.GetType().Name, message = msg });
        }

        return Ok(new { url });
    }
}
