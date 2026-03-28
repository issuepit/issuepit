<template>
  <div class="p-8">
    <!-- Breadcrumb + Header -->
    <div class="flex items-center gap-2 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${projectId}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Runs', to: `/projects/${projectId}/runs`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
        { label: 'CI/CD Run', to: `/projects/${projectId}/runs/cicd/${runId}`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
      ]" />
      <!-- WS connection indicator -->
      <span v-if="isConnected" class="flex items-center gap-1 text-xs text-green-400 font-normal ml-1">
        <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
        Live
      </span>
      <span v-else class="flex items-center gap-1 text-xs text-gray-600 font-normal ml-1">
        <span class="w-1.5 h-1.5 rounded-full bg-gray-600" />
        Offline
      </span>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="store.currentRun">
      <!-- Run Info -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div>
            <p class="text-xs text-gray-500 mb-1">Status</p>
            <span :class="statusClass(store.currentRun.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
              <span :class="statusDot(store.currentRun.status)" class="w-1.5 h-1.5 rounded-full" />
              {{ store.currentRun.statusName }}
            </span>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Workflow</p>
            <p class="text-sm text-gray-300">{{ store.currentRun.workflow || '—' }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Branch</p>
            <NuxtLink
              v-if="store.currentRun.branch"
              :to="branchUrl(store.currentRun.branch)!"
              class="text-sm text-brand-400 hover:text-brand-300 font-mono transition-colors"
              :title="`View branch ${store.currentRun.branch} in code viewer`">
              {{ store.currentRun.branch }}
            </NuxtLink>
            <span v-else class="text-sm text-gray-300 font-mono">—</span>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Commit</p>
            <NuxtLink
              v-if="store.currentRun.commitSha"
              :to="commitUrl(store.currentRun.commitSha)!"
              class="text-sm text-brand-400 hover:text-brand-300 font-mono transition-colors"
              :title="`View commit ${store.currentRun.commitSha} in code viewer`">
              {{ store.currentRun.commitSha.slice(0, 7) }}
            </NuxtLink>
            <span v-else class="text-sm text-gray-300 font-mono">—</span>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Source</p>
            <span v-if="store.currentRun.externalSource" class="text-xs bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded">
              {{ store.currentRun.externalSource }}
            </span>
            <span v-else class="text-sm text-gray-600">local</span>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Trigger</p>
            <span v-if="store.currentRun.eventName" class="text-xs bg-gray-800 text-gray-300 px-1.5 py-0.5 rounded font-mono">
              {{ store.currentRun.eventName }}
            </span>
            <span v-else class="text-sm text-gray-600">—</span>
          </div>
          <div v-if="store.currentRun.externalRunId">
            <p class="text-xs text-gray-500 mb-1">External Run ID</p>
            <a v-if="store.currentRun.externalRunUrl"
              :href="store.currentRun.externalRunUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="text-sm text-brand-400 hover:text-brand-300 font-mono text-xs transition-colors">
              {{ store.currentRun.externalRunId }} ↗
            </a>
            <p v-else class="text-sm text-gray-300 font-mono text-xs">{{ store.currentRun.externalRunId }}</p>
          </div>
          <div v-if="store.currentRun.workspacePath">
            <p class="text-xs text-gray-500 mb-1">Workspace</p>
            <p class="text-xs text-gray-400 font-mono truncate" :title="store.currentRun.workspacePath">{{ store.currentRun.workspacePath }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Started</p>
            <p class="text-sm text-gray-400"><DateDisplay :date="store.currentRun.startedAt" mode="auto" /></p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Duration</p>
            <p class="text-sm text-gray-400">{{ store.currentRun.status === CiCdRunStatus.WaitingForApproval ? '—' : duration(store.currentRun.startedAt, store.currentRun.endedAt) }}</p>
          </div>
        </div>
        <!-- Inputs for workflow_dispatch runs -->
        <div v-if="runInputs && Object.keys(runInputs).length"
          class="mt-4 pt-4 border-t border-gray-800">
          <p class="text-xs text-gray-500 mb-2">Inputs</p>
          <div class="flex flex-wrap gap-2">
            <span v-for="(val, key) in runInputs" :key="key"
              class="text-xs bg-gray-800 text-gray-300 px-2 py-0.5 rounded font-mono">
              {{ key }}={{ val }}
            </span>
          </div>
        </div>
        <!-- Actions: hidden for external runs (can't retry/approve from IssuePit) -->
        <div v-if="!store.currentRun.externalSource">
          <div v-if="store.currentRun.status === CiCdRunStatus.WaitingForApproval"
            class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
            <button
              :disabled="approving"
              class="flex items-center gap-1.5 text-sm text-purple-400 hover:text-purple-300 disabled:opacity-50 transition-colors"
              @click="approveRunAction()">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M5 13l4 4L19 7" />
              </svg>
              {{ approving ? 'Approving…' : 'Approve Run' }}
            </button>
          </div>
          <div v-else-if="store.currentRun.status === CiCdRunStatus.Failed || store.currentRun.status === CiCdRunStatus.Cancelled || store.currentRun.status === CiCdRunStatus.SucceededWithWarnings || store.currentRun.status === CiCdRunStatus.Succeeded"
            class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
            <button
              :disabled="retrying"
              class="flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300 disabled:opacity-50 transition-colors"
              :title="isRetrigger ? 'Click to retrigger · Shift+click for options' : 'Click to retry · Shift+click for options'"
              @click.exact="retryRun()"
              @click.shift="openRetryModal()">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              {{ retrying ? (isRetrigger ? 'Retriggering…' : 'Retrying…') : (isRetrigger ? 'Retrigger Run' : 'Retry Run') }}
            </button>
          </div>
        </div>
        <!-- For external runs: show a link to the source if available -->
        <div v-else-if="store.currentRun.externalRunUrl"
          class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
          <a
            :href="store.currentRun.externalRunUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300 transition-colors">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
            </svg>
            View on {{ store.currentRun.externalSource === 'github' ? 'GitHub' : store.currentRun.externalSource }}
          </a>
        </div>
      </div>

      <!-- Retry options modal -->
      <Teleport to="body">
        <div v-if="showRetryModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="cancelRetry">
          <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-md">
            <!-- Conflict confirmation view -->
            <template v-if="retryConflict">
              <h3 class="text-base font-semibold text-white mb-4">Run Already in Progress</h3>
              <div class="mb-4 rounded-lg bg-yellow-900/40 border border-yellow-700/50 p-3 text-sm text-yellow-300">
                {{ retryConflict.message }}
              </div>
              <p class="text-sm text-gray-400 mb-6">Do you want to {{ isRetrigger ? 'retrigger' : 'retry' }} anyway? The existing run will continue in parallel.</p>
              <div class="flex justify-end gap-2">
                <button
                  class="px-4 py-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors"
                  @click="cancelRetry">
                  Cancel
                </button>
                <button
                  :disabled="retrying"
                  class="px-4 py-1.5 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-md transition-colors flex items-center gap-2"
                  @click="retryConflict && retryRunWithOptions(retryConflict.activeRunIds)">
                  <svg v-if="retrying" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                  </svg>
                  {{ retrying ? (isRetrigger ? 'Retriggering…' : 'Retrying…') : (isRetrigger ? 'Retrigger Anyway' : 'Retry Anyway') }}
                </button>
              </div>
            </template>

            <!-- Options form view -->
            <template v-else>
            <h3 class="text-base font-semibold text-white mb-4">{{ isRetrigger ? 'Retrigger Options' : 'Retry Options' }}</h3>

            <!-- Event / trigger selector -->
            <div class="mb-3">
              <label class="block text-xs text-gray-500 mb-1">Event / Trigger</label>
              <select
                v-model="retryOptions.eventName"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-white px-2.5 py-1.5 focus:outline-none focus:border-brand-500">
                <option value="push">push</option>
                <option value="pull_request">pull_request</option>
                <option value="workflow_dispatch">workflow_dispatch</option>
                <option value="workflow_call">workflow_call</option>
                <option value="merge_group">merge_group</option>
                <option value="release">release</option>
              </select>
              <p class="text-xs text-gray-600 mt-1">Override the event/trigger for this run. The original trigger was <code class="text-gray-400">{{ store.currentRun?.eventName ?? 'push' }}</code>.</p>
            </div>

            <!-- Branch override -->
            <div class="mb-3">
              <label class="block text-xs text-gray-500 mb-1">Branch</label>
              <input
                v-model="retryOptions.branch"
                type="text"
                placeholder="e.g. main"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
              <p class="text-xs text-gray-600 mt-1">Override the branch to run against. Leave blank to use the original branch <code class="text-gray-400">{{ store.currentRun?.branch ?? '—' }}</code>.</p>
            </div>

            <!-- Commit SHA override -->
            <div class="mb-3">
              <label class="block text-xs text-gray-500 mb-1">Commit SHA</label>
              <input
                v-model="retryOptions.commitSha"
                type="text"
                placeholder="Leave blank to use the branch tip"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 font-mono px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
              <p class="text-xs text-gray-600 mt-1">Defaults to the original commit. Clear to use the latest commit of the specified branch.</p>
            </div>

            <label class="flex items-start gap-3 cursor-pointer mb-4">
              <input
                v-model="retryOptions.keepContainerOnFailure"
                type="checkbox"
                class="mt-0.5 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
              <span class="text-sm text-gray-300">
                Keep container on failure
                <span class="block text-xs text-gray-500 mt-0.5">The Docker container is not removed when the run fails, so you can inspect it (e.g. verify where <code class="text-gray-400">act</code> is installed).</span>
              </span>
            </label>

            <!-- Skip Steps override -->
            <div class="mb-4">
              <div class="flex items-center justify-between mb-1">
                <label class="block text-xs text-gray-500">Skip Steps</label>
                <label class="flex items-center gap-1.5 text-xs text-gray-500 cursor-pointer">
                  <input
                    v-model="retryOptions.overrideSkipSteps"
                    type="checkbox"
                    class="rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
                  Override (clear inherited steps)
                </label>
              </div>
              <SkipStepsEditor v-model="retryOptions.skipSteps" :project-id="projectId" />
              <p class="text-xs text-gray-600 mt-1">
                Leave blank to inherit the original run's skip steps
                <template v-if="store.currentRun?.skipSteps">
                  (<code class="text-gray-400">{{ store.currentRun.skipSteps.split('\n').filter(s => s.trim()).join(', ') }}</code>).
                </template>
                <template v-else>
                  (none).
                </template>
                Check "Override" to explicitly clear them.
              </p>
            </div>

            <!-- Advanced section -->
            <details class="mb-4">
              <summary class="text-xs text-gray-500 cursor-pointer hover:text-gray-300 select-none">Advanced</summary>
              <div class="mt-3 space-y-3 pl-1">
                <label class="flex items-start gap-3 cursor-pointer">
                  <input
                    v-model="retryOptions.noDind"
                    type="checkbox"
                    class="mt-0.5 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
                  <span class="text-sm text-gray-300">
                    No Docker-in-Docker
                    <span class="block text-xs text-gray-500 mt-0.5">Do not mount <code class="text-gray-400">/var/run/docker.sock</code> into the container.</span>
                  </span>
                </label>
                <label class="flex items-start gap-3 cursor-pointer">
                  <input
                    v-model="retryOptions.noVolumeMounts"
                    type="checkbox"
                    class="mt-0.5 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
                  <span class="text-sm text-gray-300">
                    No volume mounts
                    <span class="block text-xs text-gray-500 mt-0.5">Run without any host volume mounts (workspace and docker socket are omitted).</span>
                  </span>
                </label>
                <div>
                  <label class="block text-xs text-gray-500 mb-1">Custom image</label>
                  <input
                    v-model="retryOptions.customImage"
                    type="text"
                    :placeholder="currentOuterImagePlaceholder"
                    class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
                  <p class="text-xs text-gray-600 mt-1">Outer Docker image for the container that executes the act tool itself.</p>
                </div>
                <div>
                  <label class="block text-xs text-gray-500 mb-1">Act runner image</label>
                  <input
                    v-model="retryOptions.actRunnerImage"
                    type="text"
                    :placeholder="currentActRunnerImagePlaceholder"
                    class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
                  <p class="text-xs text-gray-600 mt-1">Runner image used by act for platform mapping (overrides project/org setting).</p>
                </div>
                <div>
                  <label class="block text-xs text-gray-500 mb-1">Custom entrypoint</label>
                  <input
                    v-model="retryOptions.customEntrypoint"
                    type="text"
                    placeholder="e.g. /bin/sh"
                    class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
                </div>
                <div>
                  <label class="block text-xs text-gray-500 mb-1">Additional CLI args</label>
                  <input
                    v-model="retryOptions.customArgs"
                    type="text"
                    placeholder="e.g. --verbose --reuse"
                    class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
                </div>
              </div>
            </details>

            <div class="flex justify-end gap-2">
              <button
                class="px-4 py-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors"
                @click="cancelRetry">
                Cancel
              </button>
              <button
                :disabled="retrying"
                class="px-4 py-1.5 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-md transition-colors"
                @click="retryRunWithOptions()">
                {{ retrying ? (isRetrigger ? 'Retriggering…' : 'Retrying…') : (isRetrigger ? 'Retrigger Run' : 'Retry Run') }}
              </button>
            </div>
            </template>
          </div>
        </div>
      </Teleport>

      <!-- SHA mismatch warning banner -->
      <div v-if="store.currentRun.status === CiCdRunStatus.SucceededWithWarnings && shaWarningMessage"
        class="mb-6 rounded-xl bg-yellow-900/30 border border-yellow-700/50 p-4 flex items-start gap-3">
        <svg class="w-5 h-5 text-yellow-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium text-yellow-300">Commit SHA Mismatch</p>
          <p class="text-xs text-yellow-400/80 mt-1 font-mono">{{ shaWarningMessage }}</p>
        </div>
      </div>

      <!-- Linked Runs -->
      <div v-if="store.currentRunLinkedRuns.length" class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden mb-6">
        <div class="px-5 py-3 border-b border-gray-800">
          <h2 class="text-sm font-medium text-white">Linked Runs</h2>
        </div>
        <table class="w-full text-sm">
          <thead class="bg-gray-900/50">
            <tr>
              <th class="text-left px-4 py-2 text-gray-400 font-medium text-xs">Type</th>
              <th class="text-left px-4 py-2 text-gray-400 font-medium text-xs">Status</th>
              <th class="text-left px-4 py-2 text-gray-400 font-medium text-xs">Description</th>
              <th class="text-left px-4 py-2 text-gray-400 font-medium text-xs">Started</th>
              <th class="text-left px-4 py-2 text-gray-400 font-medium text-xs">Duration</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="link in store.currentRunLinkedRuns" :key="`${link.linkType}-${link.id}`"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateToLinkedRun(link)">
              <td class="px-4 py-2">
                <span :class="linkedRunTypeBadgeClass(link.linkType)"
                  class="inline-flex items-center text-xs px-2 py-0.5 rounded-full font-medium">
                  {{ link.linkLabel }}
                </span>
              </td>
              <td class="px-4 py-2">
                <span :class="linkedRunStatusClass(link)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                  <span :class="linkedRunStatusDot(link)" class="w-1.5 h-1.5 rounded-full" />
                  {{ link.statusName }}
                </span>
              </td>
              <td class="px-4 py-2 text-gray-300">
                <template v-if="link.linkType === 'agent-triggered'">
                  <span class="text-gray-500 mr-1">#{{ link.issueNumber }}</span>
                  {{ link.issueTitle }}
                </template>
                <span v-else class="font-mono text-xs text-gray-400">{{ link.workflow || link.branch || link.commitSha?.slice(0, 7) || '—' }}</span>
              </td>
              <td class="px-4 py-2 text-gray-400 text-xs"><DateDisplay :date="link.startedAt" mode="auto" /></td>
              <td class="px-4 py-2 text-gray-400 text-xs">{{ link.status === CiCdRunStatus.WaitingForApproval ? '—' : duration(link.startedAt, link.endedAt) }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Logs / Details / Jobs -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div class="flex items-center justify-between px-4 py-3 border-b border-gray-800">
          <div class="flex gap-1">
            <button v-for="t in sectionTabs" :key="t.value"
              :class="[
                'px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                activeSection === t.value ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'
              ]"
              @click="activeSection = t.value">
              {{ t.label }}
            </button>
          </div>

          <!-- Log stream filter (only in Logs tab) -->
          <div v-if="activeSection === 'logs'" class="flex items-center gap-2">
            <div class="flex gap-1">
              <button v-for="s in streamTabs" :key="s.value ?? 'all'"
                :class="[
                  'px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                  activeStream === s.value ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'
                ]"
                @click="activeStream = s.value">
                {{ s.label }}
              </button>
            </div>
            <button
              v-if="store.currentRunLogs.length"
              class="px-2.5 py-1 text-xs font-medium rounded-md text-gray-500 hover:text-gray-300 transition-colors"
              title="Copy full log to clipboard"
              @click="copyLogsToClipboard">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
            </button>
          </div>

          <!-- Jobs tab controls -->
          <div v-else-if="activeSection === 'jobs'" class="flex items-center gap-2">
            <button
              v-if="hasMultipleWorkflowFiles"
              :class="[
                'flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                selectedTriggerFilters.size > 0 ? 'bg-brand-700 text-white' : 'text-gray-500 hover:text-gray-300'
              ]"
              title="Filter workflows by trigger event"
              @click="showTriggerFilterModal = true">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2a1 1 0 01-.293.707L13 13.414V19a1 1 0 01-.553.894l-4 2A1 1 0 017 21v-7.586L3.293 6.707A1 1 0 013 6V4z" />
              </svg>
              Filter triggers
              <span v-if="selectedTriggerFilters.size > 0" class="ml-0.5 bg-brand-500 text-white rounded-full px-1 text-[10px] leading-4">{{ selectedTriggerFilters.size }}</span>
            </button>
            <!-- Slim mode toggle -->
            <button
              :class="[
                'flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                slimMode ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'
              ]"
              title="Toggle slim mode — hides log counts, file names and status labels; filters out workflows whose trigger doesn't match this run's event"
              @click="slimMode = !slimMode">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
              </svg>
              Slim
            </button>
            <!-- Debug mode toggle -->
            <button
              :class="[
                'flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                debugMode ? 'bg-amber-700 text-white' : 'text-gray-500 hover:text-gray-300'
              ]"
              title="Toggle debug mode — shows original act log IDs before fuzzy matching and all triggers per workflow"
              @click="debugMode = !debugMode">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
              </svg>
              Debug
            </button>
          </div>
        </div>

        <!-- Jobs tab -->
        <template v-if="activeSection === 'jobs'">
          <!-- Graph not available — show as yellow warning box (always shown when graph fails, even if log-based jobs exist) -->
          <div v-if="store.currentRunGraphError" class="m-4 rounded-lg bg-yellow-900/40 border border-yellow-700/50 p-4 flex items-start gap-3">
            <svg class="w-5 h-5 text-yellow-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            </svg>
            <div>
              <p class="text-sm font-medium text-yellow-300">Workflow graph unavailable</p>
              <p class="text-xs text-yellow-400/80 mt-0.5">{{ store.currentRunGraphError }}</p>
            </div>
          </div>
          <div v-if="enrichedJobs.length" class="p-4">
            <!-- Actionlint warnings from graph validation -->
            <div v-if="store.currentRunGraph?.warnings?.length" class="mb-4 rounded-lg bg-yellow-900/40 border border-yellow-700/50 p-3">
              <div class="flex items-center gap-2 mb-2">
                <svg class="w-4 h-4 text-yellow-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                </svg>
                <span class="text-xs font-medium text-yellow-300">Workflow validation warnings (actionlint)</span>
              </div>
              <pre v-for="(w, i) in store.currentRunGraph.warnings" :key="i" class="text-xs text-yellow-400/80 font-mono whitespace-pre-wrap">{{ w }}</pre>
            </div>
            <!-- Job graph with SVG dependency arrows -->
            <div class="relative overflow-x-auto pb-2">
              <!-- SVG arrows layer (absolute, behind boxes) -->
              <svg
                v-if="graphLayout.svgWidth && graphLayout.svgHeight"
                class="absolute top-0 left-0 pointer-events-none"
                :width="graphLayout.svgWidth"
                :height="graphLayout.svgHeight"
                style="z-index: 0">
                <defs>
                  <marker id="arrow" markerWidth="4" markerHeight="4" refX="3" refY="2" orient="auto">
                    <path d="M0,0 L0,4 L4,2 z" fill="#4b5563" />
                  </marker>
                  <marker id="arrow-hi" markerWidth="4" markerHeight="4" refX="3" refY="2" orient="auto">
                    <path d="M0,0 L0,4 L4,2 z" fill="#6366f1" />
                  </marker>
                  <marker id="arrow-fail" markerWidth="4" markerHeight="4" refX="3" refY="2" orient="auto">
                    <path d="M0,0 L0,4 L4,2 z" fill="#ef4444" />
                  </marker>
                </defs>
                <path
                  v-for="(edge, i) in graphLayout.edges"
                  :key="i"
                  :d="edge.path"
                  fill="none"
                  :stroke="edge.isFailure ? '#ef4444' : edge.highlighted ? '#6366f1' : '#4b5563'"
                  :stroke-width="edge.isFailure || edge.highlighted ? 2 : 1.5"
                  :marker-end="edge.isFailure ? 'url(#arrow-fail)' : edge.highlighted ? 'url(#arrow-hi)' : 'url(#arrow)'" />
              </svg>

              <!-- Job boxes layer -->
              <div
                :style="{ position: 'relative', width: graphLayout.svgWidth + 'px', minHeight: graphLayout.svgHeight + 'px', zIndex: 1 }"
                style="z-index: 1">
                <div
                  v-for="job in visibleJobs"
                  :key="job.id"
                  :ref="(el) => registerJobBox(job.id, el as HTMLElement | null)"
                  :style="{ position: 'absolute', left: job.x + 'px', top: job.y + 'px', width: '220px' }"
                  :class="[
                    'flex flex-col items-start gap-1 px-4 py-3 rounded-xl border transition-colors text-left cursor-pointer',
                    selectedJob === job.id
                      ? 'border-brand-500 bg-brand-950/30 ring-1 ring-brand-500/40'
                      : hoveredJob === job.id
                        ? 'border-gray-400/50 bg-gray-700/40 shadow-lg shadow-black/40 ring-1 ring-white/5 backdrop-blur-sm'
                        : connectedJobIds.has(job.id)
                          ? 'border-brand-700/50 bg-gray-800/60'
                          : blockedJobIds.has(job.id)
                            ? 'border-gray-700 bg-gray-800/40 opacity-40'
                            : 'border-gray-700 bg-gray-800/80 hover:border-gray-600',
                  ]"
                  @mouseenter="hoveredJob = job.id"
                  @mouseleave="hoveredJob = null"
                  @click="toggleJobFilter(job.id)">
                  <span class="flex items-center gap-1.5 w-full">
                    <span :class="jobStatusDot(job)" class="w-2 h-2 rounded-full shrink-0" />
                    <span class="text-sm font-medium text-white break-words leading-tight">{{ job.name }}</span>
                  </span>
                  <!-- Debug mode: show raw act log IDs and workflow triggers -->
                  <template v-if="debugMode">
                    <div v-if="job.rawJobIds.length" class="w-full">
                      <span class="text-[10px] text-amber-500 font-mono uppercase tracking-wide">act IDs</span>
                      <div class="flex flex-wrap gap-0.5 mt-0.5">
                        <span
                          v-for="rawId in job.rawJobIds"
                          :key="rawId"
                          class="text-[10px] font-mono bg-amber-900/30 border border-amber-700/40 text-amber-300 px-1 rounded">{{ rawId }}</span>
                      </div>
                    </div>
                    <div v-if="job.workflowTriggers.length" class="w-full">
                      <span class="text-[10px] text-amber-500 font-mono uppercase tracking-wide">triggers</span>
                      <div class="flex flex-wrap gap-0.5 mt-0.5">
                        <span
                          v-for="trigger in job.workflowTriggers"
                          :key="trigger"
                          class="text-[10px] font-mono bg-amber-900/30 border border-amber-700/40 text-amber-300 px-1 rounded">{{ trigger }}</span>
                      </div>
                    </div>
                  </template>
                  <span v-if="(job.callerWorkflowFile || job.workflowFile) && !slimMode" class="text-xs text-gray-500 font-mono">
                    <template v-if="job.callerWorkflowFile">{{ job.callerWorkflowFile }} / {{ job.workflowFile }}</template>
                    <template v-else>{{ job.workflowFile }}</template>
                  </span>
                  <span v-if="!slimMode" :class="jobStatusClass(job)" class="text-xs px-1.5 py-0.5 rounded-full font-medium">
                    {{ jobStatusLabel(job) }}
                  </span>
                  <!-- Timing: show elapsed time while running, total time when complete -->
                  <span v-if="job.startedAt" class="text-xs text-gray-500 font-mono mt-0.5">
                    <template v-if="job.isComplete && job.endedAt">
                      {{ jobDuration(job.startedAt, job.endedAt) }} total
                    </template>
                    <template v-else-if="job.startedAt">
                      {{ jobDuration(job.startedAt) }} elapsed
                    </template>
                  </span>
                  <span v-if="!slimMode" class="text-xs text-gray-600">{{ job.logCount }} log line{{ job.logCount === 1 ? '' : 's' }}</span>
                  <!-- Matrix jobs: show per-instance status dots instead of "Nx matrix" label -->
                  <div v-if="job.matrixInstances.length > 1" class="flex flex-wrap gap-1 mt-1 w-full">
                    <button
                      v-for="inst in job.matrixInstances"
                      :key="inst.rawId"
                      :title="inst.rawId"
                      :class="[
                        'flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-mono border transition-colors',
                        selectedMatrixRawId === inst.rawId
                          ? 'border-brand-500 bg-brand-950/40 text-brand-300'
                          : 'border-gray-700 bg-gray-900/50 text-gray-500 hover:border-gray-600',
                      ]"
                      @click.stop="selectMatrixInstance(job.id, inst.rawId)">
                      <span :class="jobStatusDot(inst)" class="w-1.5 h-1.5 rounded-full shrink-0" />
                      {{ matrixLabel(inst.rawId, job) }}
                      <span v-if="inst.startedAt" class="text-gray-600 ml-0.5">{{ jobDuration(inst.startedAt, inst.isComplete ? inst.endedAt : undefined) }}</span>
                    </button>
                  </div>

                  <!-- Create issue button for failed jobs -->
                  <button
                    v-if="job.hasError && job.isComplete"
                    class="mt-1 flex items-center gap-1 text-xs text-red-400 hover:text-red-300 transition-colors"
                    title="Create an issue from this failed job"
                    @click.stop="openCreateIssueModal(job.id)">
                    <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                    </svg>
                    Create Issue
                  </button>
                </div>
              </div>
            </div>

            <!-- Logs filtered to selected job, grouped by step -->
            <div v-if="selectedJob" class="mt-4">
              <div class="flex items-center gap-2 mb-2 flex-wrap">
                <span class="text-xs text-gray-400">
                  Showing logs for job: <span class="text-white font-mono">{{ visibleJobs.find(j => j.id === selectedJob)?.name ?? selectedJob }}</span>
                  <template v-if="selectedMatrixRawId"> · instance <span class="text-brand-300 font-mono">{{ selectedMatrixRawId }}</span></template>
                </span>
                <button class="text-xs text-gray-500 hover:text-gray-300 transition-colors" @click="deselectJob()">Clear filter</button>
                <!-- Search -->
                <div class="ml-auto flex items-center gap-2">
                  <div class="relative">
                    <svg class="absolute left-2 top-1/2 -translate-y-1/2 w-3 h-3 text-gray-500 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                    <input
                      v-model="logSearchQuery"
                      type="text"
                      placeholder="Search logs…"
                      class="bg-gray-900 border border-gray-700 rounded-md text-xs text-gray-300 pl-6 pr-2 py-1 placeholder-gray-600 focus:outline-none focus:border-brand-500 w-44 transition-colors" />
                  </div>
                  <!-- Word wrap toggle -->
                  <button
                    :class="['px-2 py-0.5 text-xs rounded-md border transition-colors', wordWrap ? 'border-brand-700 text-brand-300 bg-brand-950/30' : 'border-gray-700 text-gray-500 hover:border-gray-600']"
                    title="Toggle word wrap"
                    @click="wordWrap = !wordWrap">
                    Wrap
                  </button>
                </div>
              </div>
              <div class="bg-gray-950 rounded-lg p-4 font-mono text-xs overflow-auto max-h-[500px]">
                <template v-if="filteredJobLogsByStep.length">
                  <template v-for="(group, gi) in filteredJobLogsByStep" :key="gi">
                    <!-- Step header: collapsible, shows duration. Null stepId → "Set up job" (step 0) or "Complete job" (last). -->
                    <div
                      class="flex items-center gap-2 mt-3 mb-1 first:mt-0 select-none cursor-pointer group"
                      @click="toggleStep(group.key)">
                      <span class="text-gray-600 transition-transform" :class="collapsedSteps.has(group.key) ? '' : 'rotate-90'">▶</span>
                      <span
                        class="text-xs font-semibold tracking-wide uppercase group-hover:text-gray-200 transition-colors"
                        :class="isStepIgnored(group.stepId) ? 'text-gray-600 line-through' : 'text-gray-400'">
                        {{ group.label }}
                      </span>
                      <span v-if="isStepIgnored(group.stepId)" class="text-[10px] text-gray-600 bg-gray-800 px-1 rounded font-mono normal-case tracking-normal">skipped</span>
                      <span v-if="stepDuration(group)" class="text-[10px] text-gray-600 font-mono">{{ stepDuration(group) }}</span>
                      <span class="flex-1 border-t border-gray-800" />
                    </div>
                    <template v-if="!collapsedSteps.has(group.key)">
                      <div v-for="log in group.logs" :key="log.id" class="flex gap-3 leading-5">
                        <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
                        <!-- eslint-disable-next-line vue/no-v-html -->
                        <span :class="[log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300', wordWrap ? 'whitespace-pre-wrap break-all' : 'whitespace-pre']" v-html="renderLogLine(log.line, logSearchQuery)" />
                      </div>
                    </template>
                  </template>
                </template>
                <div v-else class="text-gray-500 text-center py-4">{{ logSearchQuery ? 'No matching log lines' : 'No logs for this job' }}</div>
              </div>
            </div>

            <!-- Unmatched log job IDs warning (logs arrived for job IDs not in the graph) -->
            <div v-if="unmatchedLogJobIds.length" class="mt-4 rounded-lg bg-gray-800/60 border border-gray-700 p-3">
              <div class="flex items-center gap-2 mb-2">
                <svg class="w-3.5 h-3.5 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span class="text-xs font-medium text-gray-400">Unmatched log streams ({{ unmatchedLogJobIds.length }}) — logs arrived for job IDs not found in the workflow graph</span>
              </div>
              <div class="flex flex-wrap gap-1">
                <span
                  v-for="id in unmatchedLogJobIds"
                  :key="id"
                  class="text-[10px] font-mono bg-gray-900 border border-gray-700 rounded px-1.5 py-0.5 text-gray-500">
                  {{ id }}
                </span>
              </div>
            </div>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">No job data available — job info is extracted from act's JSON output</div>
        </template>

        <!-- Logs tab -->
        <template v-else-if="activeSection === 'logs'">
          <div v-if="filteredLogs.length" class="bg-gray-950 p-4 font-mono text-xs overflow-auto max-h-[600px]">
            <!-- Create issue button for error logs -->
            <div v-if="store.currentRunLogs.some(l => l.stream === 'stderr')" class="mb-3 flex justify-end">
              <button
                class="flex items-center gap-1.5 text-xs text-red-400 hover:text-red-300 transition-colors"
                title="Create an issue from the run logs"
                @click="openCreateIssueModal('')">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                </svg>
                Create Issue from Logs
              </button>
            </div>
            <div v-for="log in filteredLogs" :key="log.id" class="flex gap-3 leading-5">
              <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
              <!-- eslint-disable-next-line vue/no-v-html -->
              <span :class="log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300'" class="whitespace-pre-wrap break-all" v-html="renderLogLine(log.line)" />
            </div>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">No logs available</div>
        </template>

        <!-- Tests tab -->
        <template v-else-if="activeSection === 'tests'">
          <div v-if="store.currentRunTestSuites.length" class="p-4 space-y-4">
            <!-- Stats bar -->
            <div class="flex flex-wrap items-center gap-4 bg-gray-900 border border-gray-800 rounded-lg px-4 py-3">
              <div>
                <p class="text-xs text-gray-500">Total</p>
                <p class="text-lg font-semibold text-white">{{ testRunStats.total }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-500">Passed</p>
                <p class="text-lg font-semibold text-green-400">{{ testRunStats.passed }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-500">Failed</p>
                <p class="text-lg font-semibold" :class="testRunStats.failed > 0 ? 'text-red-400' : 'text-gray-600'">{{ testRunStats.failed }}</p>
              </div>
              <div v-if="testRunStats.skipped">
                <p class="text-xs text-gray-500">Skipped</p>
                <p class="text-lg font-semibold text-yellow-500">{{ testRunStats.skipped }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-500">Fail Rate</p>
                <p class="text-lg font-semibold" :class="testRunStats.failRate >= 0.5 ? 'text-red-400' : testRunStats.failRate > 0 ? 'text-yellow-400' : 'text-gray-600'">
                  {{ Math.round(testRunStats.failRate * 100) }}%
                </p>
              </div>
              <div>
                <p class="text-xs text-gray-500">Duration</p>
                <p class="text-lg font-semibold text-gray-300">{{ formatTestDuration(testRunStats.duration) }}</p>
              </div>
              <div class="ml-auto flex items-center gap-2">
                <NuxtLink
                  :to="`/projects/${projectId}/runs/test-history`"
                  class="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-300 transition-colors">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                  </svg>
                  Test History
                </NuxtLink>
              </div>
            </div>

            <!-- Controls bar -->
            <div class="flex flex-wrap items-center gap-2">
              <!-- Failed-only filter toggle -->
              <button
                :class="['flex items-center gap-1.5 text-xs px-3 py-1.5 rounded-lg border transition-colors', showFailedOnly ? 'border-red-600 bg-red-950/30 text-red-400' : 'border-gray-700 text-gray-500 hover:border-gray-600 hover:text-gray-300']"
                @click="showFailedOnly = !showFailedOnly">
                <span class="w-1.5 h-1.5 rounded-full" :class="showFailedOnly ? 'bg-red-400' : 'bg-gray-600'" />
                Failed only
              </button>
              <!-- Collapse/expand all -->
              <button
                class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-1.5"
                @click="collapseAllSuites">
                Collapse all
              </button>
              <button
                class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-1.5"
                @click="expandAllSuites">
                Expand all
              </button>
            </div>

            <!-- Suite list -->
            <div
              v-for="suite in filteredSuites"
              :key="suite.id"
              class="bg-gray-900 border border-gray-800 rounded-lg overflow-hidden">
              <!-- Suite header — clickable to collapse/expand -->
              <div
                class="flex items-center gap-4 p-3 border-b border-gray-800 bg-gray-800/40 cursor-pointer select-none group"
                @click="toggleSuite(suite.id)">
                <span
                  class="text-gray-500 transition-transform shrink-0"
                  :class="collapsedSuites.has(suite.id) ? '' : 'rotate-90'">▶</span>
                <span class="text-sm font-medium text-gray-300 truncate flex-1 group-hover:text-gray-200 transition-colors">{{ suite.artifactName }}</span>
                <span class="flex items-center gap-1 text-xs text-green-400">
                  <span class="w-1.5 h-1.5 rounded-full bg-green-400" />
                  {{ suite.passedTests }} passed
                </span>
                <span v-if="suite.failedTests" class="flex items-center gap-1 text-xs text-red-400">
                  <span class="w-1.5 h-1.5 rounded-full bg-red-400" />
                  {{ suite.failedTests }} failed
                </span>
                <span v-if="suite.skippedTests" class="flex items-center gap-1 text-xs text-yellow-500">
                  <span class="w-1.5 h-1.5 rounded-full bg-yellow-500" />
                  {{ suite.skippedTests }} skipped
                </span>
                <span class="text-xs text-gray-500">{{ formatTestDuration(suite.durationMs) }}</span>
              </div>
              <!-- Test cases (hidden when suite is collapsed) -->
              <div v-if="!collapsedSuites.has(suite.id)" class="divide-y divide-gray-800">
                <div
                  v-for="tc in suite.testCases"
                  :key="tc.id"
                  class="px-3 py-2">
                  <div class="flex items-center gap-2">
                    <!-- outcome icon -->
                    <span v-if="tc.outcomeName === 'Passed'" class="text-green-400 shrink-0">✓</span>
                    <span v-else-if="tc.outcomeName === 'Failed'" class="text-red-400 shrink-0">✗</span>
                    <span v-else class="text-yellow-500 shrink-0">–</span>
                    <NuxtLink
                      :to="`/projects/${projectId}/runs/test-history?tab=Tests&test=${encodeURIComponent(tc.fullName)}`"
                      class="text-xs text-gray-300 font-mono truncate flex-1 hover:text-brand-400 transition-colors"
                      :title="tc.fullName"
                      @mouseenter="showTestTooltip($event, tc)"
                      @mousemove="moveTestTooltip($event)"
                      @mouseleave="hideTestTooltip">
                      {{ tc.methodName || tc.fullName }}
                    </NuxtLink>
                    <!-- Create issue button (only for failed tests) -->
                    <button
                      v-if="tc.outcomeName === 'Failed'"
                      class="shrink-0 text-xs text-gray-600 hover:text-red-400 transition-colors px-1"
                      title="Create issue from this failed test"
                      @click.stop="openCreateIssueFromTest(tc, suite.artifactName)">
                      <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                          d="M12 9v3m0 0v3m0-3h3m-3 0H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    </button>
                    <span class="text-xs text-gray-600 shrink-0">{{ formatTestDuration(tc.durationMs) }}</span>
                  </div>
                  <!-- Error details (collapsed by default) -->
                  <div v-if="tc.errorMessage" class="mt-1.5 ml-5 text-xs text-red-400 font-mono whitespace-pre-wrap break-all">{{ tc.errorMessage }}</div>
                </div>
              </div>
            </div>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">
            No test results available.<br>
            <span class="text-xs text-gray-600">Test results are collected automatically from <code>.trx</code> artifact files after the run completes.</span>
          </div>
        </template>

        <!-- Artifacts tab -->
        <template v-else-if="activeSection === 'artifacts'">
          <div class="p-4">
            <template v-if="store.currentRunArtifacts.length">
              <div class="flex items-center justify-between mb-3 gap-3 flex-wrap">
                <p class="text-xs text-gray-500">{{ store.currentRunArtifacts.length }} artifact{{ store.currentRunArtifacts.length === 1 ? '' : 's' }} produced by this run.</p>
                <button
                  v-if="hiddenTestResultArtifactCount > 0"
                  class="text-xs text-gray-400 hover:text-gray-200 transition-colors flex items-center gap-1"
                  data-testid="toggle-test-result-artifacts"
                  @click="showTestResultArtifacts = !showTestResultArtifacts">
                  <svg class="w-3.5 h-3.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  {{ showTestResultArtifacts ? 'Hide' : 'Show' }} {{ hiddenTestResultArtifactCount }} test result artifact{{ hiddenTestResultArtifactCount === 1 ? '' : 's' }}
                </button>
              </div>
              <div class="space-y-2">
                <div
                  v-for="artifact in visibleArtifacts"
                  :key="artifact.id"
                  class="flex items-center gap-3 px-4 py-3 rounded-lg bg-gray-800/60 border border-gray-700">
                  <svg class="w-5 h-5 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                      d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
                  </svg>
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-medium text-gray-200 truncate">{{ artifact.name }}</p>
                    <p class="text-xs text-gray-500">{{ artifact.fileCount }} file{{ artifact.fileCount === 1 ? '' : 's' }} · {{ formatBytes(artifact.sizeBytes) }}</p>
                  </div>
                  <span class="text-xs text-gray-600 shrink-0"><DateDisplay :date="artifact.createdAt" mode="auto" /></span>
                  <button
                    v-if="artifact.storageKey"
                    class="flex items-center gap-1 px-2.5 py-1.5 rounded text-xs font-medium bg-brand-600 hover:bg-brand-500 disabled:opacity-60 text-white transition-colors shrink-0"
                    :disabled="downloadingArtifacts.has(artifact.id)"
                    :aria-label="`Download ${artifact.name}.zip`"
                    :title="`Download ${artifact.name}.zip`"
                    @click="downloadArtifact(artifact.id, artifact.name)">
                    <svg v-if="downloadingArtifacts.has(artifact.id)" class="w-3.5 h-3.5 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                    <svg v-else class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                    </svg>
                    {{ downloadingArtifacts.has(artifact.id) ? 'Downloading…' : 'Download' }}
                  </button>
                  <span
                    v-else
                    class="flex items-center gap-1 px-2.5 py-1.5 rounded text-xs font-medium bg-gray-700 text-gray-500 cursor-not-allowed shrink-0"
                    title="Artifact storage (S3) is not configured">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                    </svg>
                    Storage not configured
                  </span>
                </div>
              </div>
            </template>
            <div v-else class="rounded-lg bg-gray-800/60 border border-gray-700 p-6 flex flex-col items-center gap-3 text-center">
              <svg class="w-8 h-8 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                  d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
              </svg>
              <p class="text-sm text-gray-500">No artifacts found for this run.</p>
              <p class="text-xs text-gray-600">Artifacts are collected after the run completes. Make sure <code class="text-gray-400">actions/upload-artifact</code> is used in your workflows.</p>
            </div>
          </div>
        </template>

        <!-- Details tab -->
        <template v-else>
          <div v-if="debugMetadata.length" class="p-4 font-mono text-xs">
            <table class="w-full">
              <tbody>
                <tr v-for="(entry, i) in debugMetadata" :key="i" class="border-b border-gray-800 last:border-0">
                  <td class="py-2 pr-6 text-gray-500 whitespace-nowrap align-top w-40">{{ entry.key }}</td>
                  <td class="py-2 break-all">
                    <!-- Docker image (outer) -->
                    <template v-if="entry.key === 'Docker image'">
                      <span
                        :class="isNonDefaultOuterImage ? 'text-yellow-300 font-semibold' : 'text-gray-300'"
                        :title="isNonDefaultOuterImage ? 'Custom outer image overridden for this run' : 'Configured outer image'"
                      >{{ entry.value }}</span>
                      <span v-if="isNonDefaultOuterImage" class="ml-2 inline-flex items-center gap-1 text-yellow-500 text-xs">
                        <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                          <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
                        </svg>
                        run override
                      </span>
                      <span v-else-if="outerImgSourceLabel" class="ml-2 text-xs text-gray-500">{{ outerImgSourceLabel }}</span>
                    </template>
                    <!-- Docker image source annotation -->
                    <template v-else-if="entry.key === 'Docker img src'">
                      <span class="text-xs" :class="isNonDefaultOuterImage ? 'text-orange-400' : 'text-gray-500'">{{ entry.value }}</span>
                    </template>
                    <!-- Act runner image (inner) -->
                    <template v-else-if="entry.key === 'Act runner img'">
                      <span
                        :class="runnerImgSourceLabel === 'global-default' || runnerImgSourceLabel === 'server-config' ? 'text-gray-400' : 'text-yellow-300 font-semibold'"
                        :title="runnerImgSourceTitle"
                      >{{ entry.value }}</span>
                      <span v-if="runnerImgSourceBadge" class="ml-2 text-xs" :class="runnerImgSourceBadgeClass">{{ runnerImgSourceBadge }}</span>
                    </template>
                    <!-- Runner image source annotation -->
                    <template v-else-if="entry.key === 'Runner img src'">
                      <span class="text-xs" :class="runnerImgSourceBadgeClass">{{ runnerImgSourceLabel }}</span>
                    </template>
                    <!-- Default display -->
                    <template v-else>
                      <span class="text-gray-300">{{ entry.value }}</span>
                    </template>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">No details available</div>
        </template>
      </div>
    </template>

    <div v-else-if="!store.loading" class="flex flex-col items-center justify-center py-16 text-center">
      <p class="text-gray-400 font-medium">{{ store.error || 'Run not found' }}</p>
    </div>

    <ErrorBox :error="store.error" />
    <ToastError :error="downloadError" />

    <!-- Create Issue from failed job modal -->
    <Teleport to="body">
      <div v-if="showCreateIssueModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="showCreateIssueModal = false">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-lg">
          <h3 class="text-base font-semibold text-white mb-1">Create Issue from {{ createIssueJobId ? 'Failed Job' : 'Run Logs' }}</h3>
          <div class="flex flex-wrap gap-x-4 text-xs text-gray-500 mb-4">
            <span v-if="createIssueJobId">Job: <span class="font-mono text-gray-300">{{ createIssueJobId }}</span></span>
            <span v-else>Run: <span class="font-mono text-gray-300">{{ runId.slice(0, 8) }}</span></span>
            <span v-if="store.currentRun?.branch">Branch: <span class="font-mono text-gray-300">{{ store.currentRun.branch }}</span></span>
            <span v-if="store.currentRun?.commitSha">Commit: <span class="font-mono text-gray-300">{{ store.currentRun.commitSha.slice(0, 7) }}</span></span>
          </div>

          <div class="mb-3">
            <label class="block text-xs text-gray-500 mb-1">Title</label>
            <input
              v-model="createIssueTitle"
              type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-3 py-2 focus:outline-none focus:border-brand-500" />
          </div>

          <div class="mb-3">
            <label class="block text-xs text-gray-500 mb-1">Log scope</label>
            <div class="flex flex-wrap gap-2">
              <button v-for="scope in logScopeOptions" :key="scope.value"
                :class="['px-3 py-1 text-xs rounded-md border transition-colors', createIssueLogScope === scope.value ? 'border-brand-500 bg-brand-950/30 text-brand-300' : 'border-gray-700 text-gray-500 hover:border-gray-600']"
                @click="createIssueLogScope = scope.value">
                {{ scope.label }}
              </button>
            </div>
            <div v-if="createIssueLogScope === 'filter'" class="mt-2">
              <input
                v-model="createIssueFilterPattern"
                type="text"
                placeholder="Regex pattern, e.g. error|fault|warn|fail|exception"
                :class="['w-full bg-gray-800 border rounded-md text-xs text-gray-300 px-3 py-1.5 font-mono focus:outline-none', createIssueFilterPatternInvalid ? 'border-red-500 focus:border-red-400' : 'border-gray-700 focus:border-brand-500']" />
              <p v-if="createIssueFilterPatternInvalid" class="mt-1 text-xs text-red-400">Invalid regex pattern — showing all lines</p>
            </div>
          </div>

          <div class="mb-4 bg-gray-950 rounded-lg p-3 font-mono text-xs overflow-auto max-h-[200px]">
            <div v-for="(line, i) in createIssuePreviewLines" :key="i" class="text-gray-400 leading-5 whitespace-pre-wrap break-all">{{ line }}</div>
            <div v-if="!createIssuePreviewLines.length" class="text-gray-600">No log lines for this scope</div>
          </div>

          <div class="mb-3">
            <label for="create-issue-verbose" class="flex items-center gap-2 cursor-pointer select-none">
              <input id="create-issue-verbose" v-model="createIssueVerbose" type="checkbox" class="rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500">
              <span class="text-xs text-gray-400">Include verbose info (act / IssuePit versions from debug log)</span>
            </label>
          </div>

          <div v-if="createIssueError" class="mb-3 text-xs text-red-400">{{ createIssueError }}</div>

          <div class="flex justify-end gap-2">
            <button class="px-4 py-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors" @click="showCreateIssueModal = false">Cancel</button>
            <button
              data-testid="create-issue-submit"
              :disabled="creatingIssue || !createIssueTitle.trim()"
              class="px-4 py-1.5 text-sm bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-md transition-colors"
              @click="submitCreateIssue">
              {{ creatingIssue ? 'Creating…' : 'Create Issue' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Create Issue from failed test modal -->
    <Teleport to="body">
      <div v-if="createIssueTestCase" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="createIssueTestCase = null">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-lg">
          <h3 class="text-base font-semibold text-white mb-1">Create Issue from Failed Test</h3>
          <div class="flex flex-wrap gap-x-4 text-xs text-gray-500 mb-4">
            <span class="font-mono text-gray-400 truncate max-w-xs" :title="createIssueTestCase.fullName">{{ createIssueTestCase.methodName || createIssueTestCase.fullName }}</span>
            <span v-if="store.currentRun?.branch">Branch: <span class="font-mono text-gray-300">{{ store.currentRun.branch }}</span></span>
            <span v-if="store.currentRun?.commitSha">Commit: <span class="font-mono text-gray-300">{{ store.currentRun.commitSha.slice(0, 7) }}</span></span>
          </div>

          <div class="mb-3">
            <label class="block text-xs text-gray-500 mb-1">Title</label>
            <input
              v-model="createIssueFromTestTitle"
              type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-3 py-2 focus:outline-none focus:border-brand-500" />
          </div>

          <div v-if="createIssueTestCase.errorMessage" class="mb-3">
            <label class="block text-xs text-gray-500 mb-1">Error</label>
            <div class="bg-gray-950 rounded-lg p-3 font-mono text-xs text-red-400 whitespace-pre-wrap break-all max-h-[120px] overflow-auto">{{ createIssueTestCase.errorMessage }}</div>
          </div>

          <div v-if="createIssueTestCase.stackTrace" class="mb-4">
            <label class="block text-xs text-gray-500 mb-1">Stack trace</label>
            <div class="bg-gray-950 rounded-lg p-3 font-mono text-xs text-gray-400 whitespace-pre-wrap break-all max-h-[120px] overflow-auto">{{ createIssueTestCase.stackTrace }}</div>
          </div>

          <div v-if="createIssueFromTestError" class="mb-3 text-xs text-red-400">{{ createIssueFromTestError }}</div>

          <div class="flex justify-end gap-2">
            <button class="px-4 py-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors" @click="createIssueTestCase = null">Cancel</button>
            <button
              :disabled="creatingIssueFromTest || !createIssueFromTestTitle.trim()"
              class="px-4 py-1.5 text-sm bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-md transition-colors"
              @click="submitCreateIssueFromTest">
              {{ creatingIssueFromTest ? 'Creating…' : 'Create Issue' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Test stats tooltip -->
    <Teleport to="body">
      <div
        v-if="testTooltip.visible"
        class="fixed z-[9999] pointer-events-none bg-gray-900 border border-gray-700 rounded-lg shadow-xl text-xs p-2.5 min-w-[180px] max-w-[280px]"
        :style="`left:${testTooltip.x + 14}px;top:${testTooltip.y - 8}px;transform:translateY(-100%)`">
        <div class="font-medium text-gray-200 mb-1.5 truncate">{{ testTooltip.data?.methodName || testTooltip.fullName }}</div>
        <div v-if="testTooltip.loading" class="flex items-center gap-1.5 text-gray-500">
          <div class="w-3 h-3 border border-gray-500 border-t-transparent rounded-full animate-spin" />
          Loading stats…
        </div>
        <template v-else-if="testTooltip.data">
          <div class="space-y-0.5">
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Total runs</span>
              <span class="text-gray-200">{{ testTooltip.data.totalRuns }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-green-400">Passed</span>
              <span class="text-gray-200">{{ testTooltip.data.passedRuns }}</span>
            </div>
            <div v-if="testTooltip.data.failedRuns > 0" class="flex justify-between gap-3">
              <span class="text-red-400">Failed</span>
              <span class="text-gray-200">{{ testTooltip.data.failedRuns }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Fail rate</span>
              <span :class="testTooltip.data.failedRuns / testTooltip.data.totalRuns >= 0.5 ? 'text-red-400' : testTooltip.data.failedRuns > 0 ? 'text-yellow-400' : 'text-gray-200'">
                {{ Math.round((testTooltip.data.failedRuns / testTooltip.data.totalRuns) * 100) }}%
              </span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Avg duration</span>
              <span class="text-gray-200">{{ formatTestDuration(testTooltip.data.avgDurationMs) }}</span>
            </div>
          </div>
        </template>
        <div v-else class="text-gray-600">No history data</div>
      </div>
    </Teleport>

    <!-- Trigger filter modal -->
    <Teleport to="body">
      <div v-if="showTriggerFilterModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="showTriggerFilterModal = false">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-sm">
          <h3 class="text-base font-semibold text-white mb-1">Filter workflows by trigger</h3>
          <p class="text-xs text-gray-500 mb-4">Show only workflows that are triggered by the selected events. Events matching this run's triggers are marked below.</p>

          <div class="space-y-2 mb-5">
            <label
              v-for="evt in allAvailableTriggers"
              :key="evt"
              class="flex items-center gap-3 cursor-pointer group">
              <input
                type="checkbox"
                :checked="selectedTriggerFilters.has(evt)"
                :aria-label="runWorkflowTriggers.includes(evt) ? `${evt} (used by this run)` : evt"
                class="rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500"
                @change="toggleTriggerFilter(evt)" />
              <span class="text-sm text-gray-300 group-hover:text-white transition-colors font-mono">{{ evt }}</span>
              <span v-if="runWorkflowTriggers.includes(evt)" class="ml-auto text-xs text-brand-400 font-medium">this run</span>
            </label>
            <div v-if="!allAvailableTriggers.length" class="text-xs text-gray-500">No trigger information available for these workflows.</div>
          </div>

          <div class="flex justify-between items-center">
            <button
              class="text-xs text-gray-500 hover:text-gray-300 transition-colors"
              @click="clearTriggerFilters">
              Clear all
            </button>
            <button
              class="px-4 py-1.5 text-sm bg-brand-600 hover:bg-brand-500 text-white rounded-md transition-colors"
              @click="showTriggerFilterModal = false">
              Done
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { useIssuesStore } from '~/stores/issues'
import { useProjectsStore } from '~/stores/projects'
import { CiCdRunStatus, type CiCdRunLog, type LinkedCiCdRun, type LinkedRunType, type CiCdTestCase, type TestStats } from '~/types'
import { parseAnsiToHtml, stripAnsiCodes } from '~/composables/useAnsiParser'
import { buildGraphJobIndexes, resolveLogJobId as resolveLogJobIdFn, matrixLabel as matrixLabelFn } from '~/utils/cicdLogMapper'
import { buildBranchUrl } from '~/utils/gitHub'

const route = useRoute()
const router = useRouter()
const projectId = route.params.id as string
const runId = route.params.runId as string

const store = useCiCdRunsStore()
const issuesStore = useIssuesStore()
const projectsStore = useProjectsStore()
const { prefs } = useUserPreferences()
const config = useRuntimeConfig()
const api = useApi()

/**
 * Pre-compiled regex cache for log color rules. Rebuilt whenever the rules list changes.
 * Maps rule id → compiled RegExp (or null if the pattern is invalid).
 */
const compiledColorRules = computed(() =>
  prefs.value.logColorRules.map((rule) => {
    try { return { rule, re: new RegExp(rule.pattern) } } catch { return null }
  }).filter((r): r is { rule: typeof prefs.value.logColorRules[0]; re: RegExp } => r !== null),
)

/**
 * Renders a log line as HTML, applying ANSI color codes and custom regex color rules.
 * Always strips raw ANSI codes — when ansiColors is enabled they become colored spans,
 * otherwise they are simply removed so no `[90m` artifacts are shown.
 * When `highlight` is provided, all case-insensitive occurrences of the query are
 * wrapped in a <mark> element for visual emphasis.
 */
function renderLogLine(line: string, highlight?: string): string {
  let html = prefs.value.ansiColors ? parseAnsiToHtml(line) : stripAnsiCodes(line)

  // Apply the first matching regex color rule (line-level text color override).
  // The plain text (ANSI-stripped) version is computed once and shared across all rules.
  if (compiledColorRules.value.length) {
    const plain = stripAnsiCodes(line)
    for (const { rule, re } of compiledColorRules.value) {
      if (re.test(plain)) {
        html = `<span style="color:${rule.color}">${html}</span>`
        break
      }
    }
  }

  // Highlight search query hits — replace occurrences in text content only (not inside HTML tags).
  if (highlight && highlight.trim()) {
    const escapedQuery = highlight.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    const re = new RegExp(`(${escapedQuery})`, 'gi')
    // Split HTML by tags so we only replace in text nodes (even-indexed parts).
    html = html.split(/(<[^>]*>)/g).map((part, i) =>
      i % 2 === 1 ? part : part.replace(re, '<mark class="bg-yellow-400/40 text-yellow-100 rounded-sm not-italic">$1</mark>'),
    ).join('')
  }

  return html
}

const retrying = ref(false)
const approving = ref(false)
const showRetryModal = ref(false)
const retryOptions = reactive({
  keepContainerOnFailure: false,
  noDind: false,
  noVolumeMounts: false,
  customImage: '',
  customEntrypoint: '',
  customArgs: '',
  actRunnerImage: '',
  eventName: '',
  branch: '',
  commitSha: '',
  skipSteps: '',
  overrideSkipSteps: false,
})
const retryConflict = ref<{ message: string; activeRunIds: string[] } | null>(null)

// True when the run succeeded (green run) — action is "Retrigger" rather than "Retry".
const isRetrigger = computed(() =>
  store.currentRun?.status === CiCdRunStatus.Succeeded ||
  store.currentRun?.status === CiCdRunStatus.SucceededWithWarnings,
)

const sectionTabs = [
  { label: 'Jobs', value: 'jobs' },
  { label: 'Logs', value: 'logs' },
  { label: 'Tests', value: 'tests' },
  { label: 'Artifacts', value: 'artifacts' },
  { label: 'Details', value: 'details' },
]
const validSections = ['jobs', 'logs', 'tests', 'artifacts', 'details'] as const
type Section = typeof validSections[number]
const activeSection = computed({
  get: (): Section => {
    const tab = route.query.tab as string
    return validSections.includes(tab as Section) ? (tab as Section) : 'jobs'
  },
  set: (value: Section) => {
    if (route.query.tab !== value)
      router.push({ query: { ...route.query, tab: value } })
  },
})

/** Slim mode: hides log counts, yml file names and status labels in the job graph. */
const slimMode = ref(false)

/** Debug mode: shows original log IDs before fuzzy matching and all workflow triggers per box. */
const debugMode = ref(false)

/** Actual rendered heights of job boxes, keyed by job ID. Populated by ResizeObserver. */
const measuredBoxHeights = ref<Map<string, number>>(new Map())

/** ResizeObserver instance for tracking actual box heights. */
let boxObserver: ResizeObserver | null = null

/** Map from DOM element to job ID for ResizeObserver callbacks. */
const boxElementIds = new WeakMap<Element, string>()

/** Reverse map from job ID to the currently observed DOM element (for cleanup). */
const jobIdToBoxElement = new Map<string, HTMLElement>()

/** Registers a job box element with the ResizeObserver for height measurement. */
function registerJobBox(id: string, el: HTMLElement | null) {
  if (!boxObserver) return
  // Unobserve the previous element for this job (handles unmount / key change).
  const old = jobIdToBoxElement.get(id)
  if (old) {
    boxObserver.unobserve(old)
    jobIdToBoxElement.delete(id)
  }
  if (el) {
    boxElementIds.set(el, id)
    jobIdToBoxElement.set(id, el)
    boxObserver.observe(el)
  }
}

// Clear measured heights when slim mode or debug mode changes so stale measurements are not used.
watch([slimMode, debugMode], () => {
  measuredBoxHeights.value = new Map()
})

/** Currently hovered job ID for connection highlighting. */
const hoveredJob = ref<string | null>(null)

/** Log search query for filtering job log lines. */
const logSearchQuery = ref('')

/** Word/line wrap for log display. Off by default for better readability of long log lines. */
const wordWrap = ref(false)

/**
 * Per-job completion status received via backend `job-status` SignalR events.
 * Provides authoritative real-time completion state without requiring the frontend to
 * parse "Job succeeded" / "Job failed" strings from log lines.
 * Keyed by resolved graph node ID (via resolveLogJobId).
 */
const jobStatusMap = ref(new Map<string, { isComplete: boolean; hasError: boolean }>())

const streamTabs = [
  { label: 'All', value: null },
  { label: 'Stdout', value: 'stdout' },
  { label: 'Stderr', value: 'stderr' },
]
const activeStream = ref<string | null>(null)

// ── Workflow trigger filter modal ─────────────────────────────────────────────

/** Whether the trigger filter modal is open. */
const showTriggerFilterModal = ref(false)

/** The set of trigger event names the user has selected for filtering (empty = show all). */
const selectedTriggerFilters = ref(new Set<string>())

/** True when the graph contains jobs from more than one workflow file (filter is relevant). */
const hasMultipleWorkflowFiles = computed(() => {
  const files = new Set(
    (store.currentRunGraph?.jobs ?? []).map(j => j.workflowFile).filter(Boolean),
  )
  return files.size > 1
})

/** Sorted list of all unique trigger event names across all workflow files in the graph. */
const allAvailableTriggers = computed<string[]>(() => {
  const triggers = store.currentRunGraph?.workflowTriggers
  if (!triggers) return []
  const all = new Set<string>()
  for (const evts of Object.values(triggers))
    for (const evt of evts) all.add(evt)
  return Array.from(all).sort()
})

/** Triggers of the workflow that triggered this run (extracted from workflowTriggers). */
const runWorkflowTriggers = computed<string[]>(() => {
  const triggers = store.currentRunGraph?.workflowTriggers
  if (!store.currentRun?.workflow || !triggers) return []
  // Normalise backslashes (Windows-style paths) before extracting the filename.
  const workflowFile = store.currentRun.workflow.replace(/\\/g, '/').split('/').pop() ?? store.currentRun.workflow
  return triggers[workflowFile] ?? []
})

/** Set of workflow filenames whose triggers overlap with the selected filter events. Empty = show all. */
const triggerVisibleFiles = computed<Set<string>>(() => {
  if (selectedTriggerFilters.value.size === 0) return new Set()
  const allTriggers = store.currentRunGraph?.workflowTriggers
  if (!allTriggers) return new Set()

  const visible = new Set<string>()
  for (const [file, fileTriggers] of Object.entries(allTriggers)) {
    if (fileTriggers.some(t => selectedTriggerFilters.value.has(t))) {
      visible.add(file)
    }
  }
  return visible
})

function toggleTriggerFilter(evt: string) {
  const s = new Set(selectedTriggerFilters.value)
  if (s.has(evt)) s.delete(evt)
  else s.add(evt)
  selectedTriggerFilters.value = s
}

function clearTriggerFilters() {
  selectedTriggerFilters.value = new Set()
}

const selectedJob = ref<string | null>(null)
/** When a specific matrix instance is selected (rawId from act), only show that instance's logs. */
const selectedMatrixRawId = ref<string | null>(null)

function deselectJob() {
  selectedJob.value = null
  selectedMatrixRawId.value = null
}

const filteredLogs = computed(() =>
  activeStream.value === null
    ? store.currentRunLogs
    : store.currentRunLogs.filter(l => l.stream === activeStream.value)
)

// ── Log job ID ↔ graph node ID resolution ──────────────────────────────────────
// `act` emits job identifiers in its JSON logs using the job's display name (from
// the `name:` YAML field), not the YAML key. For matrix jobs it appends `-N`.
// Reusable workflow calls may prepend the workflow or caller job name with a `/`.
// Our graph node IDs use the file-stem / yaml-key format (e.g. "docker/build").
// We need a fuzzy resolver to map log IDs → graph node IDs to avoid duplicate boxes.

/** Pre-built lookup indexes so resolveLogJobId() runs in O(1) per call. */
const graphJobIndexes = computed(() => buildGraphJobIndexes(store.currentRunGraph?.jobs ?? []))

function resolveLogJobId(logId: string): string {
  return resolveLogJobIdFn(logId, graphJobIndexes.value)
}

const jobFilteredLogs = computed(() => {
  if (!selectedJob.value) return []
  // If a specific matrix instance is selected, filter to only that raw job ID.
  if (selectedMatrixRawId.value)
    return store.currentRunLogs.filter(l => l.jobId === selectedMatrixRawId.value)
  const entry = jobLogMap.value.get(selectedJob.value)
  const rawIds = entry?.rawJobIds
  if (rawIds && rawIds.size > 0)
    return store.currentRunLogs.filter(l => !!l.jobId && rawIds.has(l.jobId))
  return store.currentRunLogs.filter(l => l.jobId === selectedJob.value)
})

/** Groups job-filtered logs by step, preserving order. Each group has a stepId (null = no step) and its log lines. */
interface StepGroup { stepId: string | null; key: string; label: string; logs: CiCdRunLog[]; startTs?: string; endTs?: string }

/**
 * Parsed skip-step entries from the current run's skipSteps field.
 * Returns two sets: globalSkips (bare step names) and jobStepSkips (job:step pairs).
 */
const parsedSkipSteps = computed(() => {
  const raw = store.currentRun?.skipSteps
  if (!raw) return { global: new Set<string>(), jobStep: new Map<string, Set<string>>() }
  const global = new Set<string>()
  const jobStep = new Map<string, Set<string>>()
  for (const line of raw.split('\n').map(l => l.trim()).filter(l => l.length > 0)) {
    const colonIdx = line.indexOf(':')
    if (colonIdx > 0) {
      const jobId = line.slice(0, colonIdx).toLowerCase()
      const stepId = line.slice(colonIdx + 1)
      if (!jobStep.has(jobId)) jobStep.set(jobId, new Set())
      jobStep.get(jobId)!.add(stepId)
    } else {
      global.add(line)
    }
  }
  return { global, jobStep }
})

/**
 * Returns true if the given step is in the run's skip-step configuration.
 * Checks both bare step names and job:step pairs against the currently selected job.
 */
function isStepIgnored(stepId: string | null): boolean {
  if (!stepId) return false
  const { global, jobStep } = parsedSkipSteps.value
  if (global.has(stepId)) return true
  if (!selectedJob.value) return false
  // Normalise the selected job ID: strip workflow prefix (e.g. "CI/build" → "build")
  const jobPart = selectedJob.value.includes('/')
    ? selectedJob.value.slice(selectedJob.value.lastIndexOf('/') + 1)
    : selectedJob.value
  const steps = jobStep.get(jobPart.toLowerCase())
  return steps != null && steps.has(stepId)
}

const jobLogsByStep = computed<StepGroup[]>(() => {
  const groups: StepGroup[] = []
  let current: StepGroup | null = null
  let firstNullSeen = false
  for (const log of jobFilteredLogs.value) {
    if (current === null || log.stepId !== current.stepId) {
      let key: string
      let label: string
      if (log.stepId == null) {
        if (!firstNullSeen) {
          key = '__setup__'
          label = 'Set up job'
          firstNullSeen = true
        } else {
          key = '__complete__'
          label = 'Complete job'
        }
      } else {
        key = log.stepId
        label = log.stepId
      }
      current = { stepId: log.stepId ?? null, key, label, logs: [], startTs: log.timestamp }
      groups.push(current)
    }
    current.logs.push(log)
    current.endTs = log.timestamp
  }
  return groups
})

/** Filtered job log groups by search query. */
const filteredJobLogsByStep = computed<StepGroup[]>(() => {
  if (!logSearchQuery.value.trim()) return jobLogsByStep.value
  const query = logSearchQuery.value.toLowerCase()
  return jobLogsByStep.value.map(group => ({
    ...group,
    logs: group.logs.filter(l => stripAnsiCodes(l.line).toLowerCase().includes(query)),
  })).filter(g => g.logs.length > 0)
})

/** Set of collapsed step IDs (default: all named steps collapsed). */
const collapsedSteps = ref(new Set<string>())
/** Tracks which step IDs have already been auto-collapsed so we don't re-collapse them after manual expand. */
const seenStepIds = ref(new Set<string>())
/** Tracks count of steps in the previous render to detect when a new step starts. */
const prevStepCount = ref(0)
/** Steps the user has manually expanded — auto-collapse will not override these. */
const manuallyOpenedSteps = ref(new Set<string>())

// Auto-collapse logic:
// - A new step starts → collapse it unless it is the current (last) step or has failed.
// - When a new step starts, the previously-current step (now second-to-last) is collapsed unless it failed.
// - Failed steps are always kept open (even if previously collapsed).
// - Steps manually opened by the user are not re-collapsed automatically.
watch(jobLogsByStep, (groups) => {
  const newSeen = new Set(seenStepIds.value)
  const newCollapsed = new Set(collapsedSteps.value)

  for (let i = 0; i < groups.length; i++) {
    const g = groups[i]
    const key = g.key
    const isLast = i === groups.length - 1
    const hasFailed = g.logs.some(l => l.stream === 'stderr')
    const manuallyOpened = manuallyOpenedSteps.value.has(key)

    if (!newSeen.has(key)) {
      // First time we see this step: auto-collapse unless it's current, failed, or manually opened.
      newSeen.add(key)
      if (!isLast && !hasFailed && !manuallyOpened) newCollapsed.add(key)
    } else if (!isLast && !hasFailed && !manuallyOpened && groups.length > prevStepCount.value && i === groups.length - 2) {
      // A new step just appeared (groups grew by 1). The step at position groups.length-2 is
      // the one that was the last (current) step in the previous render — collapse it now that
      // it has been superseded by the new last step.
      newCollapsed.add(key)
    }

    // Failed steps must always stay open regardless of previous state.
    if (hasFailed) newCollapsed.delete(key)
  }

  seenStepIds.value = newSeen
  collapsedSteps.value = newCollapsed
  prevStepCount.value = groups.length
}, { immediate: true })

function toggleStep(stepId: string) {
  const s = new Set(collapsedSteps.value)
  const m = new Set(manuallyOpenedSteps.value)
  if (s.has(stepId)) {
    s.delete(stepId)
    m.add(stepId)  // user manually opened this step
  } else {
    s.add(stepId)
    m.delete(stepId)  // user manually closed this step
  }
  collapsedSteps.value = s
  manuallyOpenedSteps.value = m
}

function stepDuration(group: StepGroup): string | null {
  if (!group.startTs || !group.endTs || group.startTs === group.endTs) return null
  const ms = new Date(group.endTs).getTime() - new Date(group.startTs).getTime()
  // Sub-second or out-of-order timestamps are treated as unmeasurable — show nothing.
  if (ms <= 0) return null
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  return `${Math.floor(s / 60)}m ${s % 60}s`
}

// ── Job enrichment ─────────────────────────────────────────────────────────────
// Merges graph data (from YAML) with live log data to produce a unified list of
// job nodes with status, log counts, and layout coordinates.

interface MatrixInstance {
  rawId: string
  hasError: boolean
  isComplete: boolean
  hasStarted: boolean
  startedAt?: string
  endedAt?: string
}

interface EnrichedJob {
  id: string
  name: string
  needs: string[]
  logCount: number
  hasError: boolean
  isComplete: boolean
  hasStarted: boolean
  workflowFile?: string
  callerWorkflowFile?: string
  startedAt?: string
  endedAt?: string
  /** Number of distinct act job IDs (log streams) that map to this graph node. >1 means matrix job. */
  matrixCount: number
  /** Per-instance matrix data when matrixCount > 1. */
  matrixInstances: MatrixInstance[]
  /** Raw act log job IDs that were resolved to this graph node (for debug mode). */
  rawJobIds: string[]
  /** All trigger event names for the workflow file containing this job (for debug mode). */
  workflowTriggers: string[]
  // layout
  x: number
  y: number
  boxHeight: number
}

const jobLogMap = computed(() => {
  type InstanceEntry = { logCount: number; hasError: boolean; isComplete: boolean; startedAt?: string; endedAt?: string }
  const makeEntry = (): InstanceEntry => ({ logCount: 0, hasError: false, isComplete: false })
  const map = new Map<string, {
    logCount: number; hasError: boolean; isComplete: boolean; startedAt?: string; endedAt?: string; rawJobIds: Set<string>
    // Per-instance tracking for matrix jobs
    instances: Map<string, InstanceEntry>
  }>()
  for (const log of store.currentRunLogs) {
    if (!log.jobId) continue
    // Resolve the act job ID to the matching graph node ID (fuzzy: display names, matrix suffix, compound path).
    const resolvedId = resolveLogJobId(log.jobId)
    if (!map.has(resolvedId)) map.set(resolvedId, { ...makeEntry(), rawJobIds: new Set(), instances: new Map() })
    const entry = map.get(resolvedId)!
    entry.logCount++
    entry.rawJobIds.add(log.jobId)
    if (log.stream === 'stderr') entry.hasError = true
    // Track first and last log timestamps as job start/end
    if (!entry.startedAt) entry.startedAt = log.timestamp
    entry.endedAt = log.timestamp
    // act v0.2.84+ emits "🏁  Job succeeded" / "🏁  Job failed" (emoji prefix); use endsWith.
    if (log.line.endsWith('Job succeeded') || log.line.endsWith('Job failed')) entry.isComplete = true
    // Per-instance tracking
    if (!entry.instances.has(log.jobId)) entry.instances.set(log.jobId, makeEntry())
    const inst = entry.instances.get(log.jobId)!
    inst.logCount++
    if (log.stream === 'stderr') inst.hasError = true
    if (!inst.startedAt) inst.startedAt = log.timestamp
    inst.endedAt = log.timestamp
    if (log.line.endsWith('Job succeeded') || log.line.endsWith('Job failed')) inst.isComplete = true
  }
  return map
})

// When the overall run is done, all tracked jobs are also complete (they can't still be running).
const runIsTerminal = computed(() => {
  const s = store.currentRun?.status
  return s === CiCdRunStatus.Succeeded || s === CiCdRunStatus.Failed || s === CiCdRunStatus.Cancelled || s === CiCdRunStatus.SucceededWithWarnings
})

/** Parsed inputs dictionary for workflow_dispatch runs. Null when no inputs are stored. */
const runInputs = computed<Record<string, string> | null>(() => {
  const json = store.currentRun?.inputsJson
  if (!json) return null
  try {
    return JSON.parse(json) as Record<string, string>
  } catch (e) {
    console.warn('Failed to parse run inputsJson:', e)
    return null
  }
})

// Build the enriched job list by unioning graph nodes with log-observed jobs.
// Graph nodes get their needs/name from the YAML; log-only jobs fall back to id as name.

/** Layout constants. */
const BOX_W = 220
// Estimated box height breakdown (normal mode):
//   name(20) + statusBadge(22) + logCount(16) + padding-tb(24) + inner gaps(~16) ≈ 98px (no workflow/timing)
//   + workflowFile(16) + timing(16) = 130px for a started job with workflow file info
const BASE_BOX_H = 130
// Slim mode hides status badge (~22px), log count (~16px), and workflow file (~16px),
// reducing the box height: 130 - 22 - 16 - 16 ≈ 76px; 80px with a small safety margin.
const SLIM_BOX_H = 80
const MATRIX_ROW_H = 24
const COL_GAP = 80
const ROW_GAP = 14
const LAYOUT_PAD = 16

/** Computes box height for a job using the actual measured height or an estimate. */
function computeBoxH(id: string): number {
  const measured = measuredBoxHeights.value.get(id)
  if (measured) return measured
  const instances = jobLogMap.value.get(id)?.instances
  const instanceCount = instances ? instances.size : 0
  const matrixRows = instanceCount > 1 ? Math.ceil(instanceCount / 2) : 0
  const baseH = slimMode.value ? SLIM_BOX_H : BASE_BOX_H
  return baseH + matrixRows * MATRIX_ROW_H
}

/**
 * Computes x/y positions for a set of job IDs via BFS column assignment.
 * Edges between IDs outside the provided set are ignored, so the layout
 * is compact (no gaps) when only a filtered subset is being positioned.
 */
function computePositions(
  jobIds: Set<string>,
  edges: { from: string; to: string }[],
): Map<string, { x: number; y: number }> {
  const colMap = new Map<string, number>()
  const inDegree = new Map<string, number>()
  for (const id of jobIds) {
    if (!inDegree.has(id)) inDegree.set(id, 0)
  }
  for (const e of edges) {
    if (jobIds.has(e.from) && jobIds.has(e.to))
      inDegree.set(e.to, (inDegree.get(e.to) ?? 0) + 1)
  }

  const queue: string[] = []
  for (const [id, deg] of inDegree) {
    if (deg === 0) queue.push(id)
  }
  while (queue.length) {
    const id = queue.shift()!
    const col = colMap.get(id) ?? 0
    for (const e of edges) {
      if (e.from === id && jobIds.has(e.to)) {
        const nextCol = Math.max(colMap.get(e.to) ?? 0, col + 1)
        colMap.set(e.to, nextCol)
        const newDeg = (inDegree.get(e.to) ?? 1) - 1
        inDegree.set(e.to, newDeg)
        if (newDeg === 0) queue.push(e.to)
      }
    }
  }

  const byCol = new Map<number, string[]>()
  for (const id of jobIds) {
    const col = colMap.get(id) ?? 0
    if (!byCol.has(col)) byCol.set(col, [])
    byCol.get(col)!.push(id)
  }

  const posMap = new Map<string, { x: number; y: number }>()
  const sortedCols = Array.from(byCol.keys()).sort((a, b) => a - b)
  let x = LAYOUT_PAD
  for (const col of sortedCols) {
    const jobs = byCol.get(col)!
    let y = LAYOUT_PAD
    for (const id of jobs) {
      posMap.set(id, { x, y })
      y += computeBoxH(id) + ROW_GAP
    }
    x += BOX_W + COL_GAP
  }
  return posMap
}

const enrichedJobs = computed<EnrichedJob[]>(() => {
  // Collect all job IDs
  const graphJobs = store.currentRunGraph?.jobs ?? []
  const logJobIds = Array.from(jobLogMap.value.keys())
  const allIds = new Set([...graphJobs.map(j => j.id), ...logJobIds])

  // Build a map of job metadata
  const jobMeta = new Map(graphJobs.map(j => [j.id, { name: j.name, needs: j.needs, workflowFile: j.workflowFile, callerWorkflowFile: j.callerWorkflowFile }]))

  const edges = store.currentRunGraph?.edges ?? []
  const wfTriggers = store.currentRunGraph?.workflowTriggers

  // Pre-build a map of which jobs have started (have log lines) for downstream inference.
  const startedIds = new Set(Array.from(allIds).filter(id => (jobLogMap.value.get(id)?.logCount ?? 0) > 0))
  // A job is implicitly complete if any of its downstream jobs (direct or transitive) has started.
  // Reverse BFS: seed with direct parents of all started jobs, then walk backwards through edges (O(V+E)).
  const implicitlyCompleteIds = new Set<string>()
  // Build reverse adjacency map (child → set of parents)
  const reverseAdj = new Map<string, string[]>()
  for (const e of edges) {
    if (!reverseAdj.has(e.to)) reverseAdj.set(e.to, [])
    reverseAdj.get(e.to)!.push(e.from)
  }
  const bfsQueue: string[] = []
  for (const id of startedIds) {
    for (const parent of (reverseAdj.get(id) ?? [])) {
      if (!implicitlyCompleteIds.has(parent)) {
        implicitlyCompleteIds.add(parent)
        bfsQueue.push(parent)
      }
    }
  }
  while (bfsQueue.length > 0) {
    const curr = bfsQueue.shift()!
    for (const parent of (reverseAdj.get(curr) ?? [])) {
      if (!implicitlyCompleteIds.has(parent)) {
        implicitlyCompleteIds.add(parent)
        bfsQueue.push(parent)
      }
    }
  }

  const posMap = computePositions(allIds, edges)

  return Array.from(allIds).map(id => {
    const meta = jobMeta.get(id)
    const logs = jobLogMap.value.get(id) ?? { logCount: 0, hasError: false, isComplete: false, instances: new Map(), rawJobIds: new Set<string>() }
    const pos = posMap.get(id) ?? { x: LAYOUT_PAD, y: LAYOUT_PAD }
    const hasStarted = logs.logCount > 0
    // Backend-emitted job-status event takes precedence as the authoritative completion signal.
    // Log-based detection (logs.isComplete) covers historical data loaded on page open.
    // implicitlyCompleteIds provides heuristic coverage before the event arrives.
    const statusEvent = jobStatusMap.value.get(id)
    const isComplete = statusEvent?.isComplete || logs.isComplete || (hasStarted && runIsTerminal.value) || implicitlyCompleteIds.has(id)
    const hasError = (statusEvent?.hasError ?? false) || logs.hasError
    const matrixCount = logs.rawJobIds?.size ?? (hasStarted ? 1 : 0)

    // Build per-instance matrix data for the grouped display.
    const matrixInstances: MatrixInstance[] = matrixCount > 1
      ? Array.from(logs.instances.entries()).map(([rawId, inst]) => ({
          rawId,
          hasError: inst.hasError,
          isComplete: inst.isComplete || (inst.logCount > 0 && runIsTerminal.value),
          hasStarted: inst.logCount > 0,
          startedAt: inst.startedAt,
          endedAt: inst.endedAt,
        }))
      : []

    const workflowFile = meta?.workflowFile
    return {
      id,
      name: meta?.name ?? id,
      needs: meta?.needs ?? [],
      logCount: logs.logCount,
      hasError,
      hasStarted,
      isComplete,
      workflowFile,
      callerWorkflowFile: meta?.callerWorkflowFile,
      matrixCount,
      matrixInstances,
      rawJobIds: logs.rawJobIds ? Array.from(logs.rawJobIds) : [],
      workflowTriggers: workflowFile ? Array.from(wfTriggers?.[workflowFile] ?? []) : [],
      startedAt: logs.startedAt,
      // When run ended but the job never emitted a final timestamp, use the run's end time.
      endedAt: isComplete && !logs.endedAt ? (store.currentRun?.endedAt ?? logs.startedAt) : logs.endedAt,
      x: pos.x,
      y: pos.y,
      boxHeight: computeBoxH(id),
    }
  })
})

/** Jobs visible after applying slim-mode trigger filtering and the user-selected trigger filter. */
const visibleJobs = computed<EnrichedJob[]>(() => {
  const edges = store.currentRunGraph?.edges ?? []
  const wfTriggers = store.currentRunGraph?.workflowTriggers

  let filtered = enrichedJobs.value

  // Slim mode: hide boxes whose workflow file triggers don't include the current run's event name.
  if (slimMode.value && store.currentRun?.eventName && wfTriggers) {
    const eventName = store.currentRun.eventName
    filtered = filtered.filter(j => {
      if (!j.workflowFile) return true  // no workflow info — keep
      // For nested workflow jobs (called via uses:), the job runs because the CALLER triggered —
      // check the caller's triggers instead of the nested file's own triggers.
      // e.g. backend.yml has `on: workflow_call` but runs when ci.yml (on: push) calls it.
      if (j.callerWorkflowFile) {
        const callerTriggers = wfTriggers[j.callerWorkflowFile] ?? []
        if (callerTriggers.includes(eventName)) return true
      }
      const triggers = wfTriggers[j.workflowFile] ?? []
      return triggers.includes(eventName)
    })
    // Cascade: also hide downstream jobs whose every direct dependency was just filtered out.
    // This prevents orphaned boxes from appearing when their prerequisite jobs are hidden.
    const visibleSet = new Set(filtered.map(j => j.id))
    let changed = true
    while (changed) {
      changed = false
      filtered = filtered.filter(j => {
        if (j.needs.length === 0) return true  // root job — always keep
        // Hide if ALL declared needs are now invisible.
        if (j.needs.every(n => !visibleSet.has(n))) {
          visibleSet.delete(j.id)
          changed = true
          return false
        }
        return true
      })
    }
  }

  // Apply user-selected trigger filter
  if (selectedTriggerFilters.value.size > 0 && triggerVisibleFiles.value.size > 0)
    filtered = filtered.filter(j => !j.workflowFile || triggerVisibleFiles.value.has(j.workflowFile))

  // If no jobs were filtered out, return as-is (positions already correct).
  if (filtered.length === enrichedJobs.value.length) return filtered

  // Recompute layout for the filtered subset so columns are compact (no gaps).
  const visibleIds = new Set(filtered.map(j => j.id))
  const posMap = computePositions(visibleIds, edges)
  return filtered.map(j => {
    const pos = posMap.get(j.id)
    if (!pos) return j
    return { ...j, x: pos.x, y: pos.y, boxHeight: computeBoxH(j.id) }
  })
})

/**
 * Raw act job IDs from logs that could not be resolved to any graph node.
 * These are log lines that arrived for a job the graph doesn't know about (no matching node by ID or name).
 * Shown as a warning in the Jobs tab so users know some logs are not associated with a graph box.
 */
const unmatchedLogJobIds = computed<string[]>(() => {
  if (!store.currentRunGraph) return []
  const graphNodeIds = new Set(enrichedJobs.value.map(j => j.id))
  const unmatched = new Set<string>()
  for (const log of store.currentRunLogs) {
    if (!log.jobId) continue
    const resolved = resolveLogJobId(log.jobId)
    // If resolved ID is NOT in the graph (i.e., act emitted it but we have no graph node), warn.
    if (!graphNodeIds.has(resolved)) unmatched.add(log.jobId)
  }
  return Array.from(unmatched).sort()
})

/**
 * Set of job IDs connected to the currently hovered job (all ancestors + descendants via edges).
 * Used for visual highlighting of related nodes and edges on hover.
 */
const connectedJobIds = computed<Set<string>>(() => {
  if (!hoveredJob.value) return new Set()
  const edges = store.currentRunGraph?.edges ?? []
  const connected = new Set<string>()

  // BFS forward (downstream) and backward (upstream)
  const queue = [hoveredJob.value]
  const visited = new Set([hoveredJob.value])
  while (queue.length) {
    const id = queue.shift()!
    for (const e of edges) {
      if (e.from === id && !visited.has(e.to)) {
        visited.add(e.to)
        connected.add(e.to)
        queue.push(e.to)
      }
      if (e.to === id && !visited.has(e.from)) {
        visited.add(e.from)
        connected.add(e.from)
        queue.push(e.from)
      }
    }
  }
  return connected
})

/**
 * Set of job IDs that are blocked because a predecessor job has failed.
 * Blocked jobs have not started and have at least one failed transitive dependency.
 */
const blockedJobIds = computed<Set<string>>(() => {
  const failedIds = new Set(enrichedJobs.value.filter(j => j.hasError && j.isComplete).map(j => j.id))
  if (failedIds.size === 0) return new Set()
  const edges = store.currentRunGraph?.edges ?? []
  const blocked = new Set<string>()
  // BFS forward from failed jobs; only mark not-started jobs as blocked
  const queue = Array.from(failedIds)
  const visited = new Set(queue)
  while (queue.length) {
    const id = queue.shift()!
    for (const e of edges) {
      if (e.from === id && !visited.has(e.to)) {
        visited.add(e.to)
        const job = enrichedJobs.value.find(j => j.id === e.to)
        if (job && !job.hasStarted) {
          blocked.add(e.to)
          queue.push(e.to)
        }
      }
    }
  }
  return blocked
})

// ── SVG graph layout ───────────────────────────────────────────────────────────

interface SvgEdge { path: string; highlighted: boolean; isFailure: boolean }

const graphLayout = computed<{ svgWidth: number; svgHeight: number; edges: SvgEdge[] }>(() => {
  if (!visibleJobs.value.length) return { svgWidth: 0, svgHeight: 0, edges: [] }

  const visibleIds = new Set(visibleJobs.value.map(j => j.id))
  const posMap = new Map(visibleJobs.value.map(j => [j.id, { x: j.x, y: j.y, boxHeight: j.boxHeight }]))
  const highlightedIds = connectedJobIds.value
  const hovered = hoveredJob.value
  const failedIds = new Set(enrichedJobs.value.filter(j => j.hasError && j.isComplete).map(j => j.id))
  const blocked = blockedJobIds.value

  const edges = (store.currentRunGraph?.edges ?? [])
    .filter(e => visibleIds.has(e.from) && visibleIds.has(e.to))
    .map(e => {
    const from = posMap.get(e.from)
    const to = posMap.get(e.to)
    if (!from || !to) return null

    const x1 = from.x + BOX_W
    const y1 = from.y + from.boxHeight / 2
    const x2 = to.x
    const y2 = to.y + to.boxHeight / 2
    const cx = (x1 + x2) / 2
    const highlighted = hovered !== null && (e.from === hovered || e.to === hovered || highlightedIds.has(e.from) || highlightedIds.has(e.to))
    const isFailure = failedIds.has(e.from) && blocked.has(e.to)

    return { path: `M ${x1} ${y1} C ${cx} ${y1}, ${cx} ${y2}, ${x2} ${y2}`, highlighted, isFailure }
  }).filter((e): e is SvgEdge => e !== null)

  const maxX = Math.max(...visibleJobs.value.map(j => j.x)) + BOX_W + LAYOUT_PAD
  const maxY = Math.max(...visibleJobs.value.map(j => j.y + j.boxHeight)) + LAYOUT_PAD

  return { svgWidth: maxX, svgHeight: maxY, edges }
})

function toggleJobFilter(jobId: string) {
  if (selectedJob.value === jobId) {
    deselectJob()
  } else {
    selectedJob.value = jobId
    selectedMatrixRawId.value = null
    // Reset collapsed steps so each job starts with all steps collapsed by default.
    collapsedSteps.value = new Set()
    seenStepIds.value = new Set()
    prevStepCount.value = 0
    manuallyOpenedSteps.value = new Set()
    logSearchQuery.value = ''
  }
}

function selectMatrixInstance(jobId: string, rawId: string) {
  selectedJob.value = jobId
  selectedMatrixRawId.value = selectedMatrixRawId.value === rawId ? null : rawId
  collapsedSteps.value = new Set()
  seenStepIds.value = new Set()
  prevStepCount.value = 0
  manuallyOpenedSteps.value = new Set()
  logSearchQuery.value = ''
}

function jobStatusDot(job: Pick<EnrichedJob, 'hasError' | 'isComplete' | 'hasStarted'>) {
  if (!job.hasStarted) return runIsTerminal.value ? 'bg-gray-500' : 'bg-gray-600 animate-pulse'
  if (job.hasError) return 'bg-red-400'
  if (!job.isComplete) return 'bg-blue-400 animate-pulse'
  return 'bg-green-400'
}

function jobStatusClass(job: Pick<EnrichedJob, 'hasError' | 'isComplete' | 'hasStarted'>) {
  if (!job.hasStarted) return 'bg-gray-800/50 text-gray-500'
  if (job.hasError) return 'bg-red-900/30 text-red-400'
  if (!job.isComplete) return 'bg-blue-900/30 text-blue-400'
  return 'bg-green-900/30 text-green-400'
}

function jobStatusLabel(job: Pick<EnrichedJob, 'hasError' | 'isComplete' | 'hasStarted'>) {
  if (!job.hasStarted) return runIsTerminal.value ? 'Cancelled' : 'Waiting'
  if (job.hasError) return 'Failed'
  if (!job.isComplete) return 'Running'
  return 'Succeeded'
}

function jobDuration(start: string, end?: string) {
  const ms = (end ? new Date(end).getTime() : now.value) - new Date(start).getTime()
  if (ms < 0) return '—'
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  return `${m}m ${s % 60}s`
}

/**
 * Returns a short display label for a matrix instance button.
 * For workflow-prefixed rawIds (e.g. "Deploy GitHub Pages/Build") returns the last
 * segment of the prefix as discriminator (e.g. "Deploy GitHub Pages").
 * For simple matrix rawIds (e.g. "Build-2") returns the numeric index ("2").
 */
function matrixLabel(rawId: string, job: EnrichedJob): string {
  return matrixLabelFn(rawId, job.name)
}

// ── Create Issue from failed job ───────────────────────────────────────────────

const showCreateIssueModal = ref(false)
/** Job ID to filter logs for the create-issue modal. Empty string means all run logs (logs tab). */
const createIssueJobId = ref<string>('')
const createIssueTitle = ref('')
const createIssueLogScope = ref<'full' | 'tail' | 'errors' | 'filter'>('errors')
/** Regex/keyword pattern used when createIssueLogScope === 'filter'. */
const createIssueFilterPattern = ref('error|fault|warn|fail|exception')
/** When true, verbose info (act/IssuePit versions from debug metadata) is appended to the issue body. */
const createIssueVerbose = ref(false)
const creatingIssue = ref(false)
const createIssueError = ref<string | null>(null)

const logScopeOptions = [
  { label: 'Errors only', value: 'errors' as const },
  { label: 'Filter by keywords', value: 'filter' as const },
  { label: 'Last 50 lines', value: 'tail' as const },
  { label: 'Full log', value: 'full' as const },
]

/**
 * Compiled regex for the filter scope. Null when the pattern is empty or invalid.
 * Recompiles only when the pattern changes.
 */
const createIssueCompiledRegex = computed<{ re: RegExp; valid: true } | { re: null; valid: false }>(() => {
  const pattern = createIssueFilterPattern.value.trim()
  if (!pattern) return { re: null, valid: false }
  try {
    return { re: new RegExp(pattern, 'i'), valid: true }
  } catch {
    return { re: null, valid: false }
  }
})

/** Returns whether the current filter pattern is an invalid (unparseable) regex. */
const createIssueFilterPatternInvalid = computed(
  () => createIssueLogScope.value === 'filter' && createIssueFilterPattern.value.trim() !== '' && !createIssueCompiledRegex.value.valid,
)

/**
 * Returns the log lines for the job identified by createIssueJobId.
 * Uses rawJobIds from jobLogMap to match act's workflow-qualified names (e.g. "CI/build").
 */
function getLogsForJobId(jobId: string): CiCdRunLog[] {
  const entry = jobLogMap.value.get(jobId)
  const rawIds = entry?.rawJobIds
  if (rawIds && rawIds.size > 0)
    return store.currentRunLogs.filter(l => !!l.jobId && rawIds.has(l.jobId))
  return store.currentRunLogs.filter(l => l.jobId === jobId)
}

const createIssuePreviewLines = computed(() => {
  // When a specific job is selected, try to get its logs. If none are found (e.g. the job
  // never produced log output), fall back to all run logs so the modal is never empty.
  let sourceLogs = createIssueJobId.value ? getLogsForJobId(createIssueJobId.value) : store.currentRunLogs
  if (sourceLogs.length === 0 && createIssueJobId.value && store.currentRunLogs.length > 0)
    sourceLogs = store.currentRunLogs
  if (createIssueLogScope.value === 'errors') return sourceLogs.filter(l => l.stream === 'stderr').map(l => l.line)
  if (createIssueLogScope.value === 'tail') return sourceLogs.slice(-50).map(l => l.line)
  if (createIssueLogScope.value === 'filter') {
    const { re } = createIssueCompiledRegex.value
    if (!re) return sourceLogs.map(l => l.line)
    return sourceLogs.filter(l => re.test(l.line)).map(l => l.line)
  }
  return sourceLogs.map(l => l.line)
})

function openCreateIssueModal(jobId: string) {
  createIssueJobId.value = jobId
  const workflow = store.currentRun?.workflow ? ` (${store.currentRun.workflow})` : ''
  createIssueTitle.value = jobId
    ? `CI/CD job "${jobId}" failed${workflow}`
    : `CI/CD run failed${workflow}`
  // Default to keyword filter (matches error/fail lines in stdout) rather than 'errors' (stderr-only),
  // because most CI runtimes (e.g. act) write all output including failures to stdout.
  createIssueLogScope.value = 'filter'
  createIssueFilterPattern.value = 'error|fault|warn|fail|exception'
  createIssueVerbose.value = false
  createIssueError.value = null
  showCreateIssueModal.value = true
}

async function submitCreateIssue() {
  if (!createIssueTitle.value.trim()) return
  creatingIssue.value = true
  createIssueError.value = null
  try {
    const logLines = createIssuePreviewLines.value
    const scopeLabel = createIssueLogScope.value === 'filter'
      ? `filter: ${createIssueFilterPattern.value}`
      : createIssueLogScope.value
    const logText = logLines.length
      ? `\n\n**Failed job logs** (scope: ${scopeLabel}):\n\`\`\`\n${logLines.join('\n')}\n\`\`\``
      : ''
    const branch = store.currentRun?.branch
    const commit = store.currentRun?.commitSha
    const context = createIssueJobId.value
      ? `CI/CD run **${runId.slice(0, 8)}** failed at job **${createIssueJobId.value}**.`
      : `CI/CD run **${runId.slice(0, 8)}** failed.`
    const branchLine = branch ? `\n- **Branch:** \`${branch}\`` : ''
    const commitLine = commit ? `\n- **Commit:** \`${commit.slice(0, 7)}\`` : ''
    const verboseSection = createIssueVerbose.value && debugMetadata.value.length
      ? `\n\n**Debug info:**\n${debugMetadata.value.map(entry => `- ${entry.key}: \`${entry.value}\``).join('\n')}`
      : ''
    const body = `${context}${branchLine}${commitLine}${logText}${verboseSection}`

    const newIssue = await issuesStore.createIssue(projectId, {
      title: createIssueTitle.value.trim(),
      body,
      gitBranch: branch,
    })
    showCreateIssueModal.value = false
    if (newIssue) {
      await navigateTo(`/projects/${projectId}/issues/${newIssue.number}`)
    }
  } catch (e: unknown) {
    createIssueError.value = e instanceof Error ? e.message : 'Failed to create issue'
  } finally {
    creatingIssue.value = false
  }
}

// ── Tests tab enhancements ─────────────────────────────────────────────────────

/** Set of suite IDs that are currently collapsed. */
const collapsedSuites = ref<Set<string>>(new Set())

/** When true, only failed test cases are shown in each suite. */
const showFailedOnly = ref(false)

/** Aggregate stats computed from all test suites of the current run. */
const testRunStats = computed(() => {
  const suites = store.currentRunTestSuites
  const total = suites.reduce((s, suite) => s + suite.totalTests, 0)
  const passed = suites.reduce((s, suite) => s + suite.passedTests, 0)
  const failed = suites.reduce((s, suite) => s + suite.failedTests, 0)
  const skipped = suites.reduce((s, suite) => s + suite.skippedTests, 0)
  const duration = suites.reduce((s, suite) => s + suite.durationMs, 0)
  return { total, passed, failed, skipped, duration, failRate: total > 0 ? failed / total : 0 }
})

/** Suites after applying the "failed only" filter (suites with no remaining tests are hidden). */
const filteredSuites = computed(() => {
  if (!showFailedOnly.value) return store.currentRunTestSuites
  return store.currentRunTestSuites
    .map(suite => ({
      ...suite,
      testCases: suite.testCases.filter(tc => tc.outcomeName === 'Failed'),
    }))
    .filter(suite => suite.testCases.length > 0)
})

function toggleSuite(suiteId: string) {
  const next = new Set(collapsedSuites.value)
  if (next.has(suiteId)) next.delete(suiteId)
  else next.add(suiteId)
  collapsedSuites.value = next
}

function collapseAllSuites() {
  collapsedSuites.value = new Set(store.currentRunTestSuites.map(s => s.id))
}

function expandAllSuites() {
  collapsedSuites.value = new Set()
}

// ── Test tooltip ────────────────────────────────────────────────────────────────

const testTooltip = ref<{
  visible: boolean
  x: number
  y: number
  loading: boolean
  data: TestStats | null
  fullName: string
}>({ visible: false, x: 0, y: 0, loading: false, data: null, fullName: '' })

let testTooltipTimer: ReturnType<typeof setTimeout> | null = null

async function showTestTooltip(e: MouseEvent, tc: CiCdTestCase) {
  if (testTooltipTimer) clearTimeout(testTooltipTimer)
  testTooltipTimer = setTimeout(async () => {
    testTooltip.value = { visible: true, x: e.clientX, y: e.clientY, loading: true, data: null, fullName: tc.fullName }
    try {
      const results = await api.get<TestStats[]>(
        `/api/projects/${projectId}/test-history/tests?search=${encodeURIComponent(tc.fullName)}&take=10`,
      )
      const match = results.find(r => r.fullName === tc.fullName) ?? null
      testTooltip.value = { ...testTooltip.value, loading: false, data: match }
    } catch {
      testTooltip.value = { ...testTooltip.value, loading: false, data: null }
    }
  }, 300)
}

function moveTestTooltip(e: MouseEvent) {
  if (testTooltip.value.visible)
    testTooltip.value = { ...testTooltip.value, x: e.clientX, y: e.clientY }
}

function hideTestTooltip() {
  if (testTooltipTimer) { clearTimeout(testTooltipTimer); testTooltipTimer = null }
  testTooltip.value = { ...testTooltip.value, visible: false }
}

// ── Create Issue from failed test ───────────────────────────────────────────────

/** When set, the "create issue from test" modal is shown. */
const createIssueTestCase = ref<(CiCdTestCase & { suiteName: string }) | null>(null)
const createIssueFromTestTitle = ref('')
const createIssueFromTestError = ref<string | null>(null)
const creatingIssueFromTest = ref(false)

function openCreateIssueFromTest(tc: CiCdTestCase, suiteName: string) {
  createIssueTestCase.value = { ...tc, suiteName }
  createIssueFromTestTitle.value = `Test "${tc.methodName || tc.fullName}" failed`
  createIssueFromTestError.value = null
  creatingIssueFromTest.value = false
}

async function submitCreateIssueFromTest() {
  if (!createIssueTestCase.value || !createIssueFromTestTitle.value.trim()) return
  creatingIssueFromTest.value = true
  createIssueFromTestError.value = null
  const tc = createIssueTestCase.value
  try {
    const branch = store.currentRun?.branch
    const commit = store.currentRun?.commitSha
    const branchLine = branch ? `\n- **Branch:** \`${branch}\`` : ''
    const commitLine = commit ? `\n- **Commit:** \`${commit.slice(0, 7)}\`` : ''
    const suiteLine = `\n- **Suite:** \`${tc.suiteName}\``
    const runLine = `\n- **Run:** [${runId.slice(0, 8)}](/projects/${projectId}/runs/cicd/${runId}?tab=tests)`
    const errorSection = tc.errorMessage
      ? `\n\n**Error:**\n\`\`\`\n${tc.errorMessage}\n\`\`\``
      : ''
    const stackSection = tc.stackTrace
      ? `\n\n**Stack trace:**\n\`\`\`\n${tc.stackTrace}\n\`\`\``
      : ''
    const body = `Test **\`${tc.fullName}\`** failed.${branchLine}${commitLine}${suiteLine}${runLine}${errorSection}${stackSection}`
    const newIssue = await issuesStore.createIssue(projectId, {
      title: createIssueFromTestTitle.value.trim(),
      body,
      gitBranch: branch,
    })
    createIssueTestCase.value = null
    if (newIssue) {
      await navigateTo(`/projects/${projectId}/issues/${newIssue.number}`)
    }
  } catch (e: unknown) {
    createIssueFromTestError.value = e instanceof Error ? e.message : 'Failed to create issue'
  } finally {
    creatingIssueFromTest.value = false
  }
}

// ── Debug metadata ─────────────────────────────────────────────────────────────

const debugMetadata = computed(() => {
  const entries: Array<{ key: string; value: string }> = []
  for (const log of store.currentRunLogs) {
    // Match lines like: [DEBUG] Key name   : value (space-colon-space separator)
    const m = log.line.match(/^\[DEBUG\]\s+([^:]+?)\s+:\s(.+)$/)
    if (m) entries.push({ key: m[1].trim(), value: m[2].trim() })
  }
  return entries
})

// SHA mismatch warning extracted from run logs (set when run is SucceededWithWarnings)
const shaWarningMessage = computed(() => {
  for (const log of store.currentRunLogs) {
    if (log.line.includes('[WARN] Commit SHA mismatch:')) return log.line
  }
  return null
})

// ── Image source helpers for the Details tab ───────────────────────────────────

/** The resolved 'Docker img src' value from debug metadata. */
const outerImgSourceLabel = computed(() =>
  debugMetadata.value.find(e => e.key === 'Docker img src')?.value ?? null,
)

/** True when a custom outer image (not the server-configured default) was used. */
const isNonDefaultOuterImage = computed(() =>
  outerImgSourceLabel.value === 'run-override',
)

/** The resolved 'Runner img src' value from debug metadata. */
const runnerImgSourceLabel = computed(() =>
  debugMetadata.value.find(e => e.key === 'Runner img src')?.value ?? null,
)

const runnerImgSourceBadge = computed(() => {
  switch (runnerImgSourceLabel.value) {
    case 'project': return 'project override'
    case 'org': return 'org override'
    case 'trigger-override': return 'run override'
    case 'server-config': return 'server config'
    case 'global-default': return 'global default'
    default: return null
  }
})

const runnerImgSourceBadgeClass = computed(() => {
  switch (runnerImgSourceLabel.value) {
    case 'project':
    case 'org':
      return 'text-yellow-400'
    case 'trigger-override':
      return 'text-orange-400'
    case 'server-config':
    case 'global-default':
    default:
      return 'text-gray-500'
  }
})

const runnerImgSourceTitle = computed(() => {
  switch (runnerImgSourceLabel.value) {
    case 'project': return 'Act runner image overridden at project level'
    case 'org': return 'Act runner image overridden at organization level'
    case 'trigger-override': return 'Act runner image explicitly set for this run'
    case 'server-config': return 'Act runner image from server configuration (CiCd__ActImage)'
    case 'global-default': return 'Act runner image using global hardcoded default'
    default: return 'Act runner image source unknown'
  }
})

/** Placeholder for the retry "Custom image" field: shows actual outer image used in this run. */
const currentOuterImagePlaceholder = computed(() => {
  const val = debugMetadata.value.find(e => e.key === 'Docker image')?.value
  return val ? `current: ${val}` : 'e.g. ghcr.io/issuepit/issuepit-helper-act:latest'
})

/** Placeholder for the retry "Act runner image" field: shows actual runner image used in this run. */
const currentActRunnerImagePlaceholder = computed(() => {
  const val = debugMetadata.value.find(e => e.key === 'Act runner img')?.value
  return val ? `current: ${val}` : 'e.g. catthehacker/ubuntu:act-latest'
})

// `now` is updated on each server-pushed event so the duration display stays live without a timer
const now = ref(Date.now())

// SignalR connections
const { connection: cicdConnection, isConnected, connect: connectCicd } = useSignalR('/hubs/cicd-output')
const { connection: projectConnection, connect: connectProject } = useSignalR('/hubs/project')

onMounted(async () => {
  // Set up ResizeObserver to track actual rendered heights of job boxes.
  boxObserver = new ResizeObserver((entries) => {
    const updated = new Map(measuredBoxHeights.value)
    let changed = false
    for (const entry of entries) {
      const id = boxElementIds.get(entry.target)
      if (!id) continue
      const h = Math.round(entry.borderBoxSize?.[0]?.blockSize ?? (entry.target as HTMLElement).offsetHeight ?? entry.contentRect.height)
      if (h > 0 && updated.get(id) !== h) {
        updated.set(id, h)
        changed = true
      }
    }
    if (changed) measuredBoxHeights.value = updated
  })

  await store.fetchRun(runId)
  await store.fetchTestResults(runId)
  await store.fetchArtifacts(runId)
  store.fetchLinkedRuns(runId)
  projectsStore.fetchProject(projectId)

  // Pre-select a job if the URL contains a `job` query param (e.g. from mini-graph tooltip click).
  const jobFromQuery = route.query.job as string | undefined
  if (jobFromQuery) {
    selectedJob.value = jobFromQuery
    activeSection.value = 'logs'
  }

  // Connect to the CiCd output hub to receive live log lines and run-completed events
  await connectCicd()
  if (cicdConnection.value) {
    await cicdConnection.value.invoke('JoinRun', runId).catch((e: unknown) => { console.warn('Failed to join run group', e) })
    cicdConnection.value.on('LogLine', (event: { runId: string; payload: string }) => {
      try {
        const data = JSON.parse(event.payload) as { event?: string; stream?: string; line?: string; jobId?: string; stepId?: string; timestamp?: string; status?: string }
        if (data.event === 'run-completed') {
          now.value = Date.now()
          // Refresh only run metadata (status, endedAt) — do NOT re-fetch logs to avoid losing scroll position
          store.fetchRunOnly(runId)
          // Fetch test results and artifacts now that the run has completed
          store.fetchTestResults(runId)
          store.fetchArtifacts(runId)
          store.fetchLinkedRuns(runId)
        } else if (data.event === 'run-heartbeat') {
          now.value = Date.now()
        } else if (data.event === 'job-status' && data.jobId) {
          // Authoritative per-job completion event emitted by the backend when act
          // reports "Job succeeded" / "Job failed". Update jobStatusMap so enrichedJobs
          // reflects the new state without waiting for the log line to be processed.
          const resolvedId = resolveLogJobId(data.jobId)
          const newMap = new Map(jobStatusMap.value)
          newMap.set(resolvedId, {
            isComplete: true,
            hasError: data.status === 'failed',
          })
          jobStatusMap.value = newMap
          now.value = Date.now()
        } else if (data.line !== undefined) {
          store.currentRunLogs.push({
            id: crypto.randomUUID(),
            line: data.line,
            stream: data.stream ?? 'stdout',
            streamName: data.stream ? (data.stream.charAt(0).toUpperCase() + data.stream.slice(1)) : 'Stdout',
            jobId: data.jobId,
            stepId: data.stepId,
            timestamp: data.timestamp ?? new Date().toISOString(),
          })
        }
      } catch (e) { console.warn('Failed to parse LogLine payload', e) }
    })
  }

  // Also connect to project hub to receive status changes (cancel, external CI/CD, run-completed via relay)
  await connectProject()
  if (projectConnection.value) {
    await projectConnection.value.invoke('JoinProject', projectId).catch((e: unknown) => { console.warn('Failed to join project group', e) })
    projectConnection.value.on('RunsUpdated', (data: { runId: string }) => {
      if (data.runId === runId) store.fetchRunOnly(runId)
    })
  }
})

onUnmounted(() => {
  boxObserver?.disconnect()
  boxObserver = null
  jobIdToBoxElement.clear()
})

async function retryRun() {
  await retryRunWithOptions()
}

async function approveRunAction() {
  approving.value = true
  try {
    await store.approveRun(runId)
  } finally {
    approving.value = false
  }
}

function openRetryModal() {
  retryOptions.eventName = store.currentRun?.eventName ?? 'push'
  retryOptions.branch = store.currentRun?.branch ?? ''
  retryOptions.commitSha = store.currentRun?.commitSha ?? ''
  retryOptions.skipSteps = store.currentRun?.skipSteps ?? ''
  retryOptions.overrideSkipSteps = false
  showRetryModal.value = true
}

function cancelRetry() {
  showRetryModal.value = false
  retryConflict.value = null
}

async function retryRunWithOptions(forceRetryWithActiveRunIds?: string[]) {
  retrying.value = true
  retryConflict.value = null
  showRetryModal.value = false
  try {
    await store.retryRun(runId, {
      keepContainerOnFailure: retryOptions.keepContainerOnFailure,
      forceRetryWithActiveRunIds,
      noDind: retryOptions.noDind,
      noVolumeMounts: retryOptions.noVolumeMounts,
      customImage: retryOptions.customImage.trim() || undefined,
      customEntrypoint: retryOptions.customEntrypoint.trim() || undefined,
      customArgs: retryOptions.customArgs.trim() || undefined,
      actRunnerImage: retryOptions.actRunnerImage.trim() || undefined,
      eventName: retryOptions.eventName.trim() || undefined,
      branch: retryOptions.branch.trim() || undefined,
      commitSha: retryOptions.commitSha.trim() || undefined,
      skipSteps: retryOptions.skipSteps.trim() || undefined,
      overrideSkipSteps: retryOptions.overrideSkipSteps,
    })
    navigateTo(`/projects/${projectId}/runs`)
  } catch (e: unknown) {
    // Handle 409 "already running" conflict — surface it in the options modal
    interface RetryConflictResponse { error?: string; canForce?: boolean; activeRunIds?: string[] }
    const data = (e as { data?: RetryConflictResponse })?.data
    if (data?.canForce) {
      retryConflict.value = {
        message: data.error ?? 'Another run is already in progress for this project.',
        activeRunIds: data.activeRunIds ?? [],
      }
      showRetryModal.value = true
    } else {
      store.error = e instanceof Error ? e.message : 'Failed to retry CI/CD run'
    }
  } finally {
    retrying.value = false
  }
}

async function copyLogsToClipboard() {
  const text = store.currentRunLogs.map(l => `${formatLogTime(l.timestamp)} ${l.line}`).join('\n')
  try {
    await navigator.clipboard.writeText(text)
  } catch {
    // Fallback: create a temporary textarea for environments where clipboard API is unavailable
    const ta = document.createElement('textarea')
    ta.value = text
    ta.style.position = 'fixed'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    document.execCommand('copy')
    document.body.removeChild(ta)
  }
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

const downloadingArtifacts = ref(new Set<string>())
const downloadError = ref<string | null>(null)
const showTestResultArtifacts = ref(false)

const visibleArtifacts = computed(() => {
  if (showTestResultArtifacts.value) return store.currentRunArtifacts
  return store.currentRunArtifacts.filter(a => !a.isTestResultArtifact)
})

const hiddenTestResultArtifactCount = computed(() =>
  store.currentRunArtifacts.filter(a => a.isTestResultArtifact).length
)

async function downloadArtifact(artifactId: string, name: string) {
  if (downloadingArtifacts.value.has(artifactId)) return
  downloadingArtifacts.value.add(artifactId)
  downloadError.value = null
  try {
    const apiBase = config.public.apiBase as string
    const url = `${apiBase}/api/cicd-runs/${runId}/artifacts/${artifactId}/download`
    const response = await fetch(url, { credentials: 'include' })
    if (response.status === 404) throw new Error('Artifact not found in storage.')
    if (!response.ok) throw new Error('Failed to download artifact. Please try again.')
    const blob = await response.blob()
    const objectUrl = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = objectUrl
    a.download = `${name}.zip`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(objectUrl)
  }
  catch (err) {
    downloadError.value = err instanceof Error ? err.message : 'Failed to download artifact. Please try again.'
  }
  finally {
    downloadingArtifacts.value.delete(artifactId)
  }
}

function formatLogTime(d: string) {
  const dt = new Date(d)
  return `${String(dt.getHours()).padStart(2, '0')}:${String(dt.getMinutes()).padStart(2, '0')}:${String(dt.getSeconds()).padStart(2, '0')}`
}

function formatTestDuration(ms: number) {
  if (ms < 1) return '<1ms'
  if (ms < 1000) return `${Math.round(ms)}ms`
  const s = ms / 1000
  if (s < 60) return `${s.toFixed(1)}s`
  return `${Math.floor(s / 60)}m ${Math.round(s % 60)}s`
}

/** Returns the internal code viewer URL for a specific commit SHA, if one is provided. */
function commitUrl(sha?: string): string | null {
  if (!sha) return null
  return `/projects/${projectId}/code?sha=${sha}`
}

/** Returns the internal code viewer URL for a specific branch, opening the Branches tab. */
function branchUrl(branch?: string): string | null {
  return buildBranchUrl(projectId, branch)
}

function duration(start: string, end?: string) {
  const ms = (end ? new Date(end).getTime() : now.value) - new Date(start).getTime()
  if (ms < 0) return '—'
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ${s % 60}s`
  return `${Math.floor(m / 60)}h ${m % 60}m`
}

function statusClass(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    case CiCdRunStatus.WaitingForApproval: return 'bg-purple-900/30 text-purple-400'
    case CiCdRunStatus.SucceededWithWarnings: return 'bg-yellow-900/30 text-yellow-400'
    default: return 'bg-gray-800 text-gray-400'
  }
}

function statusDot(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    case CiCdRunStatus.WaitingForApproval: return 'bg-purple-400'
    case CiCdRunStatus.SucceededWithWarnings: return 'bg-yellow-400'
    default: return 'bg-gray-500'
  }
}

function linkedRunTypeBadgeClass(type: LinkedRunType) {
  switch (type) {
    case 'retry': return 'bg-blue-900/30 text-blue-400'
    case 'retry-of': return 'bg-gray-800 text-gray-400'
    case 'agent-triggered': return 'bg-purple-900/30 text-purple-400'
    case 'same-sha': return 'bg-teal-900/30 text-teal-400'
    default: return 'bg-gray-800 text-gray-400'
  }
}

function linkedRunStatusClass(link: LinkedCiCdRun) {
  const s = link.status as string
  if (s === 'succeeded' || s === CiCdRunStatus.Succeeded) return 'bg-green-900/30 text-green-400'
  if (s === 'running' || s === CiCdRunStatus.Running) return 'bg-blue-900/30 text-blue-400'
  if (s === 'failed' || s === CiCdRunStatus.Failed) return 'bg-red-900/30 text-red-400'
  if (s === 'cancelled' || s === CiCdRunStatus.Cancelled) return 'bg-gray-800 text-gray-400'
  if (s === CiCdRunStatus.WaitingForApproval) return 'bg-purple-900/30 text-purple-400'
  if (s === CiCdRunStatus.SucceededWithWarnings) return 'bg-yellow-900/30 text-yellow-400'
  return 'bg-gray-800 text-gray-400'
}

function linkedRunStatusDot(link: LinkedCiCdRun) {
  const s = link.status as string
  if (s === 'succeeded' || s === CiCdRunStatus.Succeeded) return 'bg-green-400'
  if (s === 'running' || s === CiCdRunStatus.Running) return 'bg-blue-400 animate-pulse'
  if (s === 'failed' || s === CiCdRunStatus.Failed) return 'bg-red-400'
  if (s === 'cancelled' || s === CiCdRunStatus.Cancelled) return 'bg-gray-500'
  if (s === CiCdRunStatus.SucceededWithWarnings) return 'bg-yellow-400'
  return 'bg-gray-500'
}

function navigateToLinkedRun(link: LinkedCiCdRun) {
  if (link.linkType === 'agent-triggered') {
    navigateTo(`/projects/${link.projectId}/runs/agent-sessions/${link.id}`)
  } else {
    navigateTo(`/projects/${link.projectId}/runs/cicd/${link.id}`)
  }
}
</script>
