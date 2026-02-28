using IssuePit.Core.Enums;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Resolves the correct <see cref="IAgentRuntime"/> implementation for a given <see cref="RuntimeType"/>.</summary>
public class AgentRuntimeFactory(IServiceProvider services)
{
    public IAgentRuntime Create(RuntimeType type) => type switch
    {
        RuntimeType.Docker => services.GetRequiredService<DockerAgentRuntime>(),
        RuntimeType.Native => services.GetRequiredService<NativeAgentRuntime>(),
        RuntimeType.Ssh => services.GetRequiredService<SshAgentRuntime>(),
        RuntimeType.HetznerSsh => services.GetRequiredService<HetznerSshAgentRuntime>(),
        RuntimeType.OpenSandbox => services.GetRequiredService<OpenSandboxAgentRuntime>(),
        _ => throw new NotSupportedException($"Runtime type '{type}' is not supported."),
    };
}
