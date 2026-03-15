# Changelog

## [1.7.0](https://github.com/issuepit/issuepit/compare/v1.6.0...v1.7.0) (2026-03-15)


### Features

* add CI/CD run approval mode to gate auto-start for seeded projects in E2E tests ([#514](https://github.com/issuepit/issuepit/issues/514)) ([1c859e0](https://github.com/issuepit/issuepit/commit/1c859e0a383cec7f44c43d024fbbe60bf987101c))
* detect branches and commits and map them to issues ([#538](https://github.com/issuepit/issuepit/issues/538)) ([f490b84](https://github.com/issuepit/issuepit/commit/f490b84ebe12bf80f281b22635e328eb138cff18))
* expose RequiresRunApproval in CI/CD settings UI; bypass approval for user-triggered runs ([#545](https://github.com/issuepit/issuepit/issues/545)) ([6f6f403](https://github.com/issuepit/issuepit/commit/6f6f40318cf37722c15745fc24fd1991f682d180))
* improve agent action run flow — git branch setup, CI/CD loop, same-container exec, opencode session forking ([#512](https://github.com/issuepit/issuepit/issues/512)) ([c5a3af8](https://github.com/issuepit/issuepit/commit/c5a3af8babb2523afd908f311b630a5c37b6c7f2))
* improve agent action runs (MCP injection, nested agents, issue comments, session warnings, git recovery) ([#532](https://github.com/issuepit/issuepit/issues/532)) ([4a156cf](https://github.com/issuepit/issuepit/commit/4a156cffb016cbf463fa44760394bf5179cdd780))
* link runs — retry chains, agent sessions, same-SHA runs ([#535](https://github.com/issuepit/issuepit/issues/535)) ([b325f17](https://github.com/issuepit/issuepit/commit/b325f17b2b1d3eaabe71a1bbd8a804c37f3937e5))
* split agent run logs into sections with Steps chain view ([#533](https://github.com/issuepit/issuepit/issues/533)) ([48496b4](https://github.com/issuepit/issuepit/commit/48496b41e176d84b62a65a3be2980f036b974d3f))
* sync issues with GitHub ([#527](https://github.com/issuepit/issuepit/issues/527)) ([731dfab](https://github.com/issuepit/issuepit/commit/731dfabd28694cde7d2018353a67c968e96e49f3))


### Bug Fixes

* add status tooltips to agent session status badges ([#524](https://github.com/issuepit/issuepit/issues/524)) ([448ccd0](https://github.com/issuepit/issuepit/commit/448ccd0d859b15630dbac6fd029e21025753dc32))
* agents do not start CLI tool ([#505](https://github.com/issuepit/issuepit/issues/505)) ([907000d](https://github.com/issuepit/issuepit/commit/907000d11f0e4155e5230cdf2840c216682ecd65))
* artifact and test uploads in cicd runner ([#508](https://github.com/issuepit/issuepit/issues/508)) ([4053790](https://github.com/issuepit/issuepit/commit/405379063fdfd40677cb5b955c7635e9aa84b8de))
* artifact download routes to Nuxt instead of C# backend; artifacts and tests tabs empty ([#544](https://github.com/issuepit/issuepit/issues/544)) ([346a48f](https://github.com/issuepit/issuepit/commit/346a48f194838090a3ac8ba1a3ff69d8970d8e7a))
* auto-download Vosk model and ffmpeg in local Aspire runs; unblock voice issue creation on transcription failure ([#540](https://github.com/issuepit/issuepit/issues/540)) ([9633ec0](https://github.com/issuepit/issuepit/commit/9633ec0c28532047eda31cf7e392a6600dd20c48))
* CI/CD status UI — retry trigger, debug mode, slim filter, gap-free layout ([#517](https://github.com/issuepit/issuepit/issues/517)) ([d02eaef](https://github.com/issuepit/issuepit/commit/d02eaef4f72b7806c4e2bd4862edc68f812b1cd7))
* don't close modals when mousedown is inside modal but mouseup is outside ([#501](https://github.com/issuepit/issuepit/issues/501)) ([2f1c0e2](https://github.com/issuepit/issuepit/commit/2f1c0e20d6e2e27a19bcc789d30e893bd587d184))
* install .NET 10 SDK in helper-base image via dotnet-install.sh and bump Playwright to v1.58.0 ([#509](https://github.com/issuepit/issuepit/issues/509)) ([c3b04cb](https://github.com/issuepit/issuepit/commit/c3b04cb990e31252862bd88a02a9a111a67cd9db))
* job tree wrong matching and slim mode nested workflow visibility ([#534](https://github.com/issuepit/issuepit/issues/534)) ([1fe2362](https://github.com/issuepit/issuepit/commit/1fe23626c75bd2a04d8b70bafab5eea28e083417))
* manual CI/CD trigger supports branch as well as commit SHA ([#529](https://github.com/issuepit/issuepit/issues/529)) ([480e81d](https://github.com/issuepit/issuepit/commit/480e81dd33f8dcb419fd55fb43972ecded4a29ba))
* navigate to new issue on create; sort issues list by number numerically ([#530](https://github.com/issuepit/issuepit/issues/530)) ([a12cb41](https://github.com/issuepit/issuepit/commit/a12cb419817732fde82b08d665b1cadee1515bc9))
* stream agent container logs, pin default Docker image tag, and add keep-container option ([#503](https://github.com/issuepit/issuepit/issues/503)) ([275a45f](https://github.com/issuepit/issuepit/commit/275a45f651adef943f4051123ad0ef9fb9ef5691))
* suppress Vosk native logs and [PoC] diagnostic output in e2e CI runs ([#498](https://github.com/issuepit/issuepit/issues/498)) ([c55ec37](https://github.com/issuepit/issuepit/commit/c55ec3749dcaf4bfa9a8566fc89593ea678c4f80))
* WaitingForApproval runs show as Succeeded and lack Approve button ([#521](https://github.com/issuepit/issuepit/issues/521)) ([6f322c6](https://github.com/issuepit/issuepit/commit/6f322c67df3e687e126cf164cfe969fd6a805613))
* WaitingForApproval runs show as Succeeded, approval button never appears, and duration counts incorrectly ([#537](https://github.com/issuepit/issuepit/issues/537)) ([52edac7](https://github.com/issuepit/issuepit/commit/52edac7fb6f07c63567d04f0fd76257288ccc57a))

## [1.6.0](https://github.com/issuepit/issuepit/compare/v1.5.0...v1.6.0) (2026-03-13)


### Features

* allow custom issue ID formats (key prefix + number offset) ([#474](https://github.com/issuepit/issuepit/issues/474)) ([5c3adee](https://github.com/issuepit/issuepit/commit/5c3adee1918c988ce07cf4e7226b757ebac0b7a4))
* attach voice file to voice-created issues, file attachments on issues/comments, retranscription support ([#452](https://github.com/issuepit/issuepit/issues/452)) ([a87f63e](https://github.com/issuepit/issuepit/commit/a87f63e9f4a26e6edcec4826d6dadf585e967333))
* cache apt packages and CI/CD downloads with optional traffic interception ([#392](https://github.com/issuepit/issuepit/issues/392)) ([3abfbf3](https://github.com/issuepit/issuepit/commit/3abfbf34a8b13e0d3354cfb00bf3221387e46b95))
* fix "Complete job" step label and add search hit highlighting in CI/CD log viewer ([#379](https://github.com/issuepit/issuepit/issues/379)) ([6784da6](https://github.com/issuepit/issuepit/commit/6784da6f0a947164360610250671214c6529a9d1))
* hoverable CI/CD status chips with run preview tooltips ([#432](https://github.com/issuepit/issuepit/issues/432)) ([fe3b912](https://github.com/issuepit/issuepit/commit/fe3b9125efc80d75d42a74a0852c0169b25e32cd))
* idempotent data seeding via AddIfNotExistsAsync ([#396](https://github.com/issuepit/issuepit/issues/396)) ([05c7986](https://github.com/issuepit/issuepit/commit/05c7986e2b5c8e8a9a22059a4b7f323bb912a208))
* improve Merge Request page ([#447](https://github.com/issuepit/issuepit/issues/447)) ([3c2f648](https://github.com/issuepit/issuepit/commit/3c2f648e744d7ef1bfc476b9815727aab9f58d5c))
* improve project dashboard ([#403](https://github.com/issuepit/issuepit/issues/403)) ([19770f1](https://github.com/issuepit/issuepit/commit/19770f16a4f2eb87f276440a3c0ab4e305d452ec))
* inject ISSUEPIT_* env vars into act runs and guard credential-dependent workflow steps ([#401](https://github.com/issuepit/issuepit/issues/401)) ([3a4be2f](https://github.com/issuepit/issuepit/commit/3a4be2f1190e29ed729269a9260bbb614edbe6a2))
* milestones UX - quick nav link, clickable rows, detail edit/close, Gantt chart with drag, split view, issue sidebar integration, E2E coverage, user docs ([#482](https://github.com/issuepit/issuepit/issues/482)) ([9380ff9](https://github.com/issuepit/issuepit/commit/9380ff9ca1de2af3ebdf2df4c457b2d172b94baf))
* Minimal merge/pull request workflow ([#416](https://github.com/issuepit/issuepit/issues/416)) ([733e9da](https://github.com/issuepit/issuepit/commit/733e9da1ce049be738ab6d9390fe627de636ef1a))
* multiple git origins per project with mode (ReadOnly/Working/Release) ([#433](https://github.com/issuepit/issuepit/issues/433)) ([60d4a6d](https://github.com/issuepit/issuepit/commit/60d4a6d3f61852770c61e2d83b3c2966485a83f3))
* replace artifact volume mounts with S3 upload ([#411](https://github.com/issuepit/issuepit/issues/411)) ([e8fbe0f](https://github.com/issuepit/issuepit/commit/e8fbe0f508daf0b50000532277c6503bd852c84a))
* searchable multiselect filters on runs page ([#384](https://github.com/issuepit/issuepit/issues/384)) ([b64e864](https://github.com/issuepit/issuepit/commit/b64e8641bba7cf661f80cb07389069855753aa8c))
* show runs on issue page ([#478](https://github.com/issuepit/issuepit/issues/478)) ([d3dda11](https://github.com/issuepit/issuepit/commit/d3dda114667fc183398997bd3829012493e9ec57))
* tasks calendar view improvements ([#428](https://github.com/issuepit/issuepit/issues/428)) ([95162c1](https://github.com/issuepit/issuepit/commit/95162c1db7fd36ddadc6b405cbf2dfc6648dd9d4))
* unify board card drop styling between todo and kanban boards ([#462](https://github.com/issuepit/issuepit/issues/462)) ([2523c8f](https://github.com/issuepit/issuepit/commit/2523c8f3fc027af906c178de9a3530f24d3f9ab0))
* version tags for docker images and act container version config in UI ([#422](https://github.com/issuepit/issuepit/issues/422)) ([fa88d5c](https://github.com/issuepit/issuepit/commit/fa88d5cd114b28669b24ae5cabfab0620edac739))
* voice issue creation tests, WAV fixtures, drag-to-voice, audio attachment retranscription, Aspire Vosk model downloader, CI transcription, file drop zones, and FFMpegCore audio normalization ([#480](https://github.com/issuepit/issuepit/issues/480)) ([9183592](https://github.com/issuepit/issuepit/commit/9183592b5d5c21a25c3bd9c636c524186de42427))


### Bug Fixes

* agent runs should match cicd backend logic ([#440](https://github.com/issuepit/issuepit/issues/440)) ([3385b06](https://github.com/issuepit/issuepit/commit/3385b068ec371d7b79221822517a06320e871a5d))
* agent session retry image options + latest tag not pushed on release ([#464](https://github.com/issuepit/issuepit/issues/464)) ([429108d](https://github.com/issuepit/issuepit/commit/429108d655bc7c03b8477bfc28ded5a67cde9230))
* artifact names show as "1", files cannot be downloaded ([#420](https://github.com/issuepit/issuepit/issues/420)) ([eb51aa2](https://github.com/issuepit/issuepit/commit/eb51aa2c875e63629241f9e487b8e2446eb9f79d))
* artifacts not downloadable and tests not detected in CI/CD run view ([#443](https://github.com/issuepit/issuepit/issues/443)) ([70a9862](https://github.com/issuepit/issuepit/commit/70a98624de9c1ce94e293c96d38aaad33a2d5ce1))
* ArtifactStorageService always read null service URL due to wrong config key separator ([#469](https://github.com/issuepit/issuepit/issues/469)) ([8d790d8](https://github.com/issuepit/issuepit/commit/8d790d86421ab1b102bb9d953dc8b78b715c4a85))
* board drag-and-drop reordering — placeholder position, source visibility, and rank persistence ([#494](https://github.com/issuepit/issuepit/issues/494)) ([d5dc45b](https://github.com/issuepit/issuepit/commit/d5dc45b317ea5937ff63bd42104366bfd3c1ced3))
* calendar go-to-today button, drag color reset, and board drop zone placeholders ([#468](https://github.com/issuepit/issuepit/issues/468)) ([3d22590](https://github.com/issuepit/issuepit/commit/3d225901f7356a424a98327a4187a3d8fc50b0c8))
* calendar recurring events, month drag-drop, weekly card sizing, board drag-drop ([#385](https://github.com/issuepit/issuepit/issues/385)) ([b207367](https://github.com/issuepit/issuepit/commit/b207367b5ab1096331039cfbd018cb7482a6d9b1))
* calendar view — sort stability, "+N more" expand, cross-midnight todos, ISO dates, URL history, modal date format & duration ([#449](https://github.com/issuepit/issuepit/issues/449)) ([57319e8](https://github.com/issuepit/issuepit/commit/57319e831af9eef67b8f30dbe390de1d3c118598))
* cli act args ([#429](https://github.com/issuepit/issuepit/issues/429)) ([7aea761](https://github.com/issuepit/issuepit/commit/7aea761922fa104f9208bf56ab583e8a089a4ff2))
* concurrent run processing, trigger type column, and inputs display ([#397](https://github.com/issuepit/issuepit/issues/397)) ([869ed56](https://github.com/issuepit/issuepit/commit/869ed5648026a0173dced8bcd6eca8c4957a138e))
* dry run env var matching ([#407](https://github.com/issuepit/issuepit/issues/407)) ([112d984](https://github.com/issuepit/issuepit/commit/112d9849f846e6391fdccf71b12f648fd0bcb8e4))
* exclude queue wait time from CI/CD run duration ([#445](https://github.com/issuepit/issuepit/issues/445)) ([2b1156d](https://github.com/issuepit/issuepit/commit/2b1156d6a94652e46936fa9600887d54c53bdb83))
* grant `packages: write` to release-please workflow for reusable workflow call ([#495](https://github.com/issuepit/issuepit/issues/495)) ([4d9ab1c](https://github.com/issuepit/issuepit/commit/4d9ab1cdc79005ce4212f34c3ffc3fc73edc59c3))
* helper-containers tag releases never triggered workflow due to paths filter ([#441](https://github.com/issuepit/issuepit/issues/441)) ([fa55aaa](https://github.com/issuepit/issuepit/commit/fa55aaa0a5edc3a50c9e09039303dd2106acf36f))
* normalize bare workflow filenames to `.github/workflows/` for act ([#378](https://github.com/issuepit/issuepit/issues/378)) ([8ce370d](https://github.com/issuepit/issuepit/commit/8ce370da2c31a0c3c2abc3b622634533aedbf8fd))
* push version and latest tags for helper container images on release ([#455](https://github.com/issuepit/issuepit/issues/455)) ([9533e86](https://github.com/issuepit/issuepit/commit/9533e86895abfa10b236e4c4bd0ac1eb4ef79bcd))
* remove dotnet Aspire workload from helper image, pre-pull localstack for E2E ([#418](https://github.com/issuepit/issuepit/issues/418)) ([073291f](https://github.com/issuepit/issuepit/commit/073291f6ddef822455e96c0caec4712162e5a1a9))
* retry session modal + eliminate page flicker on agent session detail ([#457](https://github.com/issuepit/issuepit/issues/457)) ([a703b85](https://github.com/issuepit/issuepit/commit/a703b8509f0f0c6b146d2443142425a693c83914))
* **skills:** delete confirm modal + full-page creation ([#463](https://github.com/issuepit/issuepit/issues/463)) ([1b66ba1](https://github.com/issuepit/issuepit/commit/1b66ba17e093c7079b350a9ea56b12fb7d76cbe2))
* unify breadcrumb menu sizes across all project pages ([#467](https://github.com/issuepit/issuepit/issues/467)) ([d0cf6aa](https://github.com/issuepit/issuepit/commit/d0cf6aad81f2a1943bd8e020832442397663d8e7))
* unify breadcrumb menus across all pages with PageBreadcrumb component ([#472](https://github.com/issuepit/issuepit/issues/472)) ([5901d9f](https://github.com/issuepit/issuepit/commit/5901d9fa9603d155e50367a45e7a8a0c2408fd15))
* unify breadcrumb navigation across project sub-pages ([#458](https://github.com/issuepit/issuepit/issues/458)) ([1448bc9](https://github.com/issuepit/issuepit/commit/1448bc9a6f2f002cf414c062ba819105fefb5161))
* use vars context for ISSUEPIT_RUN in job-level conditions; log act version ([#415](https://github.com/issuepit/issuepit/issues/415)) ([c651a29](https://github.com/issuepit/issuepit/commit/c651a298fbaca5c5c34870e6966b3d2c435ac764))

## [1.5.0](https://github.com/issuepit/issuepit/compare/v1.4.0...v1.5.0) (2026-03-05)


### Features

* action cache, offline mode, and local repository rerouting for act runner ([#361](https://github.com/issuepit/issuepit/issues/361)) ([3a9761f](https://github.com/issuepit/issuepit/commit/3a9761f716dc9f3269d72046eb834eeb5d021549))
* CI/CD page slim mode, hover highlights, failure propagation, log search ([#360](https://github.com/issuepit/issuepit/issues/360)) ([7282856](https://github.com/issuepit/issuepit/commit/7282856f22813e65efeb95c51c266f57ccae9a7d))
* CICD page — slim mode fixes, artifact list, job status inference, log UX ([#365](https://github.com/issuepit/issuepit/issues/365)) ([699f321](https://github.com/issuepit/issuepit/commit/699f3212b7200b58607b9ba76b450ea0e496471b))
* create issues by parsing voice using Vosk ([#324](https://github.com/issuepit/issuepit/issues/324)) ([60bca7e](https://github.com/issuepit/issuepit/commit/60bca7e7af2af9fec74d04b4b407f998ad0641a5))
* dynamic CI/CD job graph layout via ResizeObserver ([#367](https://github.com/issuepit/issuepit/issues/367)) ([a9164e2](https://github.com/issuepit/issuepit/commit/a9164e27b9c00a12c9fcc267d8b8965e9243638b))
* improve run UI — mixed tab, filters, project column, agent run navigation ([#374](https://github.com/issuepit/issuepit/issues/374)) ([99f0fb4](https://github.com/issuepit/issuepit/commit/99f0fb4ac05b6601c554541027c60c5117ab3e9f))
* improve todos — weekly calendar, drag & drop, iCal subscription, seed data ([#344](https://github.com/issuepit/issuepit/issues/344)) ([ece7241](https://github.com/issuepit/issuepit/commit/ece724145c70247a8030c2ff0cc6ed2cfbafaee7))
* issue viewer — delete confirmation, slug/number URL routing, fix actual deletion ([#372](https://github.com/issuepit/issuepit/issues/372)) ([3e8bab4](https://github.com/issuepit/issuepit/commit/3e8bab48bd5c3097bc4d4625fc307a6c86bb4796))
* limit --concurrent-jobs to 4 by default, configurable per org and project ([#358](https://github.com/issuepit/issuepit/issues/358)) ([9ecd3f2](https://github.com/issuepit/issuepit/commit/9ecd3f2065c349259fd80743913c3866c793f95a))
* persistent npm and NuGet package caching for CI/CD runs ([#357](https://github.com/issuepit/issuepit/issues/357)) ([0c4e198](https://github.com/issuepit/issuepit/commit/0c4e198a07fa4eb50522f45cb9c27e398759d7cb))
* trigger CI/CD runs from commit list with extended event type support ([#343](https://github.com/issuepit/issuepit/issues/343)) ([dffcc5d](https://github.com/issuepit/issuepit/commit/dffcc5d694c770685c927043b500380e416bb0d7))


### Bug Fixes

* CI/CD run page – selection reset, dynamic layout, mapping, matrix UX, step 0, step collapse, unmatched log warning, unit tests ([#337](https://github.com/issuepit/issuepit/issues/337)) ([c24b74a](https://github.com/issuepit/issuepit/commit/c24b74a3b35120a65ebae7fa33bcbf7a131d80c5))
* cicd page interaction polish — hover blur, arrow sizing, job completion, matrix template resolution ([#368](https://github.com/issuepit/issuepit/issues/368)) ([d6618d4](https://github.com/issuepit/issuepit/commit/d6618d4828753cc159d1bdd6281d574446d08b27))
* embed Verdaccio config as inline C# string instead of host bind mount ([#369](https://github.com/issuepit/issuepit/issues/369)) ([8985c29](https://github.com/issuepit/issuepit/commit/8985c295a66f18b06481eccc5a98a38f7da95182))
* linked issues — bidirectional visibility, searchable picker, sub-issue linking ([#345](https://github.com/issuepit/issuepit/issues/345)) ([883cbc0](https://github.com/issuepit/issuepit/commit/883cbc0a8c3e8f2f6c6ade8ae0ec3c819ad3e3d6))
* true isolated DinD, gitRepoUrl instead of workspacePath, TriggerPayload build fix, dockerd in helper image, workflow graph via cat from clone, WorkflowGraphParser string API ([#350](https://github.com/issuepit/issuepit/issues/350)) ([862d082](https://github.com/issuepit/issuepit/commit/862d082c727c5ecb2cc0eb08af83945bafe0cfe1))
* ui orgs and project settings ([#335](https://github.com/issuepit/issuepit/issues/335)) ([5033e1e](https://github.com/issuepit/issuepit/commit/5033e1eae899691ddf827917ca3ac586dae37673))

## [1.4.0](https://github.com/issuepit/issuepit/compare/v1.3.0...v1.4.0) (2026-03-03)


### Features

* add actionlint to helper-act and create helper-opencode-act combined image ([#288](https://github.com/issuepit/issuepit/issues/288)) ([0731fde](https://github.com/issuepit/issuepit/commit/0731fded4322296ed6705185960a2baf66dbe814))
* add CI/CD config pages for orgs and projects; fix runner image selection ([#282](https://github.com/issuepit/issuepit/issues/282)) ([00cc054](https://github.com/issuepit/issuepit/commit/00cc0544e3e79e42869d3e3be8fc20b82cf856bc))
* add multi-tab view to issue detail page ([#319](https://github.com/issuepit/issuepit/issues/319)) ([139a2c5](https://github.com/issuepit/issuepit/commit/139a2c52947d19a6a56b8914a16272f5dfb4861d))
* add tag selection and custom image option to CiCdImageSelector ([#303](https://github.com/issuepit/issuepit/issues/303)) ([6a325ba](https://github.com/issuepit/issuepit/commit/6a325ba84a4102b4ea2d2550ce7286338d89f6a1))
* common agenda — org-wide goal tracker with cross-project issue linking ([#285](https://github.com/issuepit/issuepit/issues/285)) ([f9c43e2](https://github.com/issuepit/issuepit/commit/f9c43e2951b4402c48d4ec79630a259c1cfa4b95))
* image paste support in issue descriptions/comments with S3/LocalStack backend ([#317](https://github.com/issuepit/issuepit/issues/317)) ([0b89731](https://github.com/issuepit/issuepit/commit/0b89731a5dc3785662a51d11f23a61d94a134de2))
* implement IBotNotificationService interface with Telegram and Kafka dispatch ([#297](https://github.com/issuepit/issuepit/issues/297)) ([e8f2be9](https://github.com/issuepit/issuepit/commit/e8f2be9723cefe2eadc0b08b01d045ae2a91ca64))
* improve act output in UI — job tracking, JSON parsing, scroll fix ([#300](https://github.com/issuepit/issuepit/issues/300)) ([667b521](https://github.com/issuepit/issuepit/commit/667b521800f65f0fa066718deb4843cdd8b7e7a8))
* issue history tracking ([#295](https://github.com/issuepit/issuepit/issues/295)) ([5a6ead5](https://github.com/issuepit/issuepit/commit/5a6ead55e1ef1af768b76d390d397d6134ae23dd))
* issue linking (blocks, blocked by, causes, caused by, solves, linked to, duplicates, requires, implements) ([#274](https://github.com/issuepit/issuepit/issues/274)) ([b65c2ce](https://github.com/issuepit/issuepit/commit/b65c2ce50f4d90c63277d7a566b6180d8c8ccd66))
* live updates for run detail pages + server-side duration push ([#267](https://github.com/issuepit/issuepit/issues/267)) ([ec93194](https://github.com/issuepit/issuepit/commit/ec931943781fb50e4acddf22068fa644280818ae))
* pass env, secrets and other inputs to act in cicd ([#276](https://github.com/issuepit/issuepit/issues/276)) ([9a2ac4d](https://github.com/issuepit/issuepit/commit/9a2ac4d0992af4b981de7f9072a5312d39d51768))
* per-runtime container pool limits with live status UI ([#302](https://github.com/issuepit/issuepit/issues/302)) ([913c4b0](https://github.com/issuepit/issuepit/commit/913c4b017f2c2162293ed89253571972eecd603b))
* **projects,agents,e2e:** add org selector to create forms and comprehensive E2E tests with page object model ([#264](https://github.com/issuepit/issuepit/issues/264)) ([425856b](https://github.com/issuepit/issuepit/commit/425856b1948ac55c4026a812a2d45c2cfe4806fd))
* skills management infrastructure ([#293](https://github.com/issuepit/issuepit/issues/293)) ([e70dfd6](https://github.com/issuepit/issuepit/commit/e70dfd6155c63d4cb214d102b66b983bbeb71e87))
* todo tracker with board/calendar views, iCal export, and MCP support ([#325](https://github.com/issuepit/issuepit/issues/325)) ([3a4d438](https://github.com/issuepit/issuepit/commit/3a4d4388f46ecbf627d2ef795f01388fec348a76))


### Bug Fixes

* act runner image not propagated from org/project settings; split image fields in retry ([#332](https://github.com/issuepit/issuepit/issues/332)) ([6b867c1](https://github.com/issuepit/issuepit/commit/6b867c1443087f1670a631b836a23e7f0def9bc2))
* add Vue SSR hydration retry to TelegramBotsPage.GotoAsync ([#330](https://github.com/issuepit/issuepit/issues/330)) ([ca5453e](https://github.com/issuepit/issuepit/commit/ca5453e332e8dbc57c5ddf38e537b0a078ab91d9))
* **badges:** correct SVG text sizing and add CI/CD metrics ([#304](https://github.com/issuepit/issuepit/issues/304)) ([448e4c7](https://github.com/issuepit/issuepit/commit/448e4c71ca2ed6b0cc47d75bbafd700ecdda7ee3))
* ci/cd run view — steps, matrix jobs, log matching, box layout, create-issue ([#331](https://github.com/issuepit/issuepit/issues/331)) ([e2cd194](https://github.com/issuepit/issuepit/commit/e2cd19495659e0a591b407352f5acd21c24b9ec3))
* data seeding, priority colors, and demo users/teams ([#315](https://github.com/issuepit/issuepit/issues/315)) ([8126210](https://github.com/issuepit/issuepit/commit/812621022ad189ecb9837b301d33970ec4b63baa))
* eliminate flaky E2E login tab-click via hydration-aware retry ([#314](https://github.com/issuepit/issuepit/issues/314)) ([6504227](https://github.com/issuepit/issuepit/commit/65042279d049e4712e79885348ed9de21ecb0173))
* improve seeded data ([#290](https://github.com/issuepit/issuepit/issues/290)) ([3f96ba7](https://github.com/issuepit/issuepit/commit/3f96ba7ad4f0817179f37dd7a48789f79519e77b))
* inject actrc on container start to prevent interactive EOF crash ([#271](https://github.com/issuepit/issuepit/issues/271)) ([3c7a089](https://github.com/issuepit/issuepit/commit/3c7a089c1cc143e8011ebe49fc70296adccb851c))
* prevent flaky E2E login retry from hitting register submit button ([#316](https://github.com/issuepit/issuepit/issues/316)) ([3b11460](https://github.com/issuepit/issuepit/commit/3b1146051eb504ef9b3fccc3ffe3b768bedc7ca8))
* prevent race condition in E2E login registration flow ([#283](https://github.com/issuepit/issuepit/issues/283)) ([e9e49ac](https://github.com/issuepit/issuepit/commit/e9e49acbb4861d24867111f40cea21475cafe7e5))
* remove unsupported `paths` filter from `merge_group` trigger ([#265](https://github.com/issuepit/issuepit/issues/265)) ([9b9d0fd](https://github.com/issuepit/issuepit/commit/9b9d0fdd4795d17874679f991b69e6389ae1cfb2))
* resolve flaky Ui_CreateAgent_AppearsInList E2E test ([#334](https://github.com/issuepit/issuepit/issues/334)) ([e7abe9b](https://github.com/issuepit/issuepit/commit/e7abe9b560fd69d640402172ff28bc53cb5bf2e2))
* suppress act first-run prompt by always writing actrc and passing -P flags directly ([#277](https://github.com/issuepit/issuepit/issues/277)) ([fe0ee5e](https://github.com/issuepit/issuepit/commit/fe0ee5ebb9503d078d94bbb94db89ec3c6fe5929))

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
