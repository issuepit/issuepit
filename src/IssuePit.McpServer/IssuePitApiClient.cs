using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.McpServer;

/// <summary>
/// Typed HTTP client for calling the IssuePit API.
/// The tenant is forwarded via the X-Tenant-Id header configured at startup.
/// </summary>
public class IssuePitApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    public async Task<T?> PutAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(path, body, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(path, ct);
        response.EnsureSuccessStatusCode();
    }
}
