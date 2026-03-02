using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/version")]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult GetVersion()
    {
        var assembly = typeof(VersionController).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        // InformationalVersion format: "{version}+{gitHash}" (set by .NET SDK from git)
        var plusIndex = informationalVersion.IndexOf('+');
        var version = plusIndex >= 0
            ? informationalVersion[..plusIndex]
            : informationalVersion;
        var gitHash = plusIndex >= 0
            ? informationalVersion[(plusIndex + 1)..]
            : null;

        var gitHashShort = gitHash?.Length >= 7 ? gitHash[..7] : gitHash;
        return Ok(new VersionResponse(version, gitHash, gitHashShort));
    }
}

public record VersionResponse(string Version, string? GitHash, string? GitHashShort);
