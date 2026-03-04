namespace IssuePit.Core.Enums;

/// <summary>
/// Controls how Docker image layers are cached for Docker-in-Docker (DinD) CI/CD containers
/// launched by the <c>DockerCiCdRuntime</c>.
/// </summary>
public enum DindImageCacheStrategy
{
    /// <summary>
    /// No image caching. Every CI/CD run starts with an empty DinD image store.
    /// Images are pulled fresh on every run. Use when disk space is constrained
    /// or when reproducibility is more important than speed.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Mounts a persistent host directory as <c>/var/lib/docker</c> inside the DinD container.
    /// Pulled images and layers survive across runs, dramatically reducing pull times.
    /// <para>
    /// <b>Security:</b> Requires <c>Privileged=true</c> containers (already required for DinD).
    /// The host directory must be dedicated to this purpose; avoid sharing with other runtimes.
    /// </para>
    /// <para>
    /// <b>Disk:</b> The volume grows over time as images accumulate. Monitor disk usage and
    /// run <c>docker system prune</c> periodically on the host.
    /// </para>
    /// <para>Configure the host path with <c>CiCd__Docker__DindCacheVolumePath</c>
    /// (default: <c>/var/lib/issuepit-dind-cache</c>).</para>
    /// </summary>
    LocalVolume = 1,

    /// <summary>
    /// Combines a persistent <c>/var/lib/docker</c> volume mount (<see cref="LocalVolume"/>)
    /// with a Docker pull-through registry mirror (<c>registry:2</c>) running as a sidecar.
    /// The DinD Docker daemon is configured to route all image pulls through the local mirror,
    /// so cache hits are served from the same host without touching the upstream registry.
    /// This is the <b>default</b> strategy.
    /// <para>
    /// <b>When to use:</b> When you run many parallel CI/CD containers or want to share a
    /// single image cache across multiple host machines (by pointing the mirror's storage at
    /// a shared NFS/block volume).
    /// </para>
    /// <para>
    /// <b>Aspire:</b> the <c>registry-mirror</c> Aspire resource (container name
    /// <c>issuepit-registry-mirror</c>) is started automatically with a persistent Docker volume
    /// (<c>issuepit-registry-cache</c>) and port 5100. The runtime reuses it when running.
    /// </para>
    /// <para>
    /// <b>Security:</b> The sidecar container runs with <c>--restart=unless-stopped</c>.
    /// It only caches public images; private registry credentials are never forwarded.
    /// If the registry is unavailable, CI/CD runs will fail (intentional — no silent fallback).
    /// </para>
    /// <para>
    /// <b>Disk:</b> Registry data is stored at <c>CiCd__Docker__RegistryMirrorVolumePath</c>
    /// (default: <c>/var/lib/issuepit-registry-cache</c>). Monitor and prune as needed.
    /// </para>
    /// <para>Configure the mirror port with <c>CiCd__Docker__RegistryMirrorPort</c>
    /// (default: <c>5100</c>).</para>
    /// </summary>
    RegistryMirror = 2,
}
