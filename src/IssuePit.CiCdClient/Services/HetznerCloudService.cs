using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssuePit.CiCdClient.Services;

/// <summary>
/// Thin HTTP client wrapper for the Hetzner Cloud API v1.
/// See https://docs.hetzner.cloud/ for the full API reference.
///
/// Configuration keys (env var format → config key):
/// <list type="bullet">
///   <item><c>Hetzner__ApiToken</c> — Hetzner Cloud API token (global / dev fallback; org-level token takes precedence).</item>
///   <item><c>Hetzner__DefaultServerType</c> — Default server type, e.g. "cx22" (default: "cx22").</item>
///   <item><c>Hetzner__DefaultLocation</c> — Default datacenter location, e.g. "nbg1" (default: "nbg1").</item>
///   <item><c>Hetzner__DefaultImage</c> — OS image to use, e.g. "ubuntu-24.04" (default: "ubuntu-24.04").</item>
///   <item><c>Hetzner__SshKeyName</c> — Name for the generated SSH key in the Hetzner project (default: "issuepit-cicd").</item>
/// </list>
/// </summary>
public class HetznerCloudService(IConfiguration configuration, ILogger<HetznerCloudService> logger)
{
    private const string BaseUrl = "https://api.hetzner.cloud/v1";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string DefaultServerType => configuration["Hetzner:DefaultServerType"] ?? "cx22";
    public string DefaultLocation => configuration["Hetzner:DefaultLocation"] ?? "nbg1";
    public string DefaultImage => configuration["Hetzner:DefaultImage"] ?? "ubuntu-24.04";
    private string SshKeyName => configuration["Hetzner:SshKeyName"] ?? "issuepit-cicd";

    private HttpClient BuildClient(string apiToken) => new()
    {
        DefaultRequestHeaders =
        {
            Authorization = new AuthenticationHeaderValue("Bearer", apiToken),
            Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
        }
    };

    // ─── SSH Keys ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a new RSA-4096 SSH key pair. Returns (privateKeyPem, publicKeyOpenSsh).
    /// The public key is in the format expected by Hetzner's SSH key upload API.
    /// </summary>
    public static (string PrivateKeyPem, string PublicKeyOpenSsh) GenerateSshKeyPair()
    {
        using var rsa = RSA.Create(4096);
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();

        // Build OpenSSH public key format: "ssh-rsa <base64> issuepit-cicd"
        var publicKeyOpenSsh = BuildOpenSshPublicKey(rsa);
        return (privateKeyPem, publicKeyOpenSsh);
    }

    private static string BuildOpenSshPublicKey(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);

        // OpenSSH encoding: length-prefixed fields (big-endian), base64-encoded, prefixed with "ssh-rsa "
        using var ms = new MemoryStream();
        WriteOpenSshMpint(ms, "ssh-rsa"u8.ToArray(), isString: true);
        WriteOpenSshMpint(ms, parameters.Exponent!);
        WriteOpenSshMpint(ms, parameters.Modulus!);

