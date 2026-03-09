<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-6">
      <NuxtLink :to="`/projects/${projectId}/runs`" class="text-gray-500 hover:text-gray-300 transition-colors">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </NuxtLink>
      <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
      </svg>
      <h1 class="text-xl font-bold text-white">CI/CD Run</h1>
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
            <p class="text-sm text-gray-300 font-mono">{{ store.currentRun.branch || '—' }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Commit</p>
            <p class="text-sm text-gray-300 font-mono">{{ store.currentRun.commitSha?.slice(0, 7) || '—' }}</p>
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
            <p class="text-sm text-gray-300 font-mono text-xs">{{ store.currentRun.externalRunId }}</p>
          </div>
          <div v-if="store.currentRun.workspacePath">
            <p class="text-xs text-gray-500 mb-1">Workspace</p>
            <p class="text-xs text-gray-400 font-mono truncate" :title="store.currentRun.workspacePath">{{ store.currentRun.workspacePath }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Started</p>
            <p class="text-sm text-gray-400">{{ formatDate(store.currentRun.startedAt) }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Duration</p>
            <p class="text-sm text-gray-400">{{ duration(store.currentRun.startedAt, store.currentRun.endedAt) }}</p>
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
        <div v-if="store.currentRun.status === CiCdRunStatus.Failed || store.currentRun.status === CiCdRunStatus.Cancelled"
          class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
          <button
            :disabled="retrying"
            class="flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300 disabled:opacity-50 transition-colors"
            :title="'Click to retry · Shift+click for options'"
            @click.exact="retryRun()"
            @click.shift="showRetryModal = true">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ retrying ? 'Retrying…' : 'Retry Run' }}
          </button>
        </div>
      </div>

      <!-- Retry options modal -->
      <Teleport to="body">
        <div v-if="showRetryModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showRetryModal = false">
          <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-md">
            <h3 class="text-base font-semibold text-white mb-4">Retry Options</h3>

            <!-- Conflict warning -->
            <div v-if="retryConflict" class="mb-4 rounded-lg bg-yellow-900/40 border border-yellow-700/50 p-3 text-xs text-yellow-300">
              {{ retryConflict.message }}
            </div>

            <label class="flex items-start gap-3 cursor-pointer mb-3">
              <input
                v-model="retryOptions.keepContainerOnFailure"
                type="checkbox"
                class="mt-0.5 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
              <span class="text-sm text-gray-300">
                Keep container on failure
                <span class="block text-xs text-gray-500 mt-0.5">The Docker container is not removed when the run fails, so you can inspect it (e.g. verify where <code class="text-gray-400">act</code> is installed).</span>
              </span>
            </label>
            <label class="flex items-start gap-3 cursor-pointer mb-4">
              <input
                v-model="retryOptions.forceRetry"
                type="checkbox"
                class="mt-0.5 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
              <span class="text-sm text-gray-300">
                Force retry
                <span class="block text-xs text-gray-500 mt-0.5">Retry even if another run for this project is already in progress.</span>
              </span>
            </label>

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
                    placeholder="e.g. ghcr.io/catthehacker/ubuntu:act-24.04"
                    class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
                  <p class="text-xs text-gray-600 mt-1">Outer Docker image for the container that executes the act tool itself.</p>
                </div>
                <div>
                  <label class="block text-xs text-gray-500 mb-1">Act runner image</label>
                  <input
                    v-model="retryOptions.actRunnerImage"
                    type="text"
                    placeholder="e.g. ghcr.io/catthehacker/ubuntu:act-latest"
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
                @click="showRetryModal = false; retryConflict = null; retryOptions.forceRetry = false">
                Cancel
              </button>
              <button
                :disabled="retrying"
                class="px-4 py-1.5 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-md transition-colors"
                @click="retryRunWithOptions">
                {{ retrying ? 'Retrying…' : 'Retry Run' }}
              </button>
            </div>
          </div>
        </div>
      </Teleport>

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
              title="Toggle slim mode — hides log counts, file names and status labels"
              @click="slimMode = !slimMode">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
              </svg>
              Slim
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
                      <span class="text-xs font-semibold text-gray-400 tracking-wide uppercase group-hover:text-gray-200 transition-colors">{{ group.label }}</span>
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
            <div
              v-for="suite in store.currentRunTestSuites"
              :key="suite.id"
              class="bg-gray-900 border border-gray-800 rounded-lg overflow-hidden">
              <!-- Suite header -->
              <div class="flex items-center gap-4 p-3 border-b border-gray-800 bg-gray-800/40">
                <span class="text-sm font-medium text-gray-300 truncate flex-1">{{ suite.artifactName }}</span>
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
              <!-- Test cases -->
              <div class="divide-y divide-gray-800">
                <div
                  v-for="tc in suite.testCases"
                  :key="tc.id"
                  class="px-3 py-2">
                  <div class="flex items-center gap-2">
                    <!-- outcome icon -->
                    <span v-if="tc.outcomeName === 'Passed'" class="text-green-400 shrink-0">✓</span>
                    <span v-else-if="tc.outcomeName === 'Failed'" class="text-red-400 shrink-0">✗</span>
                    <span v-else class="text-yellow-500 shrink-0">–</span>
                    <span class="text-xs text-gray-300 font-mono truncate flex-1" :title="tc.fullName">{{ tc.methodName || tc.fullName }}</span>
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
              <p class="text-xs text-gray-500 mb-3">{{ store.currentRunArtifacts.length }} artifact{{ store.currentRunArtifacts.length === 1 ? '' : 's' }} produced by this run.</p>
              <div class="space-y-2">
                <div
                  v-for="artifact in store.currentRunArtifacts"
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
                  <span class="text-xs text-gray-600 shrink-0">{{ formatDate(artifact.createdAt) }}</span>
                  <a
                    v-if="artifact.storageKey"
                    :href="`/api/cicd-runs/${runId}/artifacts/${artifact.id}/download`"
                    class="flex items-center gap-1 px-2.5 py-1.5 rounded text-xs font-medium bg-brand-600 hover:bg-brand-500 text-white transition-colors shrink-0"
                    :aria-label="`Download ${artifact.name}.zip`"
                    :title="`Download ${artifact.name}.zip`">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                    </svg>
                    Download
                  </a>
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
                  <td class="py-2 text-gray-300 break-all">{{ entry.value }}</td>
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

    <!-- Create Issue from failed job modal -->
    <Teleport to="body">
      <div v-if="showCreateIssueModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showCreateIssueModal = false">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-lg">
          <h3 class="text-base font-semibold text-white mb-1">Create Issue from {{ createIssueJobId ? 'Failed Job' : 'Run Logs' }}</h3>
          <p v-if="createIssueJobId" class="text-xs text-gray-500 mb-4">Job: <span class="font-mono text-gray-300">{{ createIssueJobId }}</span></p>
          <p v-else class="text-xs text-gray-500 mb-4">Run: <span class="font-mono text-gray-300">{{ runId.slice(0, 8) }}</span></p>

          <div class="mb-3">
            <label class="block text-xs text-gray-500 mb-1">Title</label>
            <input
              v-model="createIssueTitle"
              type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-3 py-2 focus:outline-none focus:border-brand-500" />
          </div>

          <div class="mb-3">
            <label class="block text-xs text-gray-500 mb-1">Log scope</label>
            <div class="flex gap-2">
              <button v-for="scope in logScopeOptions" :key="scope.value"
                :class="['px-3 py-1 text-xs rounded-md border transition-colors', createIssueLogScope === scope.value ? 'border-brand-500 bg-brand-950/30 text-brand-300' : 'border-gray-700 text-gray-500 hover:border-gray-600']"
                @click="createIssueLogScope = scope.value">
                {{ scope.label }}
              </button>
            </div>
          </div>

          <div class="mb-4 bg-gray-950 rounded-lg p-3 font-mono text-xs overflow-auto max-h-[200px]">
            <div v-for="(line, i) in createIssuePreviewLines" :key="i" class="text-gray-400 leading-5 whitespace-pre-wrap break-all">{{ line }}</div>
            <div v-if="!createIssuePreviewLines.length" class="text-gray-600">No log lines for this job</div>
          </div>

          <div v-if="createIssueError" class="mb-3 text-xs text-red-400">{{ createIssueError }}</div>

          <div class="flex justify-end gap-2">
            <button class="px-4 py-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors" @click="showCreateIssueModal = false">Cancel</button>
            <button
              :disabled="creatingIssue || !createIssueTitle.trim()"
              class="px-4 py-1.5 text-sm bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-md transition-colors"
              @click="submitCreateIssue">
              {{ creatingIssue ? 'Creating…' : 'Create Issue' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Trigger filter modal -->
    <Teleport to="body">
      <div v-if="showTriggerFilterModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showTriggerFilterModal = false">
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
import { CiCdRunStatus, type CiCdRunLog } from '~/types'
import { parseAnsiToHtml, stripAnsiCodes } from '~/composables/useAnsiParser'
import { buildGraphJobIndexes, resolveLogJobId as resolveLogJobIdFn, matrixLabel as matrixLabelFn } from '~/utils/cicdLogMapper'

const route = useRoute()
const projectId = route.params.id as string
const runId = route.params.runId as string

const store = useCiCdRunsStore()
const issuesStore = useIssuesStore()
const { prefs } = useUserPreferences()

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
const showRetryModal = ref(false)
const retryOptions = reactive({
  keepContainerOnFailure: false,
  forceRetry: false,
  noDind: false,
  noVolumeMounts: false,
  customImage: '',
  customEntrypoint: '',
  customArgs: '',
  actRunnerImage: '',
})
const retryConflict = ref<{ message: string; activeRunId: string } | null>(null)

const sectionTabs = [
  { label: 'Jobs', value: 'jobs' },
  { label: 'Logs', value: 'logs' },
  { label: 'Tests', value: 'tests' },
  { label: 'Artifacts', value: 'artifacts' },
  { label: 'Details', value: 'details' },
]
const activeSection = ref<'jobs' | 'logs' | 'tests' | 'artifacts' | 'details'>('jobs')

/** Slim mode: hides log counts, yml file names and status labels in the job graph. */
const slimMode = ref(false)

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

// Clear measured heights when slim mode changes so stale measurements are not used.
watch(slimMode, () => {
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
    if (log.line === 'Job succeeded' || log.line === 'Job failed') entry.isComplete = true
    // Per-instance tracking
    if (!entry.instances.has(log.jobId)) entry.instances.set(log.jobId, makeEntry())
    const inst = entry.instances.get(log.jobId)!
    inst.logCount++
    if (log.stream === 'stderr') inst.hasError = true
    if (!inst.startedAt) inst.startedAt = log.timestamp
    inst.endedAt = log.timestamp
    if (log.line === 'Job succeeded' || log.line === 'Job failed') inst.isComplete = true
  }
  return map
})

// When the overall run is done, all tracked jobs are also complete (they can't still be running).
const runIsTerminal = computed(() => {
  const s = store.currentRun?.status
  return s === CiCdRunStatus.Succeeded || s === CiCdRunStatus.Failed || s === CiCdRunStatus.Cancelled
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
const enrichedJobs = computed<EnrichedJob[]>(() => {
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
  const PAD = 16

  // Helper: compute box height for a job.
  // Uses the actual measured height from ResizeObserver when available,
  // falling back to an estimate based on the current mode and matrix instance count.
  const computeBoxH = (id: string) => {
    const measured = measuredBoxHeights.value.get(id)
    if (measured) return measured
    const instances = jobLogMap.value.get(id)?.instances
    const instanceCount = instances ? instances.size : 0
    const matrixRows = instanceCount > 1 ? Math.ceil(instanceCount / 2) : 0
    const baseH = slimMode.value ? SLIM_BOX_H : BASE_BOX_H
    return baseH + matrixRows * MATRIX_ROW_H
  }

  // Collect all job IDs
  const graphJobs = store.currentRunGraph?.jobs ?? []
  const logJobIds = Array.from(jobLogMap.value.keys())
  const allIds = new Set([...graphJobs.map(j => j.id), ...logJobIds])

  // Build a map of job metadata
  const jobMeta = new Map(graphJobs.map(j => [j.id, { name: j.name, needs: j.needs, workflowFile: j.workflowFile, callerWorkflowFile: j.callerWorkflowFile }]))

  // Assign columns via BFS from roots (no needs)
  const colMap = new Map<string, number>()
  const inDegree = new Map<string, number>()
  const edges = store.currentRunGraph?.edges ?? []

  for (const id of allIds) {
    if (!inDegree.has(id)) inDegree.set(id, 0)
  }
  for (const e of edges) {
    if (allIds.has(e.to)) inDegree.set(e.to, (inDegree.get(e.to) ?? 0) + 1)
  }

  const queue: string[] = []
  for (const [id, deg] of inDegree) {
    if (deg === 0) queue.push(id)
  }

  while (queue.length) {
    const id = queue.shift()!
    const col = colMap.get(id) ?? 0
    for (const e of edges) {
      if (e.from === id) {
        const nextCol = Math.max(colMap.get(e.to) ?? 0, col + 1)
        colMap.set(e.to, nextCol)
        const newDeg = (inDegree.get(e.to) ?? 1) - 1
        inDegree.set(e.to, newDeg)
        if (newDeg === 0) queue.push(e.to)
      }
    }
  }

  // Group by column
  const byCol = new Map<number, string[]>()
  for (const id of allIds) {
    const col = colMap.get(id) ?? 0
    if (!byCol.has(col)) byCol.set(col, [])
    byCol.get(col)!.push(id)
  }

  // Assign x/y positions using per-job heights for accurate spacing
  const posMap = new Map<string, { x: number; y: number }>()
  const sortedCols = Array.from(byCol.keys()).sort((a, b) => a - b)
  let x = PAD
  for (const col of sortedCols) {
    const jobs = byCol.get(col)!
    let y = PAD
    for (const id of jobs) {
      posMap.set(id, { x, y })
      y += computeBoxH(id) + ROW_GAP
    }
    x += BOX_W + COL_GAP
  }

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

  return Array.from(allIds).map(id => {
    const meta = jobMeta.get(id)
    const logs = jobLogMap.value.get(id) ?? { logCount: 0, hasError: false, isComplete: false, instances: new Map() }
    const pos = posMap.get(id) ?? { x: PAD, y: PAD }
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

    return {
      id,
      name: meta?.name ?? id,
      needs: meta?.needs ?? [],
      logCount: logs.logCount,
      hasError,
      hasStarted,
      isComplete,
      workflowFile: meta?.workflowFile,
      callerWorkflowFile: meta?.callerWorkflowFile,
      matrixCount,
      matrixInstances,
      startedAt: logs.startedAt,
      // When run ended but the job never emitted a final timestamp, use the run's end time.
      endedAt: isComplete && !logs.endedAt ? (store.currentRun?.endedAt ?? logs.startedAt) : logs.endedAt,
      x: pos.x,
      y: pos.y,
      boxHeight: computeBoxH(id),
    }
  })
})

/** Jobs visible after applying the workflow trigger filter. */
const visibleJobs = computed<EnrichedJob[]>(() => {
  if (selectedTriggerFilters.value.size === 0 || triggerVisibleFiles.value.size === 0)
    return enrichedJobs.value
  return enrichedJobs.value.filter(j => !j.workflowFile || triggerVisibleFiles.value.has(j.workflowFile))
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
  const BOX_W = 220
  const PAD = 16

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

  const maxX = Math.max(...visibleJobs.value.map(j => j.x)) + BOX_W + PAD
  const maxY = Math.max(...visibleJobs.value.map(j => j.y + j.boxHeight)) + PAD

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
const createIssueLogScope = ref<'full' | 'tail' | 'errors'>('errors')
const creatingIssue = ref(false)
const createIssueError = ref<string | null>(null)

const logScopeOptions = [
  { label: 'Errors only', value: 'errors' as const },
  { label: 'Last 50 lines', value: 'tail' as const },
  { label: 'Full log', value: 'full' as const },
]

const createIssuePreviewLines = computed(() => {
  // When jobId is empty, use all run logs (launched from the Logs tab).
  const sourceLogs = createIssueJobId.value
    ? store.currentRunLogs.filter(l => l.jobId === createIssueJobId.value)
    : store.currentRunLogs
  if (createIssueLogScope.value === 'errors') return sourceLogs.filter(l => l.stream === 'stderr').map(l => l.line)
  if (createIssueLogScope.value === 'tail') return sourceLogs.slice(-50).map(l => l.line)
  return sourceLogs.map(l => l.line)
})

function openCreateIssueModal(jobId: string) {
  createIssueJobId.value = jobId
  const workflow = store.currentRun?.workflow ? ` (${store.currentRun.workflow})` : ''
  createIssueTitle.value = jobId
    ? `CI/CD job "${jobId}" failed${workflow}`
    : `CI/CD run failed${workflow}`
  createIssueLogScope.value = 'errors'
  createIssueError.value = null
  showCreateIssueModal.value = true
}

async function submitCreateIssue() {
  if (!createIssueTitle.value.trim()) return
  creatingIssue.value = true
  createIssueError.value = null
  try {
    const logLines = createIssuePreviewLines.value
    const logText = logLines.length
      ? `\n\n**Failed job logs** (scope: ${createIssueLogScope.value}):\n\`\`\`\n${logLines.join('\n')}\n\`\`\``
      : ''
    const context = createIssueJobId.value
      ? `CI/CD run **${runId.slice(0, 8)}** failed at job **${createIssueJobId.value}**.`
      : `CI/CD run **${runId.slice(0, 8)}** failed.`
    const description = `${context}${logText}`

    await issuesStore.createIssue(projectId, {
      title: createIssueTitle.value.trim(),
      description,
    })
    showCreateIssueModal.value = false
  } catch (e: unknown) {
    createIssueError.value = e instanceof Error ? e.message : 'Failed to create issue'
  } finally {
    creatingIssue.value = false
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

async function retryRunWithOptions() {
  retrying.value = true
  retryConflict.value = null
  showRetryModal.value = false
  try {
    await store.retryRun(runId, {
      keepContainerOnFailure: retryOptions.keepContainerOnFailure,
      forceRetry: retryOptions.forceRetry,
      noDind: retryOptions.noDind,
      noVolumeMounts: retryOptions.noVolumeMounts,
      customImage: retryOptions.customImage.trim() || undefined,
      customEntrypoint: retryOptions.customEntrypoint.trim() || undefined,
      customArgs: retryOptions.customArgs.trim() || undefined,
      actRunnerImage: retryOptions.actRunnerImage.trim() || undefined,
    })
    retryOptions.forceRetry = false
    navigateTo(`/projects/${projectId}/runs`)
  } catch (e: unknown) {
    // Handle 409 "already running" conflict — surface it in the options modal
    interface RetryConflictResponse { error?: string; canForce?: boolean; activeRunId?: string }
    const data = (e as { data?: RetryConflictResponse })?.data
    if (data?.canForce) {
      retryConflict.value = {
        message: data.error ?? 'Another run is already in progress for this project.',
        activeRunId: data.activeRunId ?? '',
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

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
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
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function statusDot(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}
</script>
