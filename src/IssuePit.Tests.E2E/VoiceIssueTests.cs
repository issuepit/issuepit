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
[Trait("Category", "E2E")]
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

        // Build a minimal valid WAV file (PCM 16-bit, 16 kHz, mono, 1 second of silence)
        var wavBytes = BuildSilentWav(sampleRate: 16000, durationSeconds: 1);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(wavBytes)
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
        }, "file", "test.wav");

        var response = await client.PostAsync("/api/uploads/voice", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.TryGetProperty("voiceUrl", out _), "Response must contain 'voiceUrl'");
        Assert.True(body.TryGetProperty("transcription", out _), "Response must contain 'transcription'");
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
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

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
