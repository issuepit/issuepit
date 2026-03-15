# Changelog

## [1.4.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.3.0...helper-containers-v1.4.0) (2026-03-15)


### Features

* opencode plugin to block `git push` ([#559](https://github.com/issuepit/issuepit/issues/559)) ([6928c6d](https://github.com/issuepit/issuepit/commit/6928c6d3386c6c9eccd14ac075bdff6fc5153cb5))

## [1.3.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.2.4...helper-containers-v1.3.0) (2026-03-15)


### Features

* improve agent action run flow — git branch setup, CI/CD loop, same-container exec, opencode session forking ([#512](https://github.com/issuepit/issuepit/issues/512)) ([c5a3af8](https://github.com/issuepit/issuepit/commit/c5a3af8babb2523afd908f311b630a5c37b6c7f2))
* improve agent action runs (MCP injection, nested agents, issue comments, session warnings, git recovery) ([#532](https://github.com/issuepit/issuepit/issues/532)) ([4a156cf](https://github.com/issuepit/issuepit/commit/4a156cffb016cbf463fa44760394bf5179cdd780))


### Bug Fixes

* artifact download routes to Nuxt instead of C# backend; artifacts and tests tabs empty ([#544](https://github.com/issuepit/issuepit/issues/544)) ([346a48f](https://github.com/issuepit/issuepit/commit/346a48f194838090a3ac8ba1a3ff69d8970d8e7a))

## [1.2.4](https://github.com/issuepit/issuepit/compare/helper-containers-v1.2.3...helper-containers-v1.2.4) (2026-03-14)


### Bug Fixes

* install .NET 10 SDK in helper-base image via dotnet-install.sh and bump Playwright to v1.58.0 ([#509](https://github.com/issuepit/issuepit/issues/509)) ([c3b04cb](https://github.com/issuepit/issuepit/commit/c3b04cb990e31252862bd88a02a9a111a67cd9db))

## [1.2.3](https://github.com/issuepit/issuepit/compare/helper-containers-v1.2.2...helper-containers-v1.2.3) (2026-03-09)


### Bug Fixes

* agent runs should match cicd backend logic ([#440](https://github.com/issuepit/issuepit/issues/440)) ([3385b06](https://github.com/issuepit/issuepit/commit/3385b068ec371d7b79221822517a06320e871a5d))

## [1.2.2](https://github.com/issuepit/issuepit/compare/helper-containers-v1.2.1...helper-containers-v1.2.2) (2026-03-08)


### Bug Fixes

* remove dotnet Aspire workload from helper image, pre-pull localstack for E2E ([#418](https://github.com/issuepit/issuepit/issues/418)) ([073291f](https://github.com/issuepit/issuepit/commit/073291f6ddef822455e96c0caec4712162e5a1a9))

## [1.2.1](https://github.com/issuepit/issuepit/compare/helper-containers-v1.2.0...helper-containers-v1.2.1) (2026-03-04)


### Bug Fixes

* true isolated DinD, gitRepoUrl instead of workspacePath, TriggerPayload build fix, dockerd in helper image, workflow graph via cat from clone, WorkflowGraphParser string API ([#350](https://github.com/issuepit/issuepit/issues/350)) ([862d082](https://github.com/issuepit/issuepit/commit/862d082c727c5ecb2cc0eb08af83945bafe0cfe1))

## [1.2.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.1.2...helper-containers-v1.2.0) (2026-03-03)


### Features

* add actionlint to helper-act and create helper-opencode-act combined image ([#288](https://github.com/issuepit/issuepit/issues/288)) ([0731fde](https://github.com/issuepit/issuepit/commit/0731fded4322296ed6705185960a2baf66dbe814))

## [1.1.2](https://github.com/issuepit/issuepit/compare/helper-containers-v1.1.1...helper-containers-v1.1.2) (2026-03-02)


### Bug Fixes

* update Playwright Docker image tag from non-existent v1.50.1 to v1.51.0 ([#258](https://github.com/issuepit/issuepit/issues/258)) ([98bc5fe](https://github.com/issuepit/issuepit/commit/98bc5fe92810ce5b714cd9ff707c16a73a79ef0f))

## [1.1.1](https://github.com/issuepit/issuepit/compare/helper-containers-v1.1.0...helper-containers-v1.1.1) (2026-03-02)


### Bug Fixes

* move helper Dockerfiles into docker/helper-containers/ so release-please tracks them ([#252](https://github.com/issuepit/issuepit/issues/252)) ([0b628b8](https://github.com/issuepit/issuepit/commit/0b628b885c88c71e86427b3e0b728dd83d5a50b7))

## [1.1.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.0.0...helper-containers-v1.1.0) (2026-03-01)


### Features

* helper Docker containers for agent and CI/CD runs ([#152](https://github.com/issuepit/issuepit/issues/152)) ([32dfdc2](https://github.com/issuepit/issuepit/commit/32dfdc2fc7fc23b2043b9a4da23f1c892663786e))
