using YamlDotNet.RepresentationModel;

namespace IssuePit.Api.Services;

/// <summary>A node in the workflow job graph representing a single job.</summary>
public record WorkflowJobNode(
    string Id,
    string Name,
    string? RunsOn,
    IReadOnlyList<string> Needs);

/// <summary>A directed edge from one job to another (dependency: <see cref="From"/> must complete before <see cref="To"/>).</summary>
public record WorkflowEdge(string From, string To);

/// <summary>Full graph of workflow jobs and their dependencies.</summary>
public record WorkflowGraph(
    IReadOnlyList<WorkflowJobNode> Jobs,
    IReadOnlyList<WorkflowEdge> Edges);

/// <summary>
/// Parses GitHub Actions workflow YAML to extract the job dependency graph.
/// Only the <c>jobs</c> key is inspected; all other workflow keys are ignored.
/// </summary>
public static class WorkflowGraphParser
{
    /// <summary>
    /// Parses <paramref name="yamlContent"/> and returns the job graph.
    /// Returns an empty graph when the content is null/empty or does not contain a valid <c>jobs</c> map.
    /// </summary>
    public static WorkflowGraph Parse(string? yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
            return new WorkflowGraph([], []);

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlContent));

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return new WorkflowGraph([], []);

            // Find the 'jobs' mapping at the top level
            var jobsNode = root.Children
                .FirstOrDefault(kv => kv.Key is YamlScalarNode k && k.Value == "jobs")
                .Value as YamlMappingNode;

            if (jobsNode is null)
                return new WorkflowGraph([], []);

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

            return new WorkflowGraph(jobs, edges);
        }
        catch (Exception)
        {
            // Malformed YAML — return empty graph rather than propagating
            return new WorkflowGraph([], []);
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
