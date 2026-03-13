using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Aspire startup project: ensures the Vosk speech-recognition model is present on disk before
// the API starts. Exits immediately (no-op) when:
//   • VoiceTranscription:ModelPath is not configured
//   • the model directory already exists
//   • VoiceTranscription:ModelDownloadUrl is not configured (opt-in auto-download)
// On download failure the project logs a warning and exits with 0 so the API can still start
// (transcription will return empty strings until the model is placed at the configured path).

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("VoskModelDownloader");
var configuration = host.Services.GetRequiredService<IConfiguration>();

var modelPath = configuration["VoiceTranscription:ModelPath"];
var downloadUrl = configuration["VoiceTranscription:ModelDownloadUrl"];

if (string.IsNullOrWhiteSpace(modelPath))
{
    logger.LogInformation(
        "VoiceTranscription:ModelPath is not configured — Vosk transcription disabled. " +
        "Set VoiceTranscription__ModelPath to the directory where the model should be stored.");
    return 0;
}

// final.mdl is present in every Vosk model package and is a reliable presence check
const string VoskModelMarkerFile = "final.mdl";
if (Directory.Exists(modelPath) && File.Exists(Path.Combine(modelPath, VoskModelMarkerFile)))
{
    logger.LogInformation("Vosk model already present at {ModelPath}", modelPath);
    return 0;
}

if (string.IsNullOrWhiteSpace(downloadUrl))
{
    logger.LogInformation(
        "Vosk model not found at {ModelPath}. " +
        "To enable automatic download set VoiceTranscription__ModelDownloadUrl to the ZIP URL " +
        "(e.g. https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip). " +
        "Alternatively download the model manually from https://alphacephei.com/vosk/models/ " +
        "and extract it to {ModelPath}.",
        modelPath, modelPath);
    return 0;
}

// Download and extract
try
{
    var parentDir = Path.GetDirectoryName(Path.GetFullPath(modelPath))!;
    Directory.CreateDirectory(parentDir);

    var tmpZip = Path.Combine(Path.GetTempPath(), $"vosk-model-{Guid.NewGuid():N}.zip");

    logger.LogInformation("Downloading Vosk model from {Url}…", downloadUrl);
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    await using (var fs = File.Create(tmpZip))
        await response.Content.CopyToAsync(fs);

    logger.LogInformation("Extracting model archive to {ParentDir}…", parentDir);
    ZipFile.ExtractToDirectory(tmpZip, parentDir, overwriteFiles: true);
    File.Delete(tmpZip);

    logger.LogInformation("Vosk model ready at {ModelPath}", modelPath);
}
catch (Exception ex)
{
    logger.LogWarning(ex,
        "Failed to download or extract the Vosk model — transcription will be unavailable. " +
        "Place the model manually at {ModelPath}", modelPath);
    // Return 0 so the API still starts; it will run without transcription.
}

return 0;
