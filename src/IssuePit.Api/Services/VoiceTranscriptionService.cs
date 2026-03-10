using System.Diagnostics;
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

    /// <summary>Expected audio sample rate in Hz. Vosk requires 16000 Hz.</summary>
    public float SampleRate { get; set; } = 16000f;
}

/// <summary>
/// Transcribes audio files using the Vosk speech recognition library.
/// Uses ffmpeg (when available) to convert any input audio format to the 16 kHz mono 16-bit PCM
/// raw stream that Vosk expects. Falls back to WAV-header-aware parsing when ffmpeg is absent.
/// Requires a Vosk model directory configured via <see cref="VoiceTranscriptionOptions.ModelPath"/>.
/// When the model is not available the service returns an empty string instead of throwing.
/// </summary>
public class VoiceTranscriptionService(IOptions<VoiceTranscriptionOptions> options, ILogger<VoiceTranscriptionService> logger)
{
    private readonly VoiceTranscriptionOptions _opts = options.Value;
    private Model? _model;
    private readonly Lock _modelLock = new();

    // Locate ffmpeg once at startup — null means it is not installed.
    private static readonly string? FfmpegPath = FindFfmpegPath();

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
    /// Transcribes an audio stream and returns the recognised text.
    /// Converts the audio to 16 kHz mono 16-bit PCM via ffmpeg before feeding it to Vosk.
    /// Returns an empty string if the Vosk model is not configured or unavailable.
    /// </summary>
    public async Task<string> TranscribeAsync(Stream audioStream, CancellationToken ct = default)
    {
        var model = GetOrLoadModel();
        if (model is null)
        {
            logger.LogWarning("Vosk model not available — skipping transcription");
            return string.Empty;
        }

        // Obtain raw 16 kHz / 1-ch / s16le PCM bytes for Vosk.
        MemoryStream pcm;
        if (FfmpegPath is not null)
        {
            pcm = await ConvertAudioToPcmAsync(audioStream, ct);
        }
        else
        {
            logger.LogWarning("ffmpeg not found — falling back to WAV-header-skip (requires 16 kHz mono 16-bit PCM input)");
            var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            pcm = SkipWavDataChunkHeader(ms);
        }

        using (pcm)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var rec = new VoskRecognizer(model, _opts.SampleRate);
                rec.SetMaxAlternatives(0);
                rec.SetWords(false);

                var buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = pcm.Read(buffer, 0, buffer.Length)) > 0)
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

    /// <summary>
    /// Runs ffmpeg to decode <paramref name="inputStream"/> and output raw 16 kHz / mono / s16le PCM.
    /// Reads stdin and stdout concurrently to avoid pipe deadlocks.
    /// </summary>
    private async Task<MemoryStream> ConvertAudioToPcmAsync(Stream inputStream, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(FfmpegPath!)
        {
            // -v quiet  — suppress progress noise
            // -i pipe:0 — read from stdin
            // -ar 16000 — resample to 16 kHz
            // -ac 1     — downmix to mono
            // -f s16le  — output format: raw signed 16-bit little-endian PCM (no WAV header)
            // pipe:1    — write to stdout
            Arguments = "-v quiet -i pipe:0 -ar 16000 -ac 1 -f s16le pipe:1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg process");

        var outputBuffer = new MemoryStream();

        // Write stdin, read stdout and stderr concurrently to prevent deadlocks.
        var writeTask = Task.Run(async () =>
        {
            try { await inputStream.CopyToAsync(process.StandardInput.BaseStream, ct); }
            finally { process.StandardInput.Close(); }
        }, ct);

        var readTask = process.StandardOutput.BaseStream.CopyToAsync(outputBuffer, ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(writeTask, readTask, stderrTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg exited with code {process.ExitCode}: {stderrTask.Result.Trim()}");

        logger.LogDebug("ffmpeg converted audio to {Bytes} bytes of raw PCM", outputBuffer.Length);
        outputBuffer.Position = 0;
        return outputBuffer;
    }

    /// <summary>
    /// Skips the RIFF/WAV header so the returned stream starts at the first raw PCM sample byte.
    /// Falls back to the start of the stream if the header cannot be parsed.
    /// </summary>
    private static MemoryStream SkipWavDataChunkHeader(MemoryStream ms)
    {
        ms.Position = 0;
        try
        {
            using var br = new BinaryReader(ms, System.Text.Encoding.ASCII, leaveOpen: true);

            // RIFF chunk: "RIFF" + 4-byte size + "WAVE"
            var riff = new string(br.ReadChars(4));
            if (riff != "RIFF") { ms.Position = 0; return ms; }
            br.ReadInt32(); // chunk size
            var wave = new string(br.ReadChars(4));
            if (wave != "WAVE") { ms.Position = 0; return ms; }

            // Walk sub-chunks until we find "data"
            while (ms.Position + 8 <= ms.Length)
            {
                var chunkId = new string(br.ReadChars(4));
                var chunkSize = br.ReadInt32();
                if (chunkId == "data") break;        // stream is now at data payload
                ms.Position += chunkSize;             // skip over unknown/fmt chunks
            }
        }
        catch
        {
            ms.Position = 0;
        }

        return ms;
    }

    private static string? FindFfmpegPath()
    {
        var candidates = new[] { "ffmpeg", "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg" };
        foreach (var candidate in candidates)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(candidate)
                {
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (p is null) continue;
                p.WaitForExit(2000);
                if (p.ExitCode == 0) return candidate;
            }
            catch { /* binary not found or not executable */ }
        }
        return null;
    }
}
