using IssuePit.Core.Services;

namespace IssuePit.Tests.Unit;

public class WorkflowGraphParserTests
{
    [Fact]
    public void Parse_NullContent_Returns_EmptyGraph()
    {
        var graph = WorkflowGraphParser.Parse(null);
        Assert.Empty(graph.Jobs);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Parse_EmptyContent_Returns_EmptyGraph()
    {
        var graph = WorkflowGraphParser.Parse(string.Empty);
        Assert.Empty(graph.Jobs);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Parse_SingleJob_NoNeeds_Returns_OneJobNoEdges()
    {
        const string yaml = """
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - run: echo hello
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Single(graph.Jobs);
        Assert.Empty(graph.Edges);
        Assert.Equal("build", graph.Jobs[0].Id);
        Assert.Equal("ubuntu-latest", graph.Jobs[0].RunsOn);
    }

    [Fact]
    public void Parse_TwoJobs_StringNeeds_Returns_OneEdge()
    {
        const string yaml = """
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - run: echo build
              test:
                needs: build
                runs-on: ubuntu-latest
                steps:
                  - run: echo test
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Equal(2, graph.Jobs.Count);
        Assert.Single(graph.Edges);
        Assert.Equal("build", graph.Edges[0].From);
        Assert.Equal("test", graph.Edges[0].To);
    }

    [Fact]
    public void Parse_JobWithMultipleNeeds_Returns_MultipleEdges()
    {
        const string yaml = """
            jobs:
              build:
                runs-on: ubuntu-latest
                steps: []
              test:
                runs-on: ubuntu-latest
                steps: []
              deploy:
                needs: [build, test]
                runs-on: ubuntu-latest
                steps: []
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Equal(3, graph.Jobs.Count);
        Assert.Equal(2, graph.Edges.Count);
        Assert.Contains(graph.Edges, e => e.From == "build" && e.To == "deploy");
        Assert.Contains(graph.Edges, e => e.From == "test" && e.To == "deploy");
    }

    [Fact]
    public void Parse_JobWithCustomName_Returns_NameFromYaml()
    {
        const string yaml = """
            jobs:
              build-job:
                name: Build the application
                runs-on: ubuntu-latest
                steps: []
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Single(graph.Jobs);
        Assert.Equal("build-job", graph.Jobs[0].Id);
        Assert.Equal("Build the application", graph.Jobs[0].Name);
    }

    [Fact]
    public void Parse_InvalidYaml_Returns_EmptyGraph()
    {
        const string yaml = "this: is: invalid: yaml: [";

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Empty(graph.Jobs);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Parse_YamlWithoutJobsKey_Returns_EmptyGraph()
    {
        const string yaml = """
            name: My Workflow
            on: push
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Empty(graph.Jobs);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Parse_JobWithLocalUsesField_Sets_UsesWorkflow()
    {
        const string yaml = """
            jobs:
              backend:
                name: Backend
                uses: ./.github/workflows/backend.yml
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Single(graph.Jobs);
        Assert.Equal("backend", graph.Jobs[0].Id);
        Assert.Equal("backend.yml", graph.Jobs[0].UsesWorkflow);
    }

    [Fact]
    public void Parse_JobWithExternalUsesField_DoesNotSet_UsesWorkflow()
    {
        const string yaml = """
            jobs:
              caller:
                uses: owner/repo/.github/workflows/file.yml@main
            """;

        var graph = WorkflowGraphParser.Parse(yaml);

        Assert.Single(graph.Jobs);
        Assert.Null(graph.Jobs[0].UsesWorkflow);
    }

    // ── ParseTriggers ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseTriggers_ScalarPush_Returns_Push()
    {
        const string yaml = """
            on: push
            jobs: {}
            """;

        var triggers = WorkflowGraphParser.ParseTriggers(yaml);
        Assert.Single(triggers);
        Assert.Equal("push", triggers[0]);
    }

    [Fact]
    public void ParseTriggers_SequenceForm_Returns_AllEvents()
    {
        const string yaml = """
            on: [push, pull_request]
            jobs: {}
            """;

        var triggers = WorkflowGraphParser.ParseTriggers(yaml);
        Assert.Equal(2, triggers.Count);
        Assert.Contains("push", triggers);
        Assert.Contains("pull_request", triggers);
    }

    [Fact]
    public void ParseTriggers_MappingForm_Returns_EventKeys()
    {
        const string yaml = """
            on:
              push:
                branches: [main]
              pull_request:
                types: [opened, synchronize]
            jobs: {}
            """;

        var triggers = WorkflowGraphParser.ParseTriggers(yaml);
        Assert.Equal(2, triggers.Count);
        Assert.Contains("push", triggers);
        Assert.Contains("pull_request", triggers);
    }

    [Fact]
    public void ParseTriggers_NoOnSection_Returns_Empty()
    {
        const string yaml = """
            jobs:
              build:
                runs-on: ubuntu-latest
            """;

        var triggers = WorkflowGraphParser.ParseTriggers(yaml);
        Assert.Empty(triggers);
    }

    // ── ParseDirectoryAsync substitution ──────────────────────────────────────

    [Fact]
    public async Task ParseDirectoryAsync_SubstitutesReusableWorkflowCalls()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            // ci.yml calls backend.yml and frontend.yml; report depends on backend
            await File.WriteAllTextAsync(Path.Combine(dir, "ci.yml"), """
                on: push
                jobs:
                  backend:
                    name: Backend
                    uses: ./.github/workflows/backend.yml
                  frontend:
                    name: Frontend
                    uses: ./.github/workflows/frontend.yml
                  report:
                    name: Test Report
                    runs-on: ubuntu-latest
                    needs: [backend]
                """);

            await File.WriteAllTextAsync(Path.Combine(dir, "backend.yml"), """
                on: push
                jobs:
                  build:
                    name: Build Backend
                    runs-on: ubuntu-latest
                  test:
                    name: Test Backend
                    runs-on: ubuntu-latest
                    needs: build
                """);

            await File.WriteAllTextAsync(Path.Combine(dir, "frontend.yml"), """
                on: push
                jobs:
                  lint:
                    name: Lint Frontend
                    runs-on: ubuntu-latest
                """);

            var graph = await WorkflowGraphParser.ParseDirectoryAsync(dir);

            // Caller jobs (ci/backend, ci/frontend) must be substituted away.
            Assert.DoesNotContain(graph.Jobs, j => j.Id == "ci/backend");
            Assert.DoesNotContain(graph.Jobs, j => j.Id == "ci/frontend");

            // Callee jobs must be present with combined names and CallerWorkflowFile set.
            Assert.Contains(graph.Jobs, j => j.Id == "backend/build" && j.Name == "Backend / Build Backend" && j.CallerWorkflowFile == "ci.yml");
            Assert.Contains(graph.Jobs, j => j.Id == "backend/test" && j.Name == "Backend / Test Backend" && j.CallerWorkflowFile == "ci.yml");
            Assert.Contains(graph.Jobs, j => j.Id == "frontend/lint" && j.Name == "Frontend / Lint Frontend" && j.CallerWorkflowFile == "ci.yml");

            // report job must still be present.
            Assert.Contains(graph.Jobs, j => j.Id == "ci/report");

            // report must now depend on the leaf of backend (backend/test, not ci/backend).
            var report = graph.Jobs.First(j => j.Id == "ci/report");
            Assert.DoesNotContain("ci/backend", report.Needs);
            Assert.Contains("backend/test", report.Needs);

            // Edge backend/test → ci/report must exist (not ci/backend → ci/report).
            Assert.DoesNotContain(graph.Edges, e => e.From == "ci/backend");
            Assert.Contains(graph.Edges, e => e.From == "backend/test" && e.To == "ci/report");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseDirectoryAsync_IncludesWorkflowTriggers()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "ci.yml"), """
                on: [push, workflow_dispatch]
                jobs:
                  build:
                    runs-on: ubuntu-latest
                """);

            await File.WriteAllTextAsync(Path.Combine(dir, "pr.yml"), """
                on: pull_request
                jobs:
                  check:
                    runs-on: ubuntu-latest
                """);

            var graph = await WorkflowGraphParser.ParseDirectoryAsync(dir);

            Assert.NotNull(graph.WorkflowTriggers);
            Assert.True(graph.WorkflowTriggers!.ContainsKey("ci.yml"));
            Assert.True(graph.WorkflowTriggers!.ContainsKey("pr.yml"));
            Assert.Contains("push", graph.WorkflowTriggers["ci.yml"]);
            Assert.Contains("workflow_dispatch", graph.WorkflowTriggers["ci.yml"]);
            Assert.Contains("pull_request", graph.WorkflowTriggers["pr.yml"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ParseWorkflowDispatchInputs_WithInputs_Returns_AllFields()
    {
        const string yaml = """
            on:
              workflow_dispatch:
                inputs:
                  environment:
                    description: 'Target environment'
                    required: true
                    type: choice
                    options:
                      - staging
                      - production
                  version:
                    description: 'Release version'
                    required: false
                    default: 'latest'
                    type: string
                  dry_run:
                    description: 'Dry run mode'
                    type: boolean
                    default: 'false'
            jobs:
              deploy:
                runs-on: ubuntu-latest
                steps:
                  - run: echo deploy
            """;

        var inputs = WorkflowGraphParser.ParseWorkflowDispatchInputs(yaml);

        Assert.Equal(3, inputs.Count);

        var env = inputs.First(i => i.Name == "environment");
        Assert.Equal("Target environment", env.Description);
        Assert.True(env.Required);
        Assert.Equal("choice", env.Type);
        Assert.NotNull(env.Options);
        Assert.Contains("staging", env.Options!);
        Assert.Contains("production", env.Options!);

        var ver = inputs.First(i => i.Name == "version");
        Assert.Equal("latest", ver.Default);
        Assert.False(ver.Required);
        Assert.Equal("string", ver.Type);

        var dryRun = inputs.First(i => i.Name == "dry_run");
        Assert.Equal("boolean", dryRun.Type);
    }

    [Fact]
    public void ParseWorkflowDispatchInputs_NoWorkflowDispatchTrigger_ReturnsEmpty()
    {
        const string yaml = """
            on:
              push:
                branches: [main]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - run: echo build
            """;

        var inputs = WorkflowGraphParser.ParseWorkflowDispatchInputs(yaml);
        Assert.Empty(inputs);
    }

    [Fact]
    public void ParseWorkflowDispatchInputs_EmptyInputsSection_ReturnsEmpty()
    {
        const string yaml = """
            on:
              workflow_dispatch:
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - run: echo build
            """;

        var inputs = WorkflowGraphParser.ParseWorkflowDispatchInputs(yaml);
        Assert.Empty(inputs);
    }

    [Fact]
    public async Task ParseWorkflowInfosAsync_Returns_FileNamesTriggersAndInputs()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"wf-infos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "ci.yml"), """
                on:
                  push:
                    branches: [main]
                  workflow_dispatch:
                    inputs:
                      debug:
                        type: boolean
                        description: 'Enable debug'
                        default: 'false'
                jobs:
                  build:
                    runs-on: ubuntu-latest
                    steps:
                      - run: echo build
                """);

            await File.WriteAllTextAsync(Path.Combine(dir, "pr.yml"), """
                on:
                  pull_request:
                jobs:
                  lint:
                    runs-on: ubuntu-latest
                    steps:
                      - run: echo lint
                """);

            var infos = await WorkflowGraphParser.ParseWorkflowInfosAsync(dir);

            Assert.Equal(2, infos.Count);

            var ci = infos.First(i => i.FileName == "ci.yml");
            Assert.Contains("push", ci.Triggers);
            Assert.Contains("workflow_dispatch", ci.Triggers);
            Assert.Single(ci.DispatchInputs);
            Assert.Equal("debug", ci.DispatchInputs[0].Name);

            var pr = infos.First(i => i.FileName == "pr.yml");
            Assert.Contains("pull_request", pr.Triggers);
            Assert.Empty(pr.DispatchInputs);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── ParseFromStringsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ParseFromStringsAsync_EmptyDictionary_Returns_EmptyGraph()
    {
        var graph = await WorkflowGraphParser.ParseFromStringsAsync(new Dictionary<string, string>());
        Assert.Empty(graph.Jobs);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task ParseFromStringsAsync_SingleFile_Returns_Graph()
    {
        const string yaml = """
            on: push
            jobs:
              build:
                name: Build
                runs-on: ubuntu-latest
              test:
                name: Test
                runs-on: ubuntu-latest
                needs: build
            """;

        var graph = await WorkflowGraphParser.ParseFromStringsAsync(new Dictionary<string, string>
        {
            ["ci.yml"] = yaml,
        });

        Assert.Contains(graph.Jobs, j => j.Id == "build");
        Assert.Contains(graph.Jobs, j => j.Id == "test");
        Assert.Contains(graph.Edges, e => e.From == "build" && e.To == "test");
    }

    [Fact]
    public async Task ParseFromStringsAsync_MultipleFiles_PrefixesJobIds()
    {
        const string backendYaml = """
            on: push
            jobs:
              build:
                name: Build Backend
                runs-on: ubuntu-latest
            """;

        const string frontendYaml = """
            on: push
            jobs:
              build:
                name: Build Frontend
                runs-on: ubuntu-latest
            """;

        var graph = await WorkflowGraphParser.ParseFromStringsAsync(new Dictionary<string, string>
        {
            ["backend.yml"] = backendYaml,
            ["frontend.yml"] = frontendYaml,
        });

        // Both 'build' jobs must be prefixed so they don't collide.
        Assert.Contains(graph.Jobs, j => j.Id == "backend/build");
        Assert.Contains(graph.Jobs, j => j.Id == "frontend/build");
        Assert.DoesNotContain(graph.Jobs, j => j.Id == "build");
    }

    [Fact]
    public async Task ParseFromStringsAsync_MultipleFiles_IncludesWorkflowTriggers()
    {
        const string ciYaml = """
            on: [push, workflow_dispatch]
            jobs:
              build:
                runs-on: ubuntu-latest
            """;

        const string prYaml = """
            on: pull_request
            jobs:
              check:
                runs-on: ubuntu-latest
            """;

        var graph = await WorkflowGraphParser.ParseFromStringsAsync(new Dictionary<string, string>
        {
            ["ci.yml"] = ciYaml,
            ["pr.yml"] = prYaml,
        });

        Assert.NotNull(graph.WorkflowTriggers);
        Assert.True(graph.WorkflowTriggers!.ContainsKey("ci.yml"));
        Assert.True(graph.WorkflowTriggers!.ContainsKey("pr.yml"));
        Assert.Contains("push", graph.WorkflowTriggers["ci.yml"]);
        Assert.Contains("workflow_dispatch", graph.WorkflowTriggers["ci.yml"]);
        Assert.Contains("pull_request", graph.WorkflowTriggers["pr.yml"]);
    }
}
