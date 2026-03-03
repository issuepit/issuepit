#!/usr/bin/env bash
# Run the IssuePit E2E test suite.
#
# Usage:
#   ./skills/write-e2e-test/scripts/run-e2e-tests.sh [<filter>]
#
# Arguments:
#   filter  Optional dotnet-test --filter expression (default: "Category=E2E")
#
# Environment:
#   DOTNET_VERSION  .NET SDK version to expect (informational only)
#   FRONTEND_URL    Override the frontend URL (used when Aspire cannot start a
#                   dev server, e.g. in environments without Node.js)
#
# The script must be run from the repository root.

set -euo pipefail

FILTER="${1:-Category=E2E}"
PROJECT="src/IssuePit.Tests.E2E/IssuePit.Tests.E2E.csproj"

echo "==> Building E2E test project..."
dotnet build "$PROJECT" --configuration Release

echo "==> Installing Playwright browsers..."
PLAYWRIGHT_SCRIPT=$(find src/IssuePit.Tests.E2E/bin/Release -name 'playwright.ps1' | head -1)
if [ -n "$PLAYWRIGHT_SCRIPT" ]; then
    pwsh "$PLAYWRIGHT_SCRIPT" install --with-deps chromium
else
    echo "WARNING: playwright.ps1 not found — skipping browser install"
fi

echo "==> Running E2E tests (filter: $FILTER)..."
dotnet test "$PROJECT" \
    --no-build \
    --configuration Release \
    --filter "$FILTER" \
    --verbosity normal \
    --logger "trx;LogFileName=e2e-results.trx" \
    --blame-hang-timeout 5min