        return "ssh-rsa " + Convert.ToBase64String(ms.ToArray()) + " issuepit-cicd";
    }

    private static void WriteOpenSshMpint(MemoryStream ms, byte[] data, bool isString = false)
    {
        if (!isString && (data[0] & 0x80) != 0)
        {
            // Prepend 0x00 to ensure positive interpretation
            var padded = new byte[data.Length + 1];
            data.CopyTo(padded, 1);
            data = padded;
        }
        var lenBytes = BitConverter.GetBytes(data.Length);
        if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
        ms.Write(lenBytes);
        ms.Write(data);
    }

    /// <summary>Lists all SSH keys in the Hetzner project. Returns (id, name, fingerprint) tuples.</summary>
    public async Task<IReadOnlyList<HetznerSshKeyInfo>> ListSshKeysAsync(string apiToken, CancellationToken ct)
    {
        using var client = BuildClient(apiToken);
        var resp = await client.GetAsync($"{BaseUrl}/ssh_keys", ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<HetznerSshKeysResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Null response from Hetzner SSH keys API");
        return json.SshKeys ?? [];
    }

    /// <summary>Creates a new SSH key in the Hetzner project. Returns the created key ID.</summary>
    public async Task<long> CreateSshKeyAsync(string apiToken, string name, string publicKey, CancellationToken ct)
    {
        using var client = BuildClient(apiToken);
        var payload = new { name, public_key = publicKey };
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{BaseUrl}/ssh_keys", body, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<HetznerSshKeyResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Null response from Hetzner create SSH key API");
        return json.SshKey?.Id ?? throw new InvalidOperationException("No SSH key ID in response");
    }

    /// <summary>
    /// Ensures an SSH key exists for IssuePit in the Hetzner project.
    /// Creates a new key pair if not already present. Returns (keyId, privateKeyPem).
    /// When a key with the given name already exists, returns its ID with an empty private key
    /// (cannot be retrieved from Hetzner after creation).
    /// </summary>
    public async Task<(long KeyId, string PrivateKeyPem)> EnsureSshKeyAsync(string apiToken, CancellationToken ct)
    {
        var existing = await ListSshKeysAsync(apiToken, ct);
        var match = existing.FirstOrDefault(k => k.Name == SshKeyName);
        if (match != null)
        {
            logger.LogDebug("Reusing existing Hetzner SSH key '{Name}' (id={Id})", match.Name, match.Id);
            return (match.Id, string.Empty);
        }

        logger.LogInformation("Generating new SSH key pair for Hetzner project (name='{Name}')", SshKeyName);
        var (privateKey, publicKey) = GenerateSshKeyPair();
        var keyId = await CreateSshKeyAsync(apiToken, SshKeyName, publicKey, ct);
        logger.LogInformation("Uploaded SSH key to Hetzner (id={Id})", keyId);
        return (keyId, privateKey);
    }

    // ─── Servers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new Hetzner Cloud server. Returns the created server details.
    /// Uses IPv6-only networking by default (cheaper; <c>public_net.enable_ipv4 = false</c>).
    /// The <paramref name="cloudInitScript"/> is passed as <c>user_data</c>.
    /// </summary>
    public async Task<HetznerServerCreated> CreateServerAsync(
        string apiToken,
        string name,
        string serverType,
        string location,
        string image,
        long sshKeyId,
        string cloudInitScript,
        CancellationToken ct)
    {
        using var client = BuildClient(apiToken);

        var payload = new
        {
            name,
            server_type = serverType,
            image,
            location,
            user_data = cloudInitScript,
            ssh_keys = new[] { sshKeyId.ToString() },
            public_net = new
            {
                enable_ipv4 = false,
                enable_ipv6 = true,
            },
        };

        logger.LogInformation(
            "Creating Hetzner server '{Name}' (type={Type}, location={Location})", name, serverType, location);

        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{BaseUrl}/servers", body, ct);
        var responseText = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Hetzner server creation failed ({resp.StatusCode}): {responseText}");

        var json = JsonSerializer.Deserialize<HetznerCreateServerResponse>(responseText, JsonOpts)
            ?? throw new InvalidOperationException("Null response from Hetzner create server API");
        return json.Server ?? throw new InvalidOperationException("No server in Hetzner create response");
    }

    /// <summary>Gets details of a single server by its Hetzner numeric ID.</summary>
    public async Task<HetznerServerDetails?> GetServerAsync(string apiToken, long hetznerServerId, CancellationToken ct)
    {
        using var client = BuildClient(apiToken);
        var resp = await client.GetAsync($"{BaseUrl}/servers/{hetznerServerId}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<HetznerGetServerResponse>(JsonOpts, ct);
        return json?.Server;
    }

    /// <summary>Lists all servers visible to the API token, optionally filtered by label.</summary>
    public async Task<IReadOnlyList<HetznerServerDetails>> ListServersAsync(string apiToken, CancellationToken ct)
    {
        using var client = BuildClient(apiToken);
        var resp = await client.GetAsync($"{BaseUrl}/servers?label_selector=issuepit-managed%3Dtrue", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<HetznerListServersResponse>(JsonOpts, ct);
        return json?.Servers ?? [];
    }

    /// <summary>Sends a power-on action to the server.</summary>
    public Task ServerActionAsync(string apiToken, long hetznerServerId, string action, CancellationToken ct)
        => SendServerActionAsync(apiToken, hetznerServerId, action, ct);

    /// <summary>Deletes a server. This is irreversible.</summary>
    public async Task DeleteServerAsync(string apiToken, long hetznerServerId, CancellationToken ct)
    {
        using var client = BuildClient(apiToken);
        logger.LogInformation("Deleting Hetzner server {Id}", hetznerServerId);
        var resp = await client.DeleteAsync($"{BaseUrl}/servers/{hetznerServerId}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return; // already gone
        resp.EnsureSuccessStatusCode();
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    private async Task SendServerActionAsync(string apiToken, long hetznerServerId, string action, CancellationToken ct)
    {
        using var client = BuildClient(apiToken);
        var resp = await client.PostAsync(
            $"{BaseUrl}/servers/{hetznerServerId}/actions/{action}",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            ct);
        resp.EnsureSuccessStatusCode();
    }

    // ─── Response DTOs ─────────────────────────────────────────────────────────

    public record HetznerSshKeyInfo(long Id, string Name, string Fingerprint);

    private record HetznerSshKeysResponse([property: JsonPropertyName("ssh_keys")] List<HetznerSshKeyInfo>? SshKeys);
    private record HetznerSshKeyResponse([property: JsonPropertyName("ssh_key")] HetznerSshKeyInfo? SshKey);

    public record HetznerServerCreated(
        long Id,
        string Name,
        string Status,
        [property: JsonPropertyName("public_net")] HetznerPublicNet? PublicNet);

    public record HetznerServerDetails(
        long Id,
        string Name,
        string Status,
        [property: JsonPropertyName("public_net")] HetznerPublicNet? PublicNet,
        [property: JsonPropertyName("server_type")] HetznerServerTypeInfo? ServerType);

    public record HetznerPublicNet(
        [property: JsonPropertyName("ipv4")] HetznerIpInfo? Ipv4,
        [property: JsonPropertyName("ipv6")] HetznerIpv6Info? Ipv6);

    public record HetznerIpInfo(string? Ip);

    public record HetznerIpv6Info(string? Ip);

    public record HetznerServerTypeInfo(string Name, int Memory, int Cores);

    private record HetznerCreateServerResponse([property: JsonPropertyName("server")] HetznerServerCreated? Server);
    private record HetznerGetServerResponse([property: JsonPropertyName("server")] HetznerServerDetails? Server);
    private record HetznerListServersResponse([property: JsonPropertyName("servers")] List<HetznerServerDetails>? Servers);
}
