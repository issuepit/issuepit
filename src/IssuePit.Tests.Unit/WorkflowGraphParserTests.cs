using IssuePit.Api.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
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
}
