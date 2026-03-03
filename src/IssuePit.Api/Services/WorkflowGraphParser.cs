using System.Diagnostics;
using YamlDotNet.RepresentationModel;

namespace IssuePit.Api.Services;

/// <summary>A node in the workflow job graph representing a single job.</summary>
public record WorkflowJobNode(
    string Id,
    string Name,
    string? RunsOn,
    IReadOnlyList<string> Needs,
    /// <summary>Workflow filename (e.g. <c>backend.yml</c>) — set only when the graph merges multiple files.</summary>
    string? WorkflowFile = null);

/// <summary>A directed edge from one job to another (dependency: <see cref="From"/> must complete before <see cref="To"/>).</summary>
public record WorkflowEdge(string From, string To);

/// <summary>Full graph of workflow jobs and their dependencies.</summary>
public record WorkflowGraph(
    IReadOnlyList<WorkflowJobNode> Jobs,
    IReadOnlyList<WorkflowEdge> Edges,
    /// <summary>Lint warnings emitted by actionlint, if available. Empty when actionlint is not installed.</summary>
    IReadOnlyList<string> Warnings);

/// <summary>
/// Parses GitHub Actions workflow YAML to extract the job dependency graph.
/// Tries to call <c>actionlint</c> (from the helper image) as a subprocess for linting/validation,
/// then falls back to direct YAML parsing when actionlint is not available on the host.
/// </summary>
public static class WorkflowGraphParser
{
    /// <summary>
    /// Reads the workflow YAML at <paramref name="filePath"/>, parses the job graph,
    /// and (if actionlint is available) validates the file for lint errors.
    /// Throws <see cref="FileNotFoundException"/> when the file does not exist.
    /// </summary>
    public static async Task<WorkflowGraph> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException($"Workflow file not found: {filePath}", filePath);

        var yamlContent = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
        var graph = ParseYaml(yamlContent);

        // Run actionlint validation if the binary is available. This is a best-effort step:
        // actionlint is included in the helper-act image and may be installed on the host,
        // but its absence is not an error — we still return the YAML-parsed graph.
        var warnings = await TryRunActionlintAsync(filePath, cancellationToken);

