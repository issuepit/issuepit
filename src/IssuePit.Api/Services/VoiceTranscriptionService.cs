using System.Text.Json;
using Microsoft.Extensions.Options;
using Vosk;

namespace IssuePit.Api.Services;

public class VoiceTranscriptionOptions
{
    public const string SectionName = "VoiceTranscription";

    /// <summary>
    /// Path to the Vosk model directory.
    /// Download a model from https://alphacephei.com/vosk/models and unpack it here.
    /// If empty or the directory does not exist, transcription is skipped and an empty string is returned.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>Expected audio sample rate in Hz. Must match the WAV file recorded by the browser.</summary>
    public float SampleRate { get; set; } = 16000f;
}

/// <summary>
/// Transcribes WAV audio files using the Vosk speech recognition library.
/// Requires a Vosk model directory configured via <see cref="VoiceTranscriptionOptions.ModelPath"/>.
/// When the model is not available the service returns an empty string instead of throwing.
/// </summary>
public class VoiceTranscriptionService(IOptions<VoiceTranscriptionOptions> options, ILogger<VoiceTranscriptionService> logger)
{
    private readonly VoiceTranscriptionOptions _opts = options.Value;
    private Model? _model;
    private readonly Lock _modelLock = new();

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_opts.ModelPath) && Directory.Exists(_opts.ModelPath);

    private Model? GetOrLoadModel()
    {
        if (!IsAvailable) return null;
        if (_model is not null) return _model;

        lock (_modelLock)
        {
            if (_model is not null) return _model;
            try
            {
                Vosk.Vosk.SetLogLevel(-1);
                _model = new Model(_opts.ModelPath!);
                logger.LogInformation("Loaded Vosk model from '{ModelPath}'", _opts.ModelPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load Vosk model from '{ModelPath}' — transcription will be skipped", _opts.ModelPath);
                return null;
            }
        }

        return _model;
    }

    /// <summary>
    /// Transcribes a WAV audio stream and returns the recognised text.
    /// Returns an empty string if the Vosk model is not configured or unavailable.
    /// </summary>
    public async Task<string> TranscribeAsync(Stream wavStream, CancellationToken ct = default)
    {
        var model = GetOrLoadModel();
        if (model is null)
        {
            logger.LogWarning("Vosk model not available — skipping transcription");
            return string.Empty;
        }

        // Copy the stream to a buffer so it can be read on a thread-pool thread
        using var ms = new MemoryStream();
        await wavStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var rec = new VoskRecognizer(model, _opts.SampleRate);
            rec.SetMaxAlternatives(0);
            rec.SetWords(false);

            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = ms.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                rec.AcceptWaveform(buffer, bytesRead);
            }

            var resultJson = rec.FinalResult();
            using var doc = JsonDocument.Parse(resultJson);
            return doc.RootElement.TryGetProperty("text", out var text)
                ? text.GetString() ?? string.Empty
                : string.Empty;
        }, ct);
    }
}
