---
title: Test Runner
layout: default
nav_order: 8
---

# Test Runner API

The **Test Runner** is a standalone API service that enables live .NET test discovery and execution against the running Aspire environment. It exposes REST endpoints for listing available tests, triggering test runs, and retrieving structured results — all from the web UI or any HTTP client.

---

## How It Works

The Test Runner service invokes `dotnet test` as an external process:

1. **Discovery** — runs `dotnet test --list-tests` to enumerate all available test cases in the configured solution.
2. **Execution** — runs `dotnet test` with TRX logging enabled, optionally filtered to specific tests.
3. **Result Parsing** — parses the generated `.trx` files using the existing `TrxParser` to produce structured test results with per-test outcomes, durations, and error details.

Test runs are tracked in-memory and can be polled for status updates.

---

## API Endpoints

All endpoints are served under the test-runner service base URL (typically `http://localhost:5060` in local dev, or resolved via Aspire service discovery).

### Discover Tests

```
GET /api/tests/discover
```

Lists all available test cases from the configured solution or project path.

**Response:**
```json
{
  "tests": [
    "IssuePit.Tests.Unit.SomeTest.ShouldPass",
    "IssuePit.Tests.Unit.AnotherTest.ShouldCalculate",
    "IssuePit.Tests.Integration.ApiTests.CanCreateProject"
  ],
  "error": null
}
```

### Start a Test Run

```
POST /api/tests/run
Content-Type: application/json

{
  "filter": "FullyQualifiedName~SomeTest",
  "project": null
}
```

Both `filter` and `project` are optional. When omitted, all tests in the configured solution are executed.

- `filter` — a [dotnet test filter expression](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests)
- `project` — path to a specific test project (defaults to the solution)

**Response (202 Accepted):**
```json
{
  "id": "a1b2c3d4-...",
  "status": "Pending",
  "filter": "FullyQualifiedName~SomeTest",
  "startedAt": "2025-01-15T10:30:00Z",
  "finishedAt": null,
  "totalTests": 0,
  "passedTests": 0,
  "failedTests": 0,
  "skippedTests": 0
}
```

### List Runs

```
GET /api/tests/runs
```

Returns all recent test runs, newest first.

### Get Run Details

```
GET /api/tests/runs/{runId}
```

Returns full details of a specific run, including parsed test case results when the run has completed.

**Response:**
```json
{
  "id": "a1b2c3d4-...",
  "status": "Completed",
  "filter": "FullyQualifiedName~SomeTest",
  "projectPath": "/path/to/IssuePit.slnx",
  "startedAt": "2025-01-15T10:30:00Z",
  "finishedAt": "2025-01-15T10:30:45Z",
  "exitCode": 0,
  "totalTests": 5,
  "passedTests": 4,
  "failedTests": 1,
  "skippedTests": 0,
  "durationMs": 45123.0,
  "testCases": [
    {
      "fullName": "IssuePit.Tests.Unit.SomeTest.ShouldPass",
      "className": "IssuePit.Tests.Unit.SomeTest",
      "methodName": "ShouldPass",
      "outcome": "Passed",
      "durationMs": 120.5,
      "errorMessage": null,
      "stackTrace": null
    }
  ]
}
```

### Get Run Output

```
GET /api/tests/runs/{runId}/output
```

Returns the raw console output captured during the test run.

---

## Configuration

The test runner automatically discovers the solution file (`IssuePit.slnx`) by walking up from its binary directory. To override this, set the `TestRunner:SolutionPath` configuration value:

```json
{
  "TestRunner": {
    "SolutionPath": "/path/to/IssuePit.slnx"
  }
}
```

Or via environment variable:

```
TestRunner__SolutionPath=/path/to/IssuePit.slnx
```

---

## Aspire Integration

The test runner is registered as an Aspire resource in the AppHost:

```csharp
var testRunner = builder.AddProject<Projects.IssuePit_TestRunner>("test-runner")
    .WithHttpHealthCheck("/health", endpointName: "http");
```

The frontend receives the test runner base URL via `NUXT_PUBLIC_TEST_RUNNER_BASE`.

---

## Adding New Tests

1. Add your test class to any existing test project (e.g. `IssuePit.Tests.Unit`, `IssuePit.Tests.Integration`, `IssuePit.Tests.E2E`).
2. The test runner will automatically discover the new tests via `dotnet test --list-tests`.
3. Use the `POST /api/tests/run` endpoint with a `filter` to run specific tests:
   - By name: `"filter": "FullyQualifiedName~MyNewTest"`
   - By category: `"filter": "Category=Smoke"`
   - By namespace: `"filter": "FullyQualifiedName~IssuePit.Tests.Unit"`

---

## Environment Support

The test runner works in both development and production environments:

- **Development** — automatically discovers the solution via directory traversal; CORS allows any origin.
- **Production** — requires explicit `TestRunner:SolutionPath` configuration; CORS restricted to loopback addresses.
