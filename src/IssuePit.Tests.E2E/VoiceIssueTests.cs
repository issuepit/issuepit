using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the voice-to-issue feature:
/// - API: POST /api/uploads/voice accepts a WAV file, stores it, and returns a transcription.
/// - UI: the Voice button on the issues page opens the recording modal.
/// </summary>
[Collection("E2E")]
[Trait("Category", "Voice")]
public class VoiceIssueTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public VoiceIssueTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    /// <summary>
    /// API test: POST /api/uploads/voice with a minimal WAV file should return 200 with voiceUrl and transcription fields.
    /// The transcription may be empty if no Vosk model is configured, but the response shape must be correct.
    /// </summary>
    [Fact]
    public async Task Api_UploadVoice_ReturnsVoiceUrlAndTranscription()
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"v{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Load the pre-recorded silent WAV fixture (16 kHz, 16-bit PCM, mono, 1 second of silence)
        var wavBytes = LoadVoiceFixture("Voice_Empty.wav");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(wavBytes)
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
        }, "file", "Voice_Empty.wav");

        var response = await client.PostAsync("/api/uploads/voice", content);

        // Read the body first so we can include it in the failure message for easier debugging
        var rawBody = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
            Console.WriteLine($"[Voice Upload] Failed with {response.StatusCode}: {rawBody}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rawBody);
        Assert.True(body.TryGetProperty("voiceUrl", out _), "Response must contain 'voiceUrl'");
        Assert.True(body.TryGetProperty("transcription", out var transcription), "Response must contain 'transcription'");
        // Silence produces no speech — transcription must be empty
        Assert.Equal(string.Empty, transcription.GetString());
    }

    /// <summary>
    /// API test: POST /api/uploads/voice with a real speech recording should return 200 and—when a Vosk
    /// model is configured—a non-empty transcription that matches at least <paramref name="matchThreshold"/>
    /// of the space-separated <paramref name="expectedKeywords"/>.
    /// When no model is available the transcription is allowed to be empty (best-effort).
    /// </summary>
    [Theory]
    // Voice_TaskCar.wav — "Create a task to call the car mechanic replacing the right door of the car"
    [InlineData("Voice_TaskCar.wav", "task car mechanic door", 0.5)]
    // Voice_TicketRefactorTests.wav — "Create a Ticket to refactor the tests to use a Page Object Model approach"
    [InlineData("Voice_TicketRefactorTests.wav", "ticket refactor tests page object model", 0.5)]
    public async Task Api_UploadVoice_WithSpeechRecording_ReturnsVoiceUrlAndOptionalTranscription(
        string fixture, string expectedKeywords, double matchThreshold)
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"v{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var wavBytes = LoadVoiceFixture(fixture);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(wavBytes)
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
        }, "file", fixture);

        var response = await client.PostAsync("/api/uploads/voice", content);

        var rawBody = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
            Console.WriteLine($"[Voice Upload] Failed with {response.StatusCode}: {rawBody}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rawBody);
        Assert.True(body.TryGetProperty("voiceUrl", out _), "Response must contain 'voiceUrl'");
        Assert.True(body.TryGetProperty("transcription", out var transcription), "Response must contain 'transcription'");

        // When Vosk is configured the transcription must match at least matchThreshold of the expected keywords.
        // When no model is available, the service returns empty string (best-effort), which is also acceptable.
        var text = transcription.GetString() ?? string.Empty;

        // Capture the optional warning from the API — it explains WHY transcription is empty:
        //   "Voice transcription model is not configured on this server." → model path wrong / not passed to API
        //   "No speech detected in the recording."                        → model loaded but Vosk returned empty
        var warning = body.TryGetProperty("transcriptionWarning", out var w) ? w.GetString() : null;

        // If a model path is explicitly set in the environment (e.g. CI), transcription must be non-empty
        var modelPath = Environment.GetEnvironmentVariable("VoiceTranscription__ModelPath");
        if (!string.IsNullOrWhiteSpace(modelPath) && Directory.Exists(modelPath))
        {
            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Vosk model is present at '{modelPath}' but transcription was empty for '{fixture}'. " +
                $"API warning: '{warning ?? "(none)"}'. " +
                "Possible causes: (1) API did not receive VoiceTranscription__ModelPath env var — " +
                "check AppHost configuration; (2) FFmpeg conversion failed and fallback WAV parsing " +
                "produced wrong PCM; (3) Vosk model failed to load (check API startup logs).");
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var keywords = expectedKeywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matchedCount = keywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
            var matchRatio = (double)matchedCount / keywords.Length;
            Assert.True(
                matchRatio >= matchThreshold,
                $"Expected at least {matchThreshold:P0} of keywords [{expectedKeywords}] in transcription '{text}', " +
                $"but only {matchedCount}/{keywords.Length} matched ({matchRatio:P0}).");
        }
    }

    /// <summary>
    /// API test: POST /api/uploads/voice without auth must return 401.
    /// </summary>
    [Fact]
    public async Task Api_UploadVoice_WithoutAuth_Returns401()
    {
        using var client = new HttpClient { BaseAddress = _fixture.ApiClient!.BaseAddress };
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var wavBytes = BuildSilentWav(sampleRate: 16000, durationSeconds: 1);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(wavBytes)
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
        }, "file", "test.wav");

        var response = await client.PostAsync("/api/uploads/voice", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// UI test: the Voice button is visible on the issues page and clicking it opens the recording modal;
    /// the modal can be closed via Cancel.
    /// </summary>
    [Fact]
    public async Task Ui_VoiceButton_OpensAndClosesModal()
    {
        if (FrontendUrl is null) return; // skip if frontend is not running

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = FrontendUrl,
            Permissions = ["microphone"],
        });
        var page = await context.NewPageAsync();

        try
        {
            var username = $"v{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";

            // 1. Register via the UI
            await new LoginPage(page).RegisterAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            // 2. Create an org and project via the API using the same credentials
            var tenantId = await GetDefaultTenantIdAsync();
            using var apiClient = CreateCookieClient();
            apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            await apiClient.PostAsJsonAsync("/api/auth/login", new { username, password });

            var orgSlug = $"v-org-{Guid.NewGuid():N}"[..16];
            var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Voice Org", slug = orgSlug });
            Assert.Equal(System.Net.HttpStatusCode.Created, orgResp.StatusCode);
            var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var orgId = org.GetProperty("id").GetString()!;

            var projectSlug = $"v-proj-{Guid.NewGuid():N}"[..16];
            var projResp = await apiClient.PostAsJsonAsync("/api/projects",
                new { name = "Voice Project", slug = projectSlug, orgId });
            Assert.Equal(System.Net.HttpStatusCode.Created, projResp.StatusCode);
            var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var projectId = project.GetProperty("id").GetString()!;

            // 3. Navigate to the project's issues page and verify the Voice button opens the modal
            var issuesPage = new IssuesPage(page);
            await issuesPage.GotoAsync(projectId);
            await issuesPage.OpenVoiceModalAsync();
            await issuesPage.CloseVoiceModalAsync();
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI test: complete voice-to-issue creation flow.
    /// Mocks getUserMedia to supply audio via an oscillator so that the ScriptProcessorNode
    /// receives audio data and stopRecording returns a valid WAV blob.
    /// Routes the /api/uploads/voice endpoint to return a deterministic transcription, then
    /// verifies that clicking "Create Issue" creates and lists the new voice issue.
    /// </summary>
    [Fact]
    public async Task Ui_VoiceRecording_CreatesIssue()
    {
        if (FrontendUrl is null) return; // skip if frontend is not running

        // Minimum recording duration to allow the ScriptProcessorNode's onaudioprocess to fire
        // at least once (4096 samples / 16000 Hz ≈ 256 ms per callback; 700 ms gives ~2 callbacks).
        const int MinRecordingDurationMs = 700;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = FrontendUrl,
            Permissions = ["microphone"],
        });
        var page = await context.NewPageAsync();

        // Mock navigator.mediaDevices.getUserMedia to return a stream driven by an oscillator
        // so that the ScriptProcessorNode's onaudioprocess fires and audio chunks accumulate.
        await page.AddInitScriptAsync("""
            if (navigator.mediaDevices) {
                Object.defineProperty(navigator.mediaDevices, 'getUserMedia', {
                    configurable: true,
                    value: async function(constraints) {
                        if (constraints && constraints.audio) {
                            const sampleRate = (typeof constraints.audio === 'object' && constraints.audio.sampleRate) || 16000;
                            const ctx = new AudioContext({ sampleRate: sampleRate });
                            try { if (ctx.state === 'suspended') await ctx.resume(); } catch {}
                            const osc = ctx.createOscillator();
                            osc.type = 'sine';
                            osc.frequency.value = 440;
                            const dest = ctx.createMediaStreamDestination();
                            osc.connect(dest);
                            osc.start();
                            window.__voiceTestCtx = ctx;
                            return dest.stream;
                        }
                        throw new Error('Test mock: only audio getUserMedia is supported');
                    }
                });
            }
        """);

        // Intercept the voice upload API to return a deterministic transcription without
        // requiring a real Vosk model in the test environment.
        const string fakeTranscription = "voice issue from recording";
        await page.RouteAsync("**/api/uploads/voice", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = $$"""{"voiceUrl":"https://cdn.example.com/test.wav","transcription":"{{fakeTranscription}}"}"""
            });
        });

        try
        {
            var username = $"v{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";

            // 1. Register via the UI
            await new LoginPage(page).RegisterAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            // 2. Create org and project via the API using the same session
            var tenantId = await GetDefaultTenantIdAsync();
            using var apiClient = CreateCookieClient();
            apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            await apiClient.PostAsJsonAsync("/api/auth/login", new { username, password });

            var orgSlug = $"v-org-{Guid.NewGuid():N}"[..16];
            var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Voice Org 2", slug = orgSlug });
            Assert.Equal(System.Net.HttpStatusCode.Created, orgResp.StatusCode);
            var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var orgId = org.GetProperty("id").GetString()!;

            var projectSlug = $"v-proj-{Guid.NewGuid():N}"[..16];
            var projResp = await apiClient.PostAsJsonAsync("/api/projects",
                new { name = "Voice Project 2", slug = projectSlug, orgId });
            Assert.Equal(System.Net.HttpStatusCode.Created, projResp.StatusCode);
            var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var projectId = project.GetProperty("id").GetString()!;

            // 3. Record voice, intercept upload, create issue from the returned transcription
            var issuesPage = new IssuesPage(page);
            await issuesPage.GotoAsync(projectId);
            await issuesPage.OpenVoiceModalAsync();
            await issuesPage.StartVoiceRecordingAsync();
            await Task.Delay(MinRecordingDurationMs); // Allow at least one audio chunk to be captured
            await issuesPage.StopVoiceRecordingAsync();
            await issuesPage.WaitForVoiceTranscriptionAsync(fakeTranscription);
            await issuesPage.SubmitVoiceCreateAsync();

            // 4. Verify the voice issue now appears in the issues list
            await page.WaitForSelectorAsync("text=Voice Issue", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // --- Helpers ---

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }

    /// <summary>
    /// Loads a WAV fixture file by name from the test output directory.
    /// Available fixtures are documented in <c>TestFixtures/voice.MD</c>.
    /// </summary>
    private static byte[] LoadVoiceFixture(string fileName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFixtures", fileName);
        return File.ReadAllBytes(path);
    }

    /// <summary>Builds a minimal PCM 16-bit WAV file filled with silence.</summary>
    private static byte[] BuildSilentWav(int sampleRate, int durationSeconds)
    {
        int numSamples = sampleRate * durationSeconds;
        int dataLen = numSamples * 2; // 16-bit = 2 bytes per sample
        var buffer = new byte[44 + dataLen];

        void WriteString(int offset, string s)
        {
            for (int i = 0; i < s.Length; i++) buffer[offset + i] = (byte)s[i];
        }

        void WriteInt32(int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        void WriteInt16(int offset, short value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        WriteString(0, "RIFF");
        WriteInt32(4, 36 + dataLen);
        WriteString(8, "WAVE");
        WriteString(12, "fmt ");
        WriteInt32(16, 16);                    // fmt chunk size
        WriteInt16(20, 1);                     // PCM
        WriteInt16(22, 1);                     // mono
        WriteInt32(24, sampleRate);
        WriteInt32(28, sampleRate * 2);        // byte rate
        WriteInt16(32, 2);                     // block align
        WriteInt16(34, 16);                    // bits per sample
        WriteString(36, "data");
        WriteInt32(40, dataLen);
        // Remaining bytes are already zeroed (silence)

        return buffer;
    }
}
