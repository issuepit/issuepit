using System.Text.Json;
using Vosk;

namespace IssuePit.Tests.E2E;

/// <summary>
/// PoC (Proof-of-Concept) unit tests that exercise the Vosk speech-recognition library
/// <strong>directly</strong> — no HTTP, no FFMpegCore, no Aspire stack.
/// These tests are intentionally isolated so they can pinpoint whether a failure is in Vosk
/// itself (model loading, audio format) vs. the API integration layer.
///
/// Prerequisites: set the <c>VoiceTranscription__ModelPath</c> environment variable to the
/// directory of an unpacked Vosk model (e.g. <c>~/.vosk/vosk-model-small-en-us-0.15</c>).
/// The tests are silently skipped when the model is absent so they are safe to run locally
/// without the model pre-installed.
/// </summary>
[Trait("Category", "PoC")]
public class VoskPocTests
{
    private static string? ModelPath =>
        Environment.GetEnvironmentVariable("VoiceTranscription__ModelPath");

    /// <summary>
    /// Verifies that the Vosk model can be loaded from the configured path.
    /// This is the first thing that could go wrong: a missing native library, an
    /// incompatible model version, or a wrong path would surface here.
    /// </summary>
    [Fact]
    public void VoskModel_Loads_WhenModelPathIsConfigured()
    {
        var path = ModelPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        Vosk.Vosk.SetLogLevel(0);
        using var model = new Model(path);
        Assert.NotNull(model);
    }

    /// <summary>
    /// Transcribes <c>Voice_TaskCar.wav</c> directly using Vosk (no API, no ffmpeg).
    /// Expected phrase: "Create a task to call the car mechanic replacing the right door of the car"
    /// Expected keywords: task, car, mechanic, door
    /// </summary>
    [Fact]
    public void VoskRecognizer_TranscribesWav_TaskCar()
    {
        var path = ModelPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        var wavBytes = LoadFixture("Voice_TaskCar.wav");
        var text = Transcribe(path, wavBytes);

        Assert.False(string.IsNullOrWhiteSpace(text),
            $"Expected non-empty transcription for Voice_TaskCar.wav with model '{path}', " +
            $"but got empty string. " +
            "Ensure the WAV is 16-bit PCM mono at 16 kHz and the model is valid.");

        // At least 50 % of keywords must appear in the transcription
        var keywords = new[] { "task", "car", "mechanic", "door" };
        var matched = keywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        Assert.True(matched >= 2,
            $"Expected at least 2/4 keywords [task, car, mechanic, door] in '{text}' but got {matched}.");
    }

    /// <summary>
    /// Transcribes <c>Voice_TicketRefactorTests.wav</c> directly using Vosk.
    /// Expected phrase: "Create a Ticket to refactor the tests to use a Page Object Model approach"
    /// Expected keywords: ticket, refactor, tests, page, object, model
    /// </summary>
    [Fact]
    public void VoskRecognizer_TranscribesWav_TicketRefactorTests()
    {
        var path = ModelPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        var wavBytes = LoadFixture("Voice_TicketRefactorTests.wav");
        var text = Transcribe(path, wavBytes);

        Assert.False(string.IsNullOrWhiteSpace(text),
            $"Expected non-empty transcription for Voice_TicketRefactorTests.wav with model '{path}', " +
            $"but got empty string. " +
            "Ensure the WAV is 16-bit PCM mono at 16 kHz and the model is valid.");

        var keywords = new[] { "ticket", "refactor", "tests", "page", "object" };
        var matched = keywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        Assert.True(matched >= 3,
            $"Expected at least 3/5 keywords [ticket, refactor, tests, page, object] in '{text}' but got {matched}.");
    }

    /// <summary>
    /// Transcribes the given WAV bytes directly using a VoskRecognizer.
    /// Collects both per-utterance Result() values (returned when AcceptWaveform is true)
    /// and the final FinalResult(). Uses the same pattern as VoiceTranscriptionService
    /// so deviations here vs. in the API indicate an integration-layer issue.
    /// </summary>
    private static string Transcribe(string modelPath, byte[] wavBytes)
    {
        Vosk.Vosk.SetLogLevel(0); // 0 = INFO; keeps Vosk logs visible during PoC diagnostic runs
        using var model = new Model(modelPath);
        using var rec = new VoskRecognizer(model, 16000f);
        rec.SetMaxAlternatives(0);
        rec.SetWords(false);

        // Skip the RIFF/WAV header to reach raw PCM bytes.
        int pcmOffset = FindPcmOffset(wavBytes);
        Console.WriteLine($"[PoC] PCM data starts at offset {pcmOffset}, " +
                          $"size {wavBytes.Length - pcmOffset} bytes " +
                          $"({(wavBytes.Length - pcmOffset) / 2.0 / 16000.0:F2}s of audio at 16 kHz)");

        var accumulated = new System.Text.StringBuilder();
        var buffer = new byte[4096];
        int pos = pcmOffset;
        int boundaryCount = 0;

        while (pos < wavBytes.Length)
        {
            int toRead = Math.Min(buffer.Length, wavBytes.Length - pos);
            Buffer.BlockCopy(wavBytes, pos, buffer, 0, toRead);
            pos += toRead;

            if (rec.AcceptWaveform(buffer, toRead))
            {
                var json = rec.Result();
                AppendText(accumulated, json);
                Console.WriteLine($"[PoC] Boundary #{++boundaryCount}: {json}");
            }
        }

        var finalJson = rec.FinalResult();
        AppendText(accumulated, finalJson);
        Console.WriteLine($"[PoC] FinalResult: {finalJson}");

        return accumulated.ToString();
    }

    private static void AppendText(System.Text.StringBuilder sb, string json)
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

    private static int FindPcmOffset(byte[] wav)
    {
        // Walk RIFF/WAV sub-chunks to find the start of the 'data' chunk payload.
        try
        {
            int offset = 12; // skip "RIFF" + size + "WAVE"
            while (offset + 8 <= wav.Length)
            {
                var chunkId = System.Text.Encoding.ASCII.GetString(wav, offset, 4);
                int chunkSize = BitConverter.ToInt32(wav, offset + 4);
                offset += 8;
                if (chunkId == "data") return offset; // now at first PCM sample
                offset += chunkSize;
            }
        }
        catch { /* Parsing failed — offset stays at 0; Vosk will receive the file including the header,
                   which adds minimal noise but does not cause correctness failures for valid WAV files. */ }
        return 0; // could not parse header; feed the whole file (may include header noise)
    }

    private static byte[] LoadFixture(string fileName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFixtures", fileName);
        return File.ReadAllBytes(path);
    }
}
