namespace IssuePit.Core.Enums;

public enum RuntimeType
{
    /// <summary>Runs directly on the host machine.</summary>
    Native,
    /// <summary>Runs inside a local Docker container (Docker-in-Docker supported).</summary>
    Docker,
    /// <summary>Connects to a remote host via SSH and executes the agent natively there.</summary>
    Ssh,
    /// <summary>Connects to a remote host via SSH and runs the agent inside a Docker container there.</summary>
    SshDocker,
    /// <summary>Provisions a Hetzner Cloud server with Terraform and then connects via SSH.</summary>
    HetznerSsh,
    /// <summary>Uses Alibaba OpenSandbox as the execution environment.</summary>
    OpenSandbox,
}
