namespace IssuePit.GitServer.Services;

/// <summary>
/// Executes the <c>git http-backend</c> CGI program to handle Git smart HTTP protocol requests.
/// </summary>
public class GitBackendService(ILogger<GitBackendService> logger)
{
    /// <summary>
    /// Proxies the HTTP request to <c>git http-backend</c> and streams the response back.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="repoPath">Absolute path to the bare repository directory.</param>
    /// <param name="pathInfo">The path portion relative to the repo root (e.g. /info/refs).</param>
    public async Task ExecuteAsync(HttpContext context, string repoPath, string pathInfo)
    {
        // git http-backend expects GIT_PROJECT_ROOT as the parent of the .git dir,
        // and PATH_INFO as /{repo.git}/{git-path}
        var repoParentDir = Path.GetDirectoryName(repoPath)!;
        var repoName = Path.GetFileName(repoPath);

        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            ArgumentList = { "http-backend" },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        psi.Environment["GIT_PROJECT_ROOT"] = repoParentDir;
        psi.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
        psi.Environment["PATH_INFO"] = $"/{repoName}{pathInfo}";
        psi.Environment["QUERY_STRING"] = context.Request.QueryString.Value ?? "";
        psi.Environment["REQUEST_METHOD"] = context.Request.Method;
        psi.Environment["CONTENT_TYPE"] = context.Request.ContentType ?? "";
        psi.Environment["REMOTE_ADDR"] = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        if (context.Request.ContentLength.HasValue)
            psi.Environment["CONTENT_LENGTH"] = context.Request.ContentLength.Value.ToString();

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git http-backend");

        var inputTask = context.Request.Body.CopyToAsync(process.StandardInput.BaseStream)
            .ContinueWith(_ => process.StandardInput.Close());

        var outputBytes = await ReadProcessOutputAsync(process.StandardOutput.BaseStream);

        await process.WaitForExitAsync();
        await inputTask;

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            logger.LogError("git http-backend exited with code {Code}: {Stderr}", process.ExitCode, stderr);
            context.Response.StatusCode = 500;
            return;
        }

        ParseAndWriteCgiResponse(outputBytes, context.Response, logger);
    }

    private static async Task<byte[]> ReadProcessOutputAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static void ParseAndWriteCgiResponse(byte[] cgiOutput, HttpResponse response, ILogger logger)
    {
        int statusCode = 200;
        var headers = new List<(string Name, string Value)>();

        using var reader = new System.IO.StreamReader(new MemoryStream(cgiOutput));
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) break;

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var name = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();

                if (name.Equals("Status", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value.Split(' ')[0], out var code))
                        statusCode = code;
                }
                else
                {
                    headers.Add((name, value));
                }
            }
        }

        var bodyStart = FindBodyStart(cgiOutput);

        response.StatusCode = statusCode;
        foreach (var (name, value) in headers)
        {
            try { response.Headers[name] = value; }
            catch (Exception ex)
            {
                // Some CGI headers (e.g. invalid names) may be rejected; log and continue
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                logger.LogDebug("Skipping invalid response header '{Name}': {Message}", name, ex.Message);
            }
        }

        if (bodyStart < cgiOutput.Length)
        {
            var bodyBytes = cgiOutput[bodyStart..];
            response.ContentLength = bodyBytes.Length;
            response.Body.Write(bodyBytes);
        }
    }

    private static int FindBodyStart(byte[] data)
    {
        // Search for \r\n\r\n
        for (int i = 0; i < data.Length - 3; i++)
        {
            if (data[i] == '\r' && data[i + 1] == '\n' &&
                data[i + 2] == '\r' && data[i + 3] == '\n')
                return i + 4;
        }
        // Search for \n\n
        for (int i = 0; i < data.Length - 1; i++)
        {
            if (data[i] == '\n' && data[i + 1] == '\n')
                return i + 2;
        }
        return data.Length;
    }
}
