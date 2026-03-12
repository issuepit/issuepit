using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Pipes;
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

    /// <summary>
    /// When true (default), automatically downloads the ffmpeg binary at startup via
    /// FFMpegCore.Extensions.Downloader if it is not already present.
    /// Set to false to disable automatic download (e.g. when ffmpeg is pre-installed on PATH).
    /// </summary>
    public bool DownloadFfmpeg { get; set; } = true;
}

/// <summary>
/// Transcribes audio files using the Vosk speech recognition library.
/// Uses FFMpegCore to convert any input audio format to the 16 kHz mono 16-bit PCM
/// raw stream that Vosk expects. Falls back to WAV-header-aware parsing when ffmpeg is absent.
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
    /// Transcribes an audio stream and returns the recognised text.
    /// Converts the audio to 16 kHz mono 16-bit PCM via FFMpegCore before feeding it to Vosk.
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
        try
        {
            pcm = await ConvertAudioToPcmAsync(audioStream, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FFMpegCore conversion failed — falling back to WAV-header-skip (requires 16 kHz mono 16-bit PCM input)");
            var ms = new MemoryStream();
            audioStream.Position = 0;
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

                // Accumulate text from all utterances. Vosk's VAD may emit multiple utterance
                // boundaries during silence gaps; AcceptWaveform returns true at each boundary
                // and the text for that utterance must be retrieved via Result(). FinalResult()
                // only covers remaining audio after the last boundary, so ignoring Result()
                // silently discards all recognised text when the audio ends with silence.
                var accumulated = new System.Text.StringBuilder();

                static void AppendText(System.Text.StringBuilder sb, string json)
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("text", out var t))
                    {
                        var s = t.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (sb.Length > 0) sb.Append(' ');
                            sb.Append(s);
                        }
                    }
                }

                var buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = pcm.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    if (rec.AcceptWaveform(buffer, bytesRead))
                        AppendText(accumulated, rec.Result());
                }

                AppendText(accumulated, rec.FinalResult());
                return accumulated.ToString();
            }, ct);
        }
    }

    /// <summary>
    /// Uses FFMpegCore to decode <paramref name="inputStream"/> and output raw 16 kHz / mono / s16le PCM.
    /// The -ar 16000 -ac 1 -f s16le arguments resample to 16 kHz mono raw PCM that Vosk expects.
    /// </summary>
    private async Task<MemoryStream> ConvertAudioToPcmAsync(Stream inputStream, CancellationToken ct)
    {
        var outputBuffer = new MemoryStream();

        // Note: FFMpegCore 5.x ProcessAsynchronously has no CancellationToken overload;
        // cancellation is not propagated into the ffmpeg process itself.
        _ = ct;
        await FFMpegArguments
            .FromPipeInput(new StreamPipeSource(inputStream))
            .OutputToPipe(new StreamPipeSink(outputBuffer), options => options
                .WithAudioSamplingRate(16000)
                .WithCustomArgument("-ac 1")
                .ForceFormat("s16le"))
            .ProcessAsynchronously();

        logger.LogDebug("FFMpegCore converted audio to {Bytes} bytes of raw PCM", outputBuffer.Length);
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
}
