using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssuePit.CiCdClient.Services;

/// <summary>
/// Thin wrapper around the Hetzner Cloud REST API.
/// Docs: https://docs.hetzner.cloud/reference/cloud
/// </summary>
public class HetznerCloudService(ILogger<HetznerCloudService> logger, IHttpClientFactory httpClientFactory)
{
    private const string BaseUrl = "https://api.hetzner.cloud/v1";

    private HttpClient CreateClient(string apiToken)
    {
        var client = httpClientFactory.CreateClient("hetzner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // ── Servers ──────────────────────────────────────────────────────────────────

    /// <summary>Lists all servers in the project.</summary>
    public async Task<List<HetznerServerDto>> ListServersAsync(string apiToken, CancellationToken ct = default)
    {
        using var client = CreateClient(apiToken);
        var response = await client.GetAsync($"{BaseUrl}/servers", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var servers = new List<HetznerServerDto>();
        if (json.TryGetProperty("servers", out var arr))
        {
            foreach (var s in arr.EnumerateArray())
                servers.Add(ParseServer(s));
        }
        return servers;
    }

    /// <summary>Creates a new server with the provided cloud-init script and returns the new server.</summary>
    public async Task<(HetznerServerDto Server, string? RootPassword)> CreateServerAsync(
        string apiToken,
        string name,
        string serverType,
        string location,
        string cloudInitScript,
        long? sshKeyId,
        CancellationToken ct = default)
    {
        using var client = CreateClient(apiToken);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["server_type"] = serverType,
            ["location"] = location,
            ["image"] = "ubuntu-24.04",
            ["user_data"] = cloudInitScript,
            ["public_net"] = new Dictionary<string, object>
            {
                // Use only IPv6 to reduce costs (as per requirement).
                ["enable_ipv4"] = false,
                ["enable_ipv6"] = true,
            },
        };

        if (sshKeyId.HasValue)
            payload["ssh_keys"] = new[] { sshKeyId.Value };

        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl}/servers", content, ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Hetzner API error creating server: {response.StatusCode} — {responseBody}");

        var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var server = ParseServer(json.GetProperty("server"));
        var rootPassword = json.TryGetProperty("root_password", out var rp) && rp.ValueKind != JsonValueKind.Null
            ? rp.GetString()
            : null;

        return (server, rootPassword);
    }

    /// <summary>Deletes a server by its Hetzner numeric ID.</summary>
    public async Task DeleteServerAsync(string apiToken, long serverId, CancellationToken ct = default)
    {
        using var client = CreateClient(apiToken);
        var response = await client.DeleteAsync($"{BaseUrl}/servers/{serverId}", ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Hetzner API error deleting server {serverId}: {response.StatusCode} — {body}");
        }
        logger.LogInformation("Deleted Hetzner server {ServerId}", serverId);
    }

    /// <summary>Powers off (hard-stop) a server.</summary>
    public async Task PowerOffServerAsync(string apiToken, long serverId, CancellationToken ct = default)
    {
        await PerformServerActionAsync(apiToken, serverId, "poweroff", ct);
    }

    /// <summary>Reboots a server.</summary>
    public async Task RebootServerAsync(string apiToken, long serverId, CancellationToken ct = default)
    {
        await PerformServerActionAsync(apiToken, serverId, "reboot", ct);
    }

