namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Resolves the correct <see cref="ICiCdRuntime"/> implementation based on configuration.
/// Mirrors the <c>AgentRuntimeFactory</c> pattern used in the ExecutionClient.
///
/// Selection logic (evaluated in order):
/// <list type="number">
///   <item><c>CiCd:DryRun=true</c> (env var <c>CiCd__DryRun</c>) → <see cref="DryRunCiCdRuntime"/></item>
///   <item><c>CiCd__Runtime=Native</c> → <see cref="NativeCiCdRuntime"/></item>
///   <item>Default → <see cref="DockerCiCdRuntime"/></item>
/// </list>
/// </summary>
public class CiCdRuntimeFactory(IServiceProvider services, IConfiguration configuration)
{
    public ICiCdRuntime Create()
    {
        // Note: env var CiCd__DryRun is normalised to config key CiCd:DryRun by
        // EnvironmentVariablesConfigurationProvider (__ → :).
        if (configuration.GetValue<bool>("CiCd:DryRun"))
            return services.GetRequiredService<DryRunCiCdRuntime>();

        var runtimeName = configuration["CiCd__Runtime"] ?? string.Empty;

        return runtimeName.Equals("Native", StringComparison.OrdinalIgnoreCase)
            ? services.GetRequiredService<NativeCiCdRuntime>()
            : services.GetRequiredService<DockerCiCdRuntime>();
    }
}
