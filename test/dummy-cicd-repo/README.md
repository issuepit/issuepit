# dummy-cicd-repo

A minimal Git repository used to test the IssuePit CI/CD pipeline end-to-end.

## Workflow

The workflow in `.github/workflows/ci.yml` consists of two jobs:

- **build** — writes a build artifact (`build-output.txt`) and uploads it via `actions/upload-artifact`.
- **test** — generates a minimal `.trx` test-result file and uploads it via `actions/upload-artifact`.

## Usage in E2E tests

When running with the `NativeCiCdRuntime`, reference this repo using the `file://` protocol:

```
file:///path/to/test/dummy-cicd-repo
```

In the `DryRunCiCdRuntime` (used during automated E2E tests), the runtime simulates the same
artifact and TRX output so the parsing and storage pipeline can be verified without Docker/act.
