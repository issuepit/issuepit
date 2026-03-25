namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Resolves the correct <see cref="ICiCdRuntime"/> implementation based on configuration.
/// Mirrors the <c>AgentRuntimeFactory</c> pattern used in the ExecutionClient.
///
/// Selection logic (evaluated in order):
/// <list type="number">
///   <item><c>CiCd:Runtime=Native</c> (env: <c>CiCd__Runtime=Native</c>) → <see cref="NativeCiCdRuntime"/></item>
///   <item><c>CiCd:Runtime=Hetzner</c> (env: <c>CiCd__Runtime=Hetzner</c>) → <see cref="HetznerCiCdRuntime"/></item>
///   <item>Default → <see cref="DockerCiCdRuntime"/></item>
/// </list>
/// </summary>
public class CiCdRuntimeFactory(IServiceProvider services, IConfiguration configuration)
{
    public ICiCdRuntime Create(string? runtimeOverride = null)
    {
        var runtimeName = runtimeOverride
            ?? configuration["CiCd:Runtime"]
            ?? string.Empty;

        if (runtimeName.Equals("Native", StringComparison.OrdinalIgnoreCase))
            return services.GetRequiredService<NativeCiCdRuntime>();

        if (runtimeName.Equals("Hetzner", StringComparison.OrdinalIgnoreCase))
            return services.GetRequiredService<HetznerCiCdRuntime>();

        return services.GetRequiredService<DockerCiCdRuntime>();
    }
}
