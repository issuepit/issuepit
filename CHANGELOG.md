# Changelog

## [1.3.0](https://github.com/issuepit/issuepit/compare/v1.2.0...v1.3.0) (2026-03-02)


### Features

* add public SVG status badge endpoint and project badges info page ([#259](https://github.com/issuepit/issuepit/issues/259)) ([0164978](https://github.com/issuepit/issuepit/commit/0164978c90c43570849026239632d80a1eb8a18d))
* code review — changed files sidebar + auto-compare on branch change ([#240](https://github.com/issuepit/issuepit/issues/240)) ([01cb696](https://github.com/issuepit/issuepit/commit/01cb69627fc9feb83e6bd54a03c3a5b2929b2d52))
* enhance code review comments data format with CodeReviewComment entity ([#235](https://github.com/issuepit/issuepit/issues/235)) ([efd578d](https://github.com/issuepit/issuepit/commit/efd578dc07ef7dadab7384583af623991f14a4e2))
* global runs page for all agent and CI/CD runs across tenant ([#223](https://github.com/issuepit/issuepit/issues/223)) ([52045e3](https://github.com/issuepit/issuepit/commit/52045e34fc41a9ec23c1118200c1f585cd3f8dcd))
* improve user docs — releases page, developer category, image lightbox, screenshot workflow ([#238](https://github.com/issuepit/issuepit/issues/238)) ([dd4573b](https://github.com/issuepit/issuepit/commit/dd4573be429bf7fa0da07dffa2da2a104e30cc94))
* live sync for runs page via SignalR ([#247](https://github.com/issuepit/issuepit/issues/247)) ([50d183b](https://github.com/issuepit/issuepit/commit/50d183bdc8f17b54e7c91ce7e02cb722224c1c55))
* milestones ([#224](https://github.com/issuepit/issuepit/issues/224)) ([86d0f7a](https://github.com/issuepit/issuepit/commit/86d0f7a7e4647df6ad022bb40bb2811b870a557b))
* redesign project dashboard ([#229](https://github.com/issuepit/issuepit/issues/229)) ([2a70040](https://github.com/issuepit/issuepit/commit/2a70040752b424f47aaea785e5620bf7264807bc))
* replace helper-act base image with Docker CLI + official act install script; set as default CI/CD container ([#244](https://github.com/issuepit/issuepit/issues/244)) ([4f91c3f](https://github.com/issuepit/issuepit/commit/4f91c3f987d6b1710a38e92c20a5169adda1c1e4))
* screenshot similarity checker for docs auto-update workflow ([#262](https://github.com/issuepit/issuepit/issues/262)) ([dbb1aef](https://github.com/issuepit/issuepit/commit/dbb1aef8bb8f44257464b141af9817f3c0f57c9f))
* sidebar section state persists per browser tab with cross-session fallback ([#231](https://github.com/issuepit/issuepit/issues/231)) ([d15cd05](https://github.com/issuepit/issuepit/commit/d15cd05822886e9103521c759cb05c8d4b95cd43))


### Bug Fixes

* add `workflows: write` permission to release-please workflow ([#243](https://github.com/issuepit/issuepit/issues/243)) ([b7f5d8a](https://github.com/issuepit/issuepit/commit/b7f5d8a34efba8bcf5a413f066f6d5c7cc05b4f4))
* add verbose debug logging for agent session runs ([#248](https://github.com/issuepit/issuepit/issues/248)) ([7342b97](https://github.com/issuepit/issuepit/commit/7342b97e642a4a7b14774825be182aa6a0c8fc12))
* code review page — split view overflow, line comments, large file lazy loading, localStorage persistence, issue workflow, multi-line comments, and UX improvements ([#213](https://github.com/issuepit/issuepit/issues/213)) ([35d1de7](https://github.com/issuepit/issuepit/commit/35d1de733da8e6ea19b8c5f37b59e51dfd506b82))
* correct invalid GitHub Actions workflow syntax in release-please and helper-containers ([#246](https://github.com/issuepit/issuepit/issues/246)) ([efa4972](https://github.com/issuepit/issuepit/commit/efa4972e7e2050e5a4592c83109d4e0d031279f7))
* flaky E2E test timeout on SPA navigation ([#245](https://github.com/issuepit/issuepit/issues/245)) ([e071878](https://github.com/issuepit/issuepit/commit/e0718780e760450dbe15846489ca7d0214b7b993))
* image tag of docker act image ([998acd6](https://github.com/issuepit/issuepit/commit/998acd6cf53db349756587a8a2da974399cf585c))
* move helper Dockerfiles into docker/helper-containers/ so release-please tracks them ([#252](https://github.com/issuepit/issuepit/issues/252)) ([0b628b8](https://github.com/issuepit/issuepit/commit/0b628b885c88c71e86427b3e0b728dd83d5a50b7))
* preserve workspace path on CI/CD run retry, improve Docker diagnostics, and add advanced run options ([#219](https://github.com/issuepit/issuepit/issues/219)) ([51e1d50](https://github.com/issuepit/issuepit/commit/51e1d507647b25976955e67ba4b99e9fc849e099))
* **sidebar:** load data for lazy sections restored as open; add collapsible sidebar ([#241](https://github.com/issuepit/issuepit/issues/241)) ([e6d3953](https://github.com/issuepit/issuepit/commit/e6d395354b781b182fdc27c33f5a0459343988e0))
* update Playwright Docker image tag from non-existent v1.50.1 to v1.51.0 ([#258](https://github.com/issuepit/issuepit/issues/258)) ([98bc5fe](https://github.com/issuepit/issuepit/commit/98bc5fe92810ce5b714cd9ff707c16a73a79ef0f))

## [1.2.0](https://github.com/issuepit/issuepit/compare/v1.1.0...v1.2.0) (2026-03-01)


### Features

* add code review UI with side-by-side diff viewer and inline comments ([#161](https://github.com/issuepit/issuepit/issues/161)) ([bc2e800](https://github.com/issuepit/issuepit/commit/bc2e8004a02ea39441518dd86d87ad1799f5dbd9))
* add MCP Servers and MCP Playground to sidebar; show auto-linked IssuePit MCP in agent detail ([#160](https://github.com/issuepit/issuepit/issues/160)) ([2a9175a](https://github.com/issuepit/issuepit/commit/2a9175a8f5b4c47fbfc9de4b950c2b3f986cffa0))
* add pgAdmin, Kafka UI, and Redis Insight as manually-started Aspire containers ([#158](https://github.com/issuepit/issuepit/issues/158)) ([e790552](https://github.com/issuepit/issuepit/commit/e7905521af985478a8a296a725c0d29de0ffeb45))
* auto-trigger CI/CD on git repo link, poll for new commits, and Kafka-based run cancellation ([#141](https://github.com/issuepit/issuepit/issues/141)) ([89041da](https://github.com/issuepit/issuepit/commit/89041da15f0051d931ffe1cc2b08249f568f494b))
* CI/CD retry button and runner options per project/org ([#193](https://github.com/issuepit/issuepit/issues/193)) ([a01a153](https://github.com/issuepit/issuepit/commit/a01a1531bf406f6b53cad89980d7e06ea76b65d8))
* clickable run detail pages for CI/CD runs and agent sessions ([#189](https://github.com/issuepit/issuepit/issues/189)) ([ec5b744](https://github.com/issuepit/issuepit/commit/ec5b7448a8ecc5108f9e2c3873e832ebf842229c))
* code viewer — line numbers, markdown preview, and code review sessions ([#129](https://github.com/issuepit/issuepit/issues/129)) ([b2e6467](https://github.com/issuepit/issuepit/commit/b2e646716179aeacc43eb6c791483269ffc06d69))
* enhance issues via OpenRouter LLM with MCP server proxy and scoped API keys ([#175](https://github.com/issuepit/issuepit/issues/175)) ([00f7021](https://github.com/issuepit/issuepit/commit/00f70213c06c38b25ae511bc712529aed0580249))
* extend dashboard with cross-project data, runs section, and issue history chart ([#143](https://github.com/issuepit/issuepit/issues/143)) ([f3ae9b6](https://github.com/issuepit/issuepit/commit/f3ae9b6d05fed6af37c3e9d1629f27db5a4ee6e1))
* extend issue detail page ([#138](https://github.com/issuepit/issuepit/issues/138)) ([af012fc](https://github.com/issuepit/issuepit/commit/af012fc121876f9fee48ba8f6fdf4e0041b9bbdb))
* extend MCP server with non-destructive mode, agent mode, task tools, CI/CD tools, playground UI, and migration fixes ([#104](https://github.com/issuepit/issuepit/issues/104)) ([585f333](https://github.com/issuepit/issuepit/commit/585f3330777dc63744d6c5709ff5d0f099319412))
* extend profile page with teams, GitHub SSO, and sidebar GitHub Identities link ([#111](https://github.com/issuepit/issuepit/issues/111)) ([b240046](https://github.com/issuepit/issuepit/commit/b2400465b1378591ac3f5790474c8ec9a37e82d4))
* extend project page with settings, runs, and org transfer ([#114](https://github.com/issuepit/issuepit/issues/114)) ([0445c7a](https://github.com/issuepit/issuepit/commit/0445c7aa5bcdf34bd0cfaa6aabc85ed9a9772386))
* fix agents page — add agent mode detail/edit page with MCP server linking ([#137](https://github.com/issuepit/issuepit/issues/137)) ([2efd7cf](https://github.com/issuepit/issuepit/commit/2efd7cf30353dd3585fd5afd9923877a59c266d3))
* fix issue description partial-update validation errors and add markdown rendering ([#121](https://github.com/issuepit/issuepit/issues/121)) ([3e0ff1a](https://github.com/issuepit/issuepit/commit/3e0ff1abc9912426aa901e0706f3d7de9c04ea52))
* git auth configuration with automatic repo disable/throttle on failure ([#196](https://github.com/issuepit/issuepit/issues/196)) ([2da02a7](https://github.com/issuepit/issuepit/commit/2da02a7b4e8b9a735eff2c78f34d1d38b46ed350))
* git code manager ([#72](https://github.com/issuepit/issuepit/issues/72)) ([eb58ca4](https://github.com/issuepit/issuepit/commit/eb58ca44bdcde846af9066aaa76298291d40786f))
* helper Docker containers for agent and CI/CD runs ([#152](https://github.com/issuepit/issuepit/issues/152)) ([32dfdc2](https://github.com/issuepit/issuepit/commit/32dfdc2fc7fc23b2043b9a4da23f1c892663786e))
* hourly metric snapshots for projects (issues, agent runs, CI/CD runs) ([#150](https://github.com/issuepit/issuepit/issues/150)) ([a04ca8a](https://github.com/issuepit/issuepit/commit/a04ca8a69f0300b8b7346d93befe88a96f90199d))
* improve MCP pages — native playground, padding fix, combined edit/manage, scoped secrets ([#166](https://github.com/issuepit/issuepit/issues/166)) ([83d9aa2](https://github.com/issuepit/issuepit/commit/83d9aa2f4fd7d90c1442b68704bf69c0446d55fd))
* improve org management — user search, team details page, org projects ([#106](https://github.com/issuepit/issuepit/issues/106)) ([c3f6308](https://github.com/issuepit/issuepit/commit/c3f6308cb70fb58eb6bfb4d459cbe85b21f47fac))
* improve sidebar with collapsible sections, lazy loading, and issue filters ([#116](https://github.com/issuepit/issuepit/issues/116)) ([c6dba92](https://github.com/issuepit/issuepit/commit/c6dba925b37c486e9deaeb6092c9b78072b812a4))
* issue editor improvements ([#99](https://github.com/issuepit/issuepit/issues/99)) ([3f03561](https://github.com/issuepit/issuepit/commit/3f03561ee4059bfdd9277e8e6a82402dd30090bd))
* match scrollbars to dark theme ([#171](https://github.com/issuepit/issuepit/issues/171)) ([4eec89b](https://github.com/issuepit/issuepit/commit/4eec89b79f0ec0a0d104be50ed0c1257b4b202b2))
* push browser history on file/dir navigation in code view ([#182](https://github.com/issuepit/issuepit/issues/182)) ([e69783f](https://github.com/issuepit/issuepit/commit/e69783f4b4f9dfcb24183e0beff0a76f7e94dead))
* replace admin/admin local login with Aspire dashboard magic-link command ([#93](https://github.com/issuepit/issuepit/issues/93)) ([bff1639](https://github.com/issuepit/issuepit/commit/bff1639bb813e7cd69b5f6cb2db67e5e37fbbb54))
* searchable branch dropdown on code review page, include remote branches ([#184](https://github.com/issuepit/issuepit/issues/184)) ([b594aa8](https://github.com/issuepit/issuepit/commit/b594aa8097c00af4f9633a6495e287eda58f5efd))
* syntax highlighting in code browser ([#146](https://github.com/issuepit/issuepit/issues/146)) ([760504d](https://github.com/issuepit/issuepit/commit/760504def901eaaaa3e8aa1db12483affea71bfe))
* trigger agent run when agent is assigned to an existing issue ([#132](https://github.com/issuepit/issuepit/issues/132)) ([0c24368](https://github.com/issuepit/issuepit/commit/0c24368e70b38459bf40e8fdf5cc3baec3d9bb7d))
* user profile page ([#97](https://github.com/issuepit/issuepit/issues/97)) ([ffe1e3a](https://github.com/issuepit/issuepit/commit/ffe1e3a6a4e1a74f9d64276c566152e3e1bf1570))
* visual transition feedback during kanban issue drag ([#172](https://github.com/issuepit/issuepit/issues/172)) ([c6f7e09](https://github.com/issuepit/issuepit/commit/c6f7e098358a5f808d72a0f8f53d696d56209875))


### Bug Fixes

* add http/https launch profiles to execution-client and cicd-client ([#185](https://github.com/issuepit/issuepit/issues/185)) ([1bde183](https://github.com/issuepit/issuepit/commit/1bde1831224c3cbca9dd98f9ff469be35872e86d))
* add missing migration and improve check-pending-migrations CI diagnostics ([#120](https://github.com/issuepit/issuepit/issues/120)) ([71675eb](https://github.com/issuepit/issuepit/commit/71675ebf9a147fe8bc8ac33c63d5271af8b214a2))
* add missing references for execution-client and cicd-client to postgresDb ([#191](https://github.com/issuepit/issuepit/issues/191)) ([88c435f](https://github.com/issuepit/issuepit/commit/88c435fc191a007a840d58f62096e2b43efed7ab))
* admin login redirect and SSR session persistence ([#119](https://github.com/issuepit/issuepit/issues/119)) ([ff32c01](https://github.com/issuepit/issuepit/commit/ff32c0182d472fbaec5a6700b5c9cca18018bc18))
* agent activate/deactivate sends partial PUT payload causing 400 validation error ([#94](https://github.com/issuepit/issuepit/issues/94)) ([265332f](https://github.com/issuepit/issuepit/commit/265332ffcef91a29bbc25226bcdd006bd5538333))
* code viewer page scroll + sticky dir tree + taller issue description editor ([#154](https://github.com/issuepit/issuepit/issues/154)) ([5eea0ed](https://github.com/issuepit/issuepit/commit/5eea0ed5c4e16469ff73b4ec3bd7def8fe167323))
* compute issueCount and memberCount in project API responses ([#177](https://github.com/issuepit/issuepit/issues/177)) ([7b3121b](https://github.com/issuepit/issuepit/commit/7b3121b2393eda950e3924e2b26810aa777c3d80))
* correct Kafka bootstrap servers for API and add Kafka health check ([#169](https://github.com/issuepit/issuepit/issues/169)) ([6eccc36](https://github.com/issuepit/issuepit/commit/6eccc36f44a15b8629b9bcaf193cd5854f9567f2))
* declare missing `hasMoreCommits` ref in git store ([#112](https://github.com/issuepit/issuepit/issues/112)) ([ce13609](https://github.com/issuepit/issuepit/commit/ce136099c4a0587f49c00d84964385102abf76c4))
* kanban lane reorder, transition-gated issue drops, and no-transition alert ([#102](https://github.com/issuepit/issuepit/issues/102)) ([6c3f8bc](https://github.com/issuepit/issuepit/commit/6c3f8bccc638f0891689cdb75e895428063ec057))
* log full exception (inner exceptions + stack trace) in CI/CD run logs ([#192](https://github.com/issuepit/issuepit/issues/192)) ([23340d5](https://github.com/issuepit/issuepit/commit/23340d5e4947bccd8c13b3d2c1b04cddc2631c34))
* make dashboard stat cards and recent issues clickable with pre-filtered navigation ([#117](https://github.com/issuepit/issuepit/issues/117)) ([feab512](https://github.com/issuepit/issuepit/commit/feab5126a73fafcd9ccd6c5865110bda60dc8e06))
* MCP playground CORS error and config tabs wrapping ([#183](https://github.com/issuepit/issuepit/issues/183)) ([f13c418](https://github.com/issuepit/issuepit/commit/f13c418e582cc6f962770ca90d8d6104cb33dbf7))
* migrator remove raw sql ([#125](https://github.com/issuepit/issuepit/issues/125)) ([fb95e70](https://github.com/issuepit/issuepit/commit/fb95e70b605a1c0853937b2e207f0b38b202c9ab))
* org page role dropdown, project members autocomplete, seed demo users, org/team E2E tests ([#139](https://github.com/issuepit/issuepit/issues/139)) ([188dee6](https://github.com/issuepit/issuepit/commit/188dee6048a6d3e7a07fefbf814f26ad4eebcf10))
* prevent duplicate lanes when creating kanban columns ([#91](https://github.com/issuepit/issuepit/issues/91)) ([ad59cad](https://github.com/issuepit/issuepit/commit/ad59cade07a37afd5a25084288597a4eb6ad4830))
* remove duplicate task endpoints from IssuesController causing ambiguous route match ([#130](https://github.com/issuepit/issuepit/issues/130)) ([d42269d](https://github.com/issuepit/issuepit/commit/d42269de38af1284284381f1cb24cf2a8f8d2284))
* replace Aspire command with frontend URL for admin magic login ([#108](https://github.com/issuepit/issuepit/issues/108)) ([3dc4cf9](https://github.com/issuepit/issuepit/commit/3dc4cf974c3a76393c0f4de1c6a93b5937839099))
* resolve 400 "Project field is required" on kanban lane issue creation ([#100](https://github.com/issuepit/issuepit/issues/100)) ([5f2cc53](https://github.com/issuepit/issuepit/commit/5f2cc53d31e1420dddf4cdedfe0b8f435cbf2e3f))
* serialize concurrent git operations per repository ([#127](https://github.com/issuepit/issuepit/issues/127)) ([199af9f](https://github.com/issuepit/issuepit/commit/199af9fb837c2f21dfd8c0937273dd3f7ad6e9d2))
* show GitHub URL and git remote URL separately in project settings ([#134](https://github.com/issuepit/issuepit/issues/134)) ([e02d07a](https://github.com/issuepit/issuepit/commit/e02d07ab721c8deb65701d0c7f8933d4edd59884))
* use Aspire-provided connection string for Kafka, refactor health checks to service defaults ([#181](https://github.com/issuepit/issuepit/issues/181)) ([0396831](https://github.com/issuepit/issuepit/commit/03968319125136624279b280e17d57568805a18d))

## [1.1.0](https://github.com/issuepit/issuepit/compare/v1.0.0...v1.1.0) (2026-03-01)


### Features

* act-based local CI/CD runner, external sync endpoint, and external run tracking ([#8](https://github.com/issuepit/issuepit/issues/8)) ([4589629](https://github.com/issuepit/issuepit/commit/4589629301a68bbfd4575c3714688c7de80360dd))
* add Aspire migrator CLI project for EF Core migrations & seed ([#40](https://github.com/issuepit/issuepit/issues/40)) ([1102bde](https://github.com/issuepit/issuepit/commit/1102bde0ac9264e7fe8fc2f1eaa03cc7f76b8c44))
* add MCP server to control IssuePit API ([#23](https://github.com/issuepit/issuepit/issues/23)) ([b778ab9](https://github.com/issuepit/issuepit/commit/b778ab9b48c12d2d24fde377de55f2fa6a82979a))
* add Scalar OpenAPI spec viewer with Aspire dashboard link ([#75](https://github.com/issuepit/issuepit/issues/75)) ([229bb45](https://github.com/issuepit/issuepit/commit/229bb45f96dc00f738814cc1fb50b0b6289260e7))
* add team management UI ([#45](https://github.com/issuepit/issuepit/issues/45)) ([e290dee](https://github.com/issuepit/issuepit/commit/e290dee7abaef5f42657839023540bd6712803c6))
* add team, org, and user management with project permissions ([#21](https://github.com/issuepit/issuepit/issues/21)) ([7fae6bf](https://github.com/issuepit/issuepit/commit/7fae6bf76b7f540e48241a4114239f48e52968a8))
* add Vue3 (Nuxt) frontend to Aspire AppHost orchestration ([#10](https://github.com/issuepit/issuepit/issues/10)) ([8efd812](https://github.com/issuepit/issuepit/commit/8efd812fd9de5f562333b1f60d09b04305066fb6))
* enhance agent workflow documentation for PR handling and clarity ([#28](https://github.com/issuepit/issuepit/issues/28)) ([37fcb58](https://github.com/issuepit/issuepit/commit/37fcb588bc8d0bccd49a8583071806d3d0f0e85e))
* execution client agent orchestrator with multi-runtime support ([#7](https://github.com/issuepit/issuepit/issues/7)) ([6794c81](https://github.com/issuepit/issuepit/commit/6794c81493cd2028336f42b07b3c7a22595ac2e5))
* GitHub identities management — PAT creation, agent/project/org mapping ([#60](https://github.com/issuepit/issuepit/issues/60)) ([60abb44](https://github.com/issuepit/issuepit/commit/60abb44b7318ff22fad88f3aade869af6cade273))
* GitHub SSO login with encrypted token storage and agent token retrieval ([#47](https://github.com/issuepit/issuepit/issues/47)) ([e3cc467](https://github.com/issuepit/issuepit/commit/e3cc4676d39f6ad46ca399ec8b07855e1ef8c949))
* happy path UI e2e tests + fix issue enum serialization ([#77](https://github.com/issuepit/issuepit/issues/77)) ([91ae3a9](https://github.com/issuepit/issuepit/commit/91ae3a90c34df9598b793429f95589e6961a8786))
* integrate openCode CLI runner with model selection ([#69](https://github.com/issuepit/issuepit/issues/69)) ([7994336](https://github.com/issuepit/issuepit/commit/799433642ea2d9b839d00ec9081c05c3ee12f698))
* kanban transitions — multiple boards, lane management, agent-triggered transitions ([#6](https://github.com/issuepit/issuepit/issues/6)) ([2f730c4](https://github.com/issuepit/issuepit/commit/2f730c4d4e68b950ac576b7dafa149d19c88ad7f))
* local user account login, registration, and admin user management ([#66](https://github.com/issuepit/issuepit/issues/66)) ([78357d4](https://github.com/issuepit/issuepit/commit/78357d4c6ed03d797aa84e3a2de08fd6a0d9c261))
* MCP server manager ([#78](https://github.com/issuepit/issuepit/issues/78)) ([6eab5b4](https://github.com/issuepit/issuepit/commit/6eab5b4c61974b20271f99fb804a11595e75dcf0))
* telegram notifications ([#83](https://github.com/issuepit/issuepit/issues/83)) ([a38ad55](https://github.com/issuepit/issuepit/commit/a38ad557bad892628aaae19096204090c566b014))
* Tenant Manager — CRUD API, auto PostgreSQL provisioning, and admin UI ([#19](https://github.com/issuepit/issuepit/issues/19)) ([77fa49d](https://github.com/issuepit/issuepit/commit/77fa49d6403cd2db69859b868238ada342e7d1c4))


### Bug Fixes

* add launchSettings.json for project configuration and environment variables ([#34](https://github.com/issuepit/issuepit/issues/34)) ([4511f92](https://github.com/issuepit/issuepit/commit/4511f921e9d245acc52458d272e8dc605738d075))
* enhance CORS policy for dynamic local origins during development ([#51](https://github.com/issuepit/issuepit/issues/51)) ([5caa012](https://github.com/issuepit/issuepit/commit/5caa0127207944a9f9ffe76a3734f5afd7868b17))
* frontend CORS issue ([#43](https://github.com/issuepit/issuepit/issues/43)) ([4943b61](https://github.com/issuepit/issuepit/commit/4943b619f74a0daee7d62f364fb83551573027ac))
* missing closing paren on telegram_bots CREATE TABLE causes migrator crash ([#89](https://github.com/issuepit/issuepit/issues/89)) ([d3b80fa](https://github.com/issuepit/issuepit/commit/d3b80fa89748d29bffba74e64ae5073b059f822d))
* replace addgroup/adduser with groupadd/useradd in .NET 10 Dockerfiles ([#12](https://github.com/issuepit/issuepit/issues/12)) ([1fd128c](https://github.com/issuepit/issuepit/commit/1fd128ca6adeab023c8a641e5afe4487ab0f6bfd))
* use default tenant if tenants table is unavailable ([#58](https://github.com/issuepit/issuepit/issues/58)) ([61c89da](https://github.com/issuepit/issuepit/commit/61c89da74e1be212b582c2f9af297941e6a301df))

## 1.0.0 (2026-02-28)


### Features

* Aspire 13 scaffold – backend, frontend, Redis, SignalR, CI/CD streaming, Docker, tests ([#1](https://github.com/issuepit/issuepit/issues/1)) ([4913022](https://github.com/issuepit/issuepit/commit/4913022963d4d777dc4afa5890c48cebbf2fde07))


### Bug Fixes

* remove redundant `IsAspireHost` property causing AppHost startup hang ([#9](https://github.com/issuepit/issuepit/issues/9)) ([66f3497](https://github.com/issuepit/issuepit/commit/66f3497f81eaf2d99af75e943178a01c4b46e9a1))