        return graph with { Warnings = warnings };
    }

    /// <summary>
    /// Parses all <c>*.yml</c> / <c>*.yaml</c> files found in <paramref name="workflowsDir"/> and
    /// merges them into a single <see cref="WorkflowGraph"/>.
    /// <para>
    /// When only one file exists the result is identical to calling <see cref="ParseFileAsync"/>
    /// for that file (job IDs unchanged). When multiple files exist every job ID is prefixed with
    /// the file's name stem (e.g. <c>backend/build</c>, <c>frontend/build</c>) so that jobs with
    /// the same ID in different files remain distinct. The <see cref="WorkflowJobNode.WorkflowFile"/>
    /// property is set for every node in the multi-file case.
    /// </para>
    /// </summary>
    public static async Task<WorkflowGraph> ParseDirectoryAsync(string workflowsDir, CancellationToken cancellationToken = default)
    {
        var files = Directory
            .EnumerateFiles(workflowsDir, "*.yml")
            .Concat(Directory.EnumerateFiles(workflowsDir, "*.yaml"))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            return new WorkflowGraph([], [], []);

        // Single file — behave exactly as ParseFileAsync.
        if (files.Count == 1)
            return await ParseFileAsync(files[0], cancellationToken);

        // Multiple files — parse each and merge with prefixed job IDs.
        var allJobs = new List<WorkflowJobNode>();
        var allEdges = new List<WorkflowEdge>();
        var allWarnings = new List<string>();

        foreach (var filePath in files)
        {
            var yamlContent = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
            var fileGraph = ParseYaml(yamlContent);
            var stem = Path.GetFileName(filePath); // e.g. "backend.yml"
            var prefix = Path.GetFileNameWithoutExtension(filePath); // e.g. "backend"

            // Build a lookup so we can rewrite 'needs' references within this file.
            // Only jobs defined in the same file are prefixed; 'needs' entries that reference
            // jobs from other files are left as-is (cross-file dependencies are not valid in
            // GitHub Actions — each workflow runs independently).
            var fileJobIds = new HashSet<string>(fileGraph.Jobs.Select(j => j.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var job in fileGraph.Jobs)
            {
                var prefixedId = $"{prefix}/{job.Id}";
                var prefixedNeeds = job.Needs
                    .Select(n => fileJobIds.Contains(n) ? $"{prefix}/{n}" : n)
                    .ToList();
                allJobs.Add(job with { Id = prefixedId, Needs = prefixedNeeds, WorkflowFile = stem });
            }

            foreach (var edge in fileGraph.Edges)
            {
                var from = fileJobIds.Contains(edge.From) ? $"{prefix}/{edge.From}" : edge.From;
                var to   = fileJobIds.Contains(edge.To)   ? $"{prefix}/{edge.To}"   : edge.To;
                allEdges.Add(new WorkflowEdge(from, to));
            }

            var fileWarnings = await TryRunActionlintAsync(filePath, cancellationToken);
            foreach (var w in fileWarnings)
                allWarnings.Add(w);
        }

        return new WorkflowGraph(allJobs, allEdges, allWarnings);
    }

    /// <summary>
    /// Parses <paramref name="yamlContent"/> and returns the job graph without actionlint validation.
    /// Returns an empty graph when the content is null/empty or does not contain a valid <c>jobs</c> map.
    /// </summary>
    public static WorkflowGraph Parse(string? yamlContent) =>
        ParseYaml(yamlContent);

    private static WorkflowGraph ParseYaml(string? yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
            return new WorkflowGraph([], [], []);

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlContent));

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return new WorkflowGraph([], [], []);

            // Find the 'jobs' mapping at the top level
            var jobsNode = root.Children
                .FirstOrDefault(kv => kv.Key is YamlScalarNode k && k.Value == "jobs")
                .Value as YamlMappingNode;

            if (jobsNode is null)
                return new WorkflowGraph([], [], []);

            var jobs = new List<WorkflowJobNode>();

            foreach (var (keyNode, valueNode) in jobsNode.Children)
            {
                if (keyNode is not YamlScalarNode jobKeyNode || jobKeyNode.Value is null)
                    continue;

                var jobId = jobKeyNode.Value;
                var jobMapping = valueNode as YamlMappingNode;

                var name = jobId;
                string? runsOn = null;
                var needs = new List<string>();

                if (jobMapping is not null)
                {
                    // Extract optional human-readable name
                    if (TryGetScalar(jobMapping, "name", out var jobName))
                        name = jobName ?? jobId;

                    // Extract runs-on
                    TryGetScalar(jobMapping, "runs-on", out runsOn);

                    // Extract needs (string or sequence)
                    var needsNode = GetChildNode(jobMapping, "needs");
                    switch (needsNode)
                    {
                        case YamlScalarNode needsScalar when needsScalar.Value is not null:
                            needs.Add(needsScalar.Value);
                            break;
                        case YamlSequenceNode needsSeq:
                            foreach (var item in needsSeq)
                            {
                                if (item is YamlScalarNode itemScalar && itemScalar.Value is not null)
                                    needs.Add(itemScalar.Value);
                            }
                            break;
                    }
                }

                jobs.Add(new WorkflowJobNode(jobId, name, runsOn, needs));
            }

            // Build edges: for each job, each need produces an edge (need → job)
            var edges = jobs
                .SelectMany(j => j.Needs.Select(n => new WorkflowEdge(n, j.Id)))
                .ToList();

            return new WorkflowGraph(jobs, edges, []);
        }
        catch (Exception)
        {
            // Malformed YAML — return empty graph rather than propagating
            return new WorkflowGraph([], [], []);
        }
    }

    /// <summary>
    /// Tries to run <c>actionlint &lt;filePath&gt;</c> as a subprocess.
    /// Returns lint warnings (one per line) when actionlint is available, or an empty list when
    /// actionlint is not installed or times out. Never throws.
    /// </summary>
    private static async Task<IReadOnlyList<string>> TryRunActionlintAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo("actionlint", filePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };

            if (!process.Start())
                return [];

            // Allow up to 10 seconds for actionlint to complete.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(combined.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(combined.Token);

            await process.WaitForExitAsync(combined.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // actionlint writes lint errors to stdout; combine stdout + stderr for warnings.
            var warnings = new List<string>();
            foreach (var line in (stdout + "\n" + stderr).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                warnings.Add(line);

            return warnings;
        }
        catch (Exception)
        {
            // actionlint not installed, or failed to start — silently skip.
            return [];
        }
    }

    private static YamlNode? GetChildNode(YamlMappingNode mapping, string key) =>
        mapping.Children
            .FirstOrDefault(kv => kv.Key is YamlScalarNode k && k.Value == key)
            .Value;

    private static bool TryGetScalar(YamlMappingNode mapping, string key, out string? value)
    {
        var node = GetChildNode(mapping, key);
        if (node is YamlScalarNode scalar)
        {
            value = scalar.Value;
            return true;
        }
        value = null;
        return false;
    }
}