    private async Task PerformServerActionAsync(string apiToken, long serverId, string action, CancellationToken ct)
    {
        using var client = CreateClient(apiToken);
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl}/servers/{serverId}/actions/{action}", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Hetzner API error performing '{action}' on server {serverId}: {response.StatusCode} — {body}");
        }
    }

    // ── SSH Keys ─────────────────────────────────────────────────────────────────

    /// <summary>Lists all SSH keys in the project.</summary>
    public async Task<List<HetznerSshKeyDto>> ListSshKeysAsync(string apiToken, CancellationToken ct = default)
    {
        using var client = CreateClient(apiToken);
        var response = await client.GetAsync($"{BaseUrl}/ssh_keys", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var keys = new List<HetznerSshKeyDto>();
        if (json.TryGetProperty("ssh_keys", out var arr))
        {
            foreach (var k in arr.EnumerateArray())
                keys.Add(new HetznerSshKeyDto(
                    k.GetProperty("id").GetInt64(),
                    k.GetProperty("name").GetString()!,
                    k.GetProperty("fingerprint").GetString()!,
                    k.GetProperty("public_key").GetString()!));
        }
        return keys;
    }

    /// <summary>
    /// Imports a public key into Hetzner Cloud and returns the resulting <see cref="HetznerSshKeyDto"/>.
    /// </summary>
    public async Task<HetznerSshKeyDto> ImportSshKeyAsync(string apiToken, string name, string publicKey, CancellationToken ct = default)
    {
        using var client = CreateClient(apiToken);
        var body = JsonSerializer.Serialize(new { name, public_key = publicKey });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl}/ssh_keys", content, ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Hetzner API error importing SSH key: {response.StatusCode} — {responseBody}");

        var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var k = json.GetProperty("ssh_key");
        return new HetznerSshKeyDto(
            k.GetProperty("id").GetInt64(),
            k.GetProperty("name").GetString()!,
            k.GetProperty("fingerprint").GetString()!,
            k.GetProperty("public_key").GetString()!);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static HetznerServerDto ParseServer(JsonElement s)
    {
        var publicNet = s.TryGetProperty("public_net", out var pn) ? pn : default;
        string? ipv4 = null;
        string? ipv6 = null;

        if (publicNet.ValueKind == JsonValueKind.Object)
        {
            if (publicNet.TryGetProperty("ipv4", out var v4) && v4.ValueKind == JsonValueKind.Object
                && v4.TryGetProperty("ip", out var v4ip))
                ipv4 = v4ip.GetString();

            if (publicNet.TryGetProperty("ipv6", out var v6) && v6.ValueKind == JsonValueKind.Object
                && v6.TryGetProperty("ip", out var v6ip))
                ipv6 = v6ip.GetString();
        }

        var serverType = s.TryGetProperty("server_type", out var st) && st.ValueKind == JsonValueKind.Object
            && st.TryGetProperty("name", out var stName) ? stName.GetString() ?? string.Empty : string.Empty;

        var location = s.TryGetProperty("datacenter", out var dc) && dc.ValueKind == JsonValueKind.Object
            && dc.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object
            && loc.TryGetProperty("name", out var locName) ? locName.GetString() ?? string.Empty : string.Empty;

        var status = s.TryGetProperty("status", out var st2) ? st2.GetString() ?? "unknown" : "unknown";
        var created = s.TryGetProperty("created", out var cr) && cr.ValueKind == JsonValueKind.String
            ? cr.GetDateTime()
            : DateTime.UtcNow;

        return new HetznerServerDto(
            s.GetProperty("id").GetInt64(),
            s.GetProperty("name").GetString()!,
            status,
            serverType,
            location,
            ipv4,
            ipv6,
            created);
    }

    /// <summary>
    /// Generates a fresh RSA 4096-bit key pair and returns (privateKeyPem, publicKeyOpenSsh).
    /// </summary>
    public static (string PrivateKeyPem, string PublicKeyOpenSsh) GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(4096);
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        var publicKey = ConvertRsaPublicKeyToOpenSshFormat(rsa);
        return (privateKeyPem, publicKey);
    }

    private static string ConvertRsaPublicKeyToOpenSshFormat(RSA rsa)
    {
        // OpenSSH public key wire format: length-prefixed key-type, exponent, modulus
        var parameters = rsa.ExportParameters(false);
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        static void WriteBlob(BinaryWriter w, byte[] data)
        {
            // If the high bit is set, prepend 0x00 to ensure positive big-endian integer
            if (data[0] >= 0x80)
            {
                w.Write(new byte[] { 0, 0, 0, (byte)(data.Length + 1) });
                w.Write((byte)0x00);
            }
            else
            {
                w.Write(new byte[] { 0, 0, 0, (byte)data.Length });
            }
            w.Write(data);
        }

        var keyType = "ssh-rsa"u8.ToArray();
        WriteBlob(writer, keyType);
        WriteBlob(writer, parameters.Exponent!);
        WriteBlob(writer, parameters.Modulus!);

        var base64 = Convert.ToBase64String(ms.ToArray());
        return $"ssh-rsa {base64} issuepit-cicd";
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────────

public record HetznerServerDto(
    long Id,
    string Name,
    string Status,
    string ServerType,
    string Location,
    string? Ipv4Address,
    string? Ipv6Address,
    DateTime Created);

public record HetznerSshKeyDto(
    long Id,
    string Name,
    string Fingerprint,
    string PublicKey);
