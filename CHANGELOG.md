# Changelog

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
