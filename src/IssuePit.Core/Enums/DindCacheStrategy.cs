namespace IssuePit.Core.Enums;

/// <summary>
/// Docker image caching strategy for DinD (Docker-in-Docker) containers used by CI/CD runs.
/// Controls how pulled image layers are cached between runs to reduce pull times.
/// </summary>
public enum DindCacheStrategy
{
    /// <summary>No cache — each run starts with a fresh Docker image store inside the DinD container.</summary>
    None,

    /// <summary>
    /// Mounts a persistent host directory at <c>/var/lib/docker</c> inside the DinD container so that
    /// pulled image layers are reused across runs.
    /// <para>
    /// Requires <c>Privileged=true</c> (already set for DinD) and good disk management.
    /// Only safe for serial runs (one concurrent runner) because multiple Docker daemons cannot
    /// safely share the same data-root directory.
    /// </para>
    /// </summary>
    Volume,

    /// <summary>
    /// Starts a pull-through <c>registry:2</c> mirror and configures each DinD daemon to use it,
    /// plus mounts a persistent volume for the DinD container's own <c>/var/lib/docker</c>.
    /// <para>
    /// The registry mirror is safe for concurrent runners because it acts as a shared HTTP cache.
    /// Suitable for both single-host and multi-host setups.
    /// </para>
    /// </summary>
    RegistryMirror,
}
