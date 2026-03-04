using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Docker.DotNet;
using Docker.DotNet.Handler.Abstractions;
using Docker.DotNet.NativeHttp;
using Docker.DotNet.X509;
using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");

// Register Docker client (used by DockerCiCdRuntime)
// On Windows, use TCP+NativeHttp (SocketsHttpHandler) to avoid fragile named-pipe connections.
// On Linux/macOS with no explicit Docker endpoint the default Unix-socket transport is used.
// Supports TLS via DOCKER_TLS_VERIFY / Docker:TlsEnabled and standard cert config.
// NOTE: identical factory exists in IssuePit.ExecutionClient/Program.cs — keep both in sync.
builder.Services.AddSingleton(_ => CreateDockerClient(builder.Configuration));

static DockerClient CreateDockerClient(IConfiguration configuration)
{
    // Resolve endpoint from DOCKER_HOST env var or Docker:Host config.
    // Supports tcp://, http://, https:// schemes; unix:// is handled by the default builder.
    var dockerHostRaw = Environment.GetEnvironmentVariable("DOCKER_HOST")
        ?? configuration["Docker:Host"];

    Uri? explicitEndpoint = null;
    if (dockerHostRaw != null && Uri.TryCreate(dockerHostRaw, UriKind.Absolute, out var parsed)
        && parsed.Scheme.ToLowerInvariant() is "tcp" or "http" or "https")
        explicitEndpoint = parsed;

    // On Linux/macOS with no explicit endpoint, use the default Unix-socket transport.
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && explicitEndpoint == null)
        return new DockerClientBuilder().Build();

    // Determine TLS:
    //   DOCKER_TLS_VERIFY=1  or  Docker:TlsEnabled=true  or  https:// endpoint scheme.
    var tlsEnabled = Environment.GetEnvironmentVariable("DOCKER_TLS_VERIFY") == "1"
        || configuration.GetValue<bool>("Docker:TlsEnabled")
        || explicitEndpoint?.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) == true;

    // Default Windows endpoint: HTTPS port 2376 (with TLS) or plain HTTP port 2375.
    var endpoint = explicitEndpoint
        ?? new Uri(tlsEnabled ? "https://localhost:2376" : "http://localhost:2375");

    // https:// endpoint scheme always implies TLS even without DOCKER_TLS_VERIFY.
    if (endpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        tlsEnabled = true;

    // Build the client using the native SocketsHttpHandler transport to avoid named-pipe issues.
    // WithTransportOptions creates a new DockerClientBuilder<T>; subsequent With* calls mutate it in place.
    var clientBuilder = new DockerClientBuilder()
        .WithTransportOptions(new NativeHttpTransportOptions())
        .WithEndpoint(endpoint);

    if (tlsEnabled)
    {
        // Cert path resolution (standard Docker convention):
        //   DOCKER_CERT_PATH env var → Docker:CertPath config → ~/.docker
        var certPath = Environment.GetEnvironmentVariable("DOCKER_CERT_PATH")
            ?? configuration["Docker:CertPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker");

        var certFile = configuration["Docker:CertFile"] ?? Path.Combine(certPath, "cert.pem");
        var keyFile = configuration["Docker:KeyFile"] ?? Path.Combine(certPath, "key.pem");
        var caFile = configuration["Docker:CaFile"] ?? Path.Combine(certPath, "ca.pem");
        var certPassword = configuration["Docker:CertPassword"];

        // Load client certificate: PFX (PKCS12) when password is set or extension is .pfx/.p12,
        // otherwise PEM cert+key pair.
        bool isPfx = certPassword != null
            || Path.GetExtension(certFile).ToLowerInvariant() is ".pfx" or ".p12";
        if (!isPfx && !File.Exists(certFile))
            throw new InvalidOperationException(
                $"Docker TLS is enabled but client cert file not found: '{certFile}'. " +
                "Set Docker:CertFile or DOCKER_CERT_PATH to the correct path.");
        if (!isPfx && !File.Exists(keyFile))
            throw new InvalidOperationException(
                $"Docker TLS is enabled but client key file not found: '{keyFile}'. " +
                "Set Docker:KeyFile or DOCKER_CERT_PATH to the correct path.");

        X509Certificate2 clientCert;
        try
        {
            clientCert = isPfx
                ? X509CertificateLoader.LoadPkcs12FromFile(certFile, certPassword)
                : X509Certificate2.CreateFromPemFile(certFile, keyFile);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load Docker client certificate from '{certFile}': {ex.Message}", ex);
        }

        // Use CA cert for server validation when present; fall back to trust-all for self-signed.
        IAuthProvider credentials;
        if (File.Exists(caFile))
        {
            X509Certificate2 caCert;
            try
            {
                caCert = X509CertificateLoader.LoadCertificateFromFile(caFile);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load Docker CA certificate from '{caFile}': {ex.Message}", ex);
            }
            credentials = new DockerTlsCertificates(clientCert, caCert).CreateCredentials();
        }
        else
        {
            var certCreds = new CertificateCredentials(clientCert);
            certCreds.ServerCertificateValidationCallback = (_, _, _, _) => true;
            credentials = certCreds;
        }

        clientBuilder = clientBuilder.WithAuthProvider(credentials);
    }

    return clientBuilder.Build();
}

// Register CI/CD runtime implementations
builder.Services.AddSingleton<DockerCiCdRuntime>();
builder.Services.AddSingleton<NativeCiCdRuntime>();
builder.Services.AddSingleton<DryRunCiCdRuntime>();
builder.Services.AddSingleton<CiCdRuntimeFactory>();

builder.Services.AddHostedService<CiCdWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
