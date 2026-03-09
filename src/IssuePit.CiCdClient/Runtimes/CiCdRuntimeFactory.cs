namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Resolves the correct <see cref="ICiCdRuntime"/> implementation based on configuration.
/// Mirrors the <c>AgentRuntimeFactory</c> pattern used in the ExecutionClient.
///
/// Selection logic (evaluated in order):
/// <list type="number">
///   <item><c>CiCd:DryRun=true</c> (env: <c>CiCd__DryRun=true</c>) → <see cref="DryRunCiCdRuntime"/></item>
///   <item><c>CiCd:Runtime=Native</c> (env: <c>CiCd__Runtime=Native</c>) → <see cref="NativeCiCdRuntime"/></item>
///   <item>Default → <see cref="DockerCiCdRuntime"/></item>
/// </list>
/// </summary>
public class CiCdRuntimeFactory(IServiceProvider services, IConfiguration configuration)
{
    public ICiCdRuntime Create(string? runtimeOverride = null)
    {
        if (configuration.GetValue<bool>("CiCd:DryRun"))
            return services.GetRequiredService<DryRunCiCdRuntime>();

        var runtimeName = runtimeOverride
            ?? configuration["CiCd:Runtime"]
            ?? string.Empty;

        return runtimeName.Equals("Native", StringComparison.OrdinalIgnoreCase)
            ? services.GetRequiredService<NativeCiCdRuntime>()
            : services.GetRequiredService<DockerCiCdRuntime>();
    }
}
