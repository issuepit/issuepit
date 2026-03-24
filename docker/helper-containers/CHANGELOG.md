# Changelog

## [1.7.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.6.1...helper-containers-v1.7.0) (2026-03-23)


### Features

* update issuepit/act to a471da1 with stage-aware skip-step logging ([#796](https://github.com/issuepit/issuepit/issues/796)) ([6ad5ab5](https://github.com/issuepit/issuepit/commit/6ad5ab50bc3d6b45708c0639f6363d84fcc45b50))

## [1.6.1](https://github.com/issuepit/issuepit/compare/helper-containers-v1.6.0...helper-containers-v1.6.1) (2026-03-23)


### Bug Fixes

* cicd skip-steps — settings lost on reload, wizard pre-populate, execution order, e2e tests, act version bump ([#771](https://github.com/issuepit/issuepit/issues/771)) ([9e6600f](https://github.com/issuepit/issuepit/commit/9e6600faad6a4ce60ef8a2367a3ef8d704147799))

## [1.6.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.5.0...helper-containers-v1.6.0) (2026-03-22)


### Features

* per-repo agent push policy (Forbidden/WorkingOriginOnly/Allowed/Yolo) ([#706](https://github.com/issuepit/issuepit/issues/706)) ([1e9ac20](https://github.com/issuepit/issuepit/commit/1e9ac20e3dd50a48423a81b806823ccb3f642e77))


### Bug Fixes

* docker helper image and agent execution ([#759](https://github.com/issuepit/issuepit/issues/759)) ([c3dce69](https://github.com/issuepit/issuepit/commit/c3dce698ec5949a75b66dc9debb831836b4b907c))

## [1.5.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.4.0...helper-containers-v1.5.0) (2026-03-19)


### Features

* add Docker Engine to issuepit-act-runner image for dind support ([#708](https://github.com/issuepit/issuepit/issues/708)) ([b5e2332](https://github.com/issuepit/issuepit/commit/b5e23326cbfe727b520651fccb8ad4c035fafcfd))

## [1.4.0](https://github.com/issuepit/issuepit/compare/helper-containers-v1.3.0...helper-containers-v1.4.0) (2026-03-18)


### Features

* add opencode agent types (primary/subagent/all) to nested agents ([#578](https://github.com/issuepit/issuepit/issues/578)) ([bffd42a](https://github.com/issuepit/issuepit/commit/bffd42ae06082e8fdfa29a75499b207126e96e23))
* issuepit-act-runner image (ffmpeg + jq + Chrome) and inherited image UI ([#691](https://github.com/issuepit/issuepit/issues/691)) ([6e4b4db](https://github.com/issuepit/issuepit/commit/6e4b4db86e0da18cfce87eddf9a25a0cfb2954ea))
* opencode HTTP server execution mode ([#561](https://github.com/issuepit/issuepit/issues/561)) ([5c8ff2c](https://github.com/issuepit/issuepit/commit/5c8ff2c63843614cd225fd77ecb657ea3e65a191))
* opencode login for OpenRouter/DeepSeek + MCP config passthrough (GitHub MCP, Context7) ([#596](https://github.com/issuepit/issuepit/issues/596)) ([0371d06](https://github.com/issuepit/issuepit/commit/0371d0682ac3ea752850a14823bab6bae1d4298c))
* opencode plugin to block `git push` ([#559](https://github.com/issuepit/issuepit/issues/559)) ([6928c6d](https://github.com/issuepit/issuepit/commit/6928c6d3386c6c9eccd14ac075bdff6fc5153cb5))


### Bug Fixes

* add apt-get update before ffmpeg install in E2E CI job ([#649](https://github.com/issuepit/issuepit/issues/649)) ([de40dd9](https://github.com/issuepit/issuepit/commit/de40dd9be6502657dca1d016d985ba50c3754321))
* agent container names ([#696](https://github.com/issuepit/issuepit/issues/696)) ([af6c836](https://github.com/issuepit/issuepit/commit/af6c836faed2ad30b9c39335079070aa63d2538b))
* agent startup — non-fatal dockerd, container health check, OPENCODE_PORT, CRLF injection fix ([#618](https://github.com/issuepit/issuepit/issues/618)) ([756d5f8](https://github.com/issuepit/issuepit/commit/756d5f84927e80b6f05b1303c72e775e9fad27f3))
* fail fast with clear error for misconfigured DefaultBranch + pre-flight remote branch check + stop stderr leaking into captured git output ([#679](https://github.com/issuepit/issuepit/issues/679)) ([4b651cd](https://github.com/issuepit/issuepit/commit/4b651cdf4321f0b6752820c7bc6de841c4b32d9a))
* pass git auth credentials to CICD clone and bypass push-blocking wrapper for agent push ([#642](https://github.com/issuepit/issuepit/issues/642)) ([08d8cea](https://github.com/issuepit/issuepit/commit/08d8ceaa3e1feaae5cdfe4d99dcc4cbebaceb9d6))
* restore exec "$@" in entrypoint.sh, add UseHttpServer toggle to agent form, and extend retry modal ([#599](https://github.com/issuepit/issuepit/issues/599)) ([ef69896](https://github.com/issuepit/issuepit/commit/ef6989655d0511de8e531e749fb39e0ce3942ab0))
* use "remote" MCP type instead of invalid "sse"/"http" for opencode config ([#638](https://github.com/issuepit/issuepit/issues/638)) ([4bdf59b](https://github.com/issuepit/issuepit/commit/4bdf59be71c70a5647e93a7ad90af4eb4010531b))

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
