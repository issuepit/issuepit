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
          <div v-else-if="activeSection === 'jobs' && hasMultipleWorkflowFiles" class="flex items-center gap-2">
            <button
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
          </div>
        </div>

        <!-- Jobs tab -->
        <template v-if="activeSection === 'jobs'">
          <!-- Graph not available — show as yellow warning box -->
          <div v-if="store.currentRunGraphError && !enrichedJobs.length" class="m-4 rounded-lg bg-yellow-900/40 border border-yellow-700/50 p-4 flex items-start gap-3">
            <svg class="w-5 h-5 text-yellow-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            </svg>
            <div>
              <p class="text-sm font-medium text-yellow-300">Workflow graph unavailable</p>
              <p class="text-xs text-yellow-400/80 mt-0.5">{{ store.currentRunGraphError }}</p>
            </div>
          </div>
          <div v-else-if="enrichedJobs.length" class="p-4">
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
                  <marker id="arrow" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
                    <path d="M0,0 L0,6 L8,3 z" fill="#4b5563" />
                  </marker>
                  <marker id="arrow-hi" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
                    <path d="M0,0 L0,6 L8,3 z" fill="#6366f1" />
                  </marker>
                </defs>
                <path
                  v-for="(edge, i) in graphLayout.edges"
                  :key="i"
                  :d="edge.path"
                  fill="none"
                  :stroke="edge.highlighted ? '#6366f1' : '#4b5563'"
                  :stroke-width="edge.highlighted ? 2 : 1.5"
                  :marker-end="edge.highlighted ? 'url(#arrow-hi)' : 'url(#arrow)'" />
              </svg>

              <!-- Job boxes layer -->
              <div
                :style="{ position: 'relative', width: graphLayout.svgWidth + 'px', minHeight: graphLayout.svgHeight + 'px', zIndex: 1 }"
                style="z-index: 1">
                <div
                  v-for="job in visibleJobs"
                  :key="job.id"
                  :style="{ position: 'absolute', left: job.x + 'px', top: job.y + 'px', width: '220px' }"
                  :class="[
                    'flex flex-col items-start gap-1 px-4 py-3 rounded-xl border transition-all text-left cursor-pointer',
                    selectedJob === job.id
                      ? 'border-brand-500 bg-brand-950/30 ring-1 ring-brand-500/40'
                      : connectedJobIds.has(job.id)
                        ? 'border-brand-700/60 bg-brand-950/10 ring-1 ring-brand-700/30'
                        : selectedJob
                          ? 'border-gray-800 bg-gray-800/40 opacity-50'
                          : 'border-gray-700 bg-gray-800/80 hover:border-gray-600',
                  ]"
                  @click="toggleJobFilter(job.id)">
                  <span class="flex items-center gap-1.5 w-full">
                    <span :class="jobStatusDot(job)" class="w-2 h-2 rounded-full shrink-0" />
                    <span class="text-sm font-medium text-white break-words leading-tight">{{ job.name }}</span>
                  </span>
                  <span v-if="job.callerWorkflowFile || job.workflowFile" class="text-xs text-gray-500 font-mono">
                    <template v-if="job.callerWorkflowFile">{{ job.callerWorkflowFile }} / {{ job.workflowFile }}</template>
                    <template v-else>{{ job.workflowFile }}</template>
                  </span>
                  <span :class="jobStatusClass(job)" class="text-xs px-1.5 py-0.5 rounded-full font-medium">
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
                  <span class="text-xs text-gray-600">{{ job.logCount }} log line{{ job.logCount === 1 ? '' : 's' }}</span>
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
                      {{ inst.rawId.replace(new RegExp(`^${job.id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}-?`, 'i'), '') || inst.rawId }}
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
              <div class="flex items-center gap-2 mb-2">
                <span class="text-xs text-gray-400">
                  Showing logs for job: <span class="text-white font-mono">{{ visibleJobs.find(j => j.id === selectedJob)?.name ?? selectedJob }}</span>
                  <template v-if="selectedMatrixRawId"> · instance <span class="text-brand-300 font-mono">{{ selectedMatrixRawId }}</span></template>
                </span>
                <button class="text-xs text-gray-500 hover:text-gray-300 transition-colors" @click="selectedJob = null; selectedMatrixRawId = null">Clear filter</button>
              </div>
              <div class="bg-gray-950 rounded-lg p-4 font-mono text-xs overflow-auto max-h-[500px]">
                <template v-if="jobLogsByStep.length">
                  <template v-for="(group, gi) in jobLogsByStep" :key="gi">
                    <!-- Step header: collapsible, shows duration -->
                    <div
                      v-if="group.stepId"
                      class="flex items-center gap-2 mt-3 mb-1 first:mt-0 select-none cursor-pointer group"
                      @click="toggleStep(group.stepId)">
                      <span class="text-gray-600 transition-transform" :class="collapsedSteps.has(group.stepId) ? '' : 'rotate-90'">▶</span>
                      <span class="text-xs font-semibold text-gray-400 tracking-wide uppercase group-hover:text-gray-200 transition-colors">{{ group.stepId }}</span>
                      <span v-if="stepDuration(group)" class="text-[10px] text-gray-600 font-mono">{{ stepDuration(group) }}</span>
                      <span class="flex-1 border-t border-gray-800" />
                    </div>
                    <template v-if="!group.stepId || !collapsedSteps.has(group.stepId)">
                      <div v-for="log in group.logs" :key="log.id" class="flex gap-3 leading-5">
                        <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
                        <!-- eslint-disable-next-line vue/no-v-html -->
                        <span :class="log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300'" class="whitespace-pre-wrap break-all" v-html="renderLogLine(log.line)" />
                      </div>
                    </template>
                  </template>
                </template>
                <div v-else class="text-gray-500 text-center py-4">No logs for this job</div>
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
 */
function renderLogLine(line: string): string {
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
  { label: 'Details', value: 'details' },
]
const activeSection = ref<'jobs' | 'logs' | 'tests' | 'details'>('jobs')

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
const graphJobIndexes = computed(() => {
  const graphJobs = store.currentRunGraph?.jobs ?? []
  const byId = new Map<string, string>()   // lowercase_id → graph_id
  const byName = new Map<string, string>() // lowercase_name → graph_id

  for (const j of graphJobs) {
    byId.set(j.id.toLowerCase(), j.id)
    byName.set(j.name.trim().toLowerCase(), j.id)
    // Also index just the last segment of compound names ("Caller / Callee" → "callee")
    const nameParts = j.name.split(/\s*\/\s*/)
    if (nameParts.length > 1) {
      const lastPart = nameParts[nameParts.length - 1].trim().toLowerCase()
      if (!byName.has(lastPart)) byName.set(lastPart, j.id)
    }
  }
  return { byId, byName }
})

/**
 * Maps an act log job ID to the matching graph node ID.
 *
 * act uses display names (e.g. "Build & Push") rather than YAML keys ("build").
 * For matrix jobs it appends "-N" (e.g. "Build & Push-2").
 * Reusable workflow calls prefix with the workflow/caller name ("Docker Build & Push/Build & Push").
 *
 * Matching order:
 *  1. Exact ID match (case-insensitive, handles file-prefixed IDs like "docker/build").
 *  2. Strip trailing matrix index "-N", retry exact ID.
 *  3. Display-name match (full or stripped).
 *  4. Last path segment of a compound "/" path, stripped of matrix index.
 *  5. No match → return original log ID (shows as standalone box).
 */
function resolveLogJobId(logId: string): string {
  const { byId, byName } = graphJobIndexes.value
  // Normalise backslashes (Windows paths emitted by act on Windows hosts) so matching works.
  const norm = logId.trim().toLowerCase().replace(/\\/g, '/')

  // 1. Exact ID match
  if (byId.has(norm)) return byId.get(norm)!

  // 2. Strip trailing matrix index "-N" and retry
  const stripped = norm.replace(/-\d+$/, '').trim()
  if (stripped !== norm && byId.has(stripped)) return byId.get(stripped)!

  // 3. Display-name match (full then stripped)
  if (byName.has(norm)) return byName.get(norm)!
  if (stripped !== norm && byName.has(stripped)) return byName.get(stripped)!

  // 4. Compound path — match last segment (e.g. "Docker Build & Push/Build & Push-2")
  const slashIdx = norm.lastIndexOf('/')
  if (slashIdx !== -1) {
    const lastSeg = norm.slice(slashIdx + 1).trim().replace(/-\d+$/, '').trim()
    if (byName.has(lastSeg)) return byName.get(lastSeg)!
    if (byId.has(lastSeg)) return byId.get(lastSeg)!
  }

  return logId // No match — use as-is
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
interface StepGroup { stepId: string | null; logs: CiCdRunLog[]; startTs?: string; endTs?: string }

const jobLogsByStep = computed<StepGroup[]>(() => {
  const groups: StepGroup[] = []
  let current: StepGroup | null = null
  for (const log of jobFilteredLogs.value) {
    if (current === null || log.stepId !== current.stepId) {
      current = { stepId: log.stepId ?? null, logs: [], startTs: log.timestamp }
      groups.push(current)
    }
    current.logs.push(log)
    current.endTs = log.timestamp
  }
  return groups
})

/** Set of collapsed step IDs (default: all named steps collapsed). */
const collapsedSteps = ref(new Set<string>())
/** Tracks which step IDs have already been auto-collapsed so we don't re-collapse them after manual expand. */
const seenStepIds = ref(new Set<string>())

// Auto-collapse any new named step that appears (default collapsed on first render).
watch(jobLogsByStep, (groups) => {
  for (const g of groups) {
    if (g.stepId && !seenStepIds.value.has(g.stepId)) {
      seenStepIds.value = new Set([...seenStepIds.value, g.stepId])
      collapsedSteps.value = new Set([...collapsedSteps.value, g.stepId])
    }
  }
}, { immediate: true })

function toggleStep(stepId: string) {
  const s = new Set(collapsedSteps.value)
  if (s.has(stepId)) s.delete(stepId)
  else s.add(stepId)
  collapsedSteps.value = s
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

// Build the enriched job list by unioning graph nodes with log-observed jobs.
// Graph nodes get their needs/name from the YAML; log-only jobs fall back to id as name.
const enrichedJobs = computed<EnrichedJob[]>(() => {
  const BOX_W = 220
  // Generous box height to prevent overlaps: boxes can show name, file, status, timing, log count,
  // and (for matrix jobs) a row of instance dots. 180 px covers all combinations without DOM measurement.
  const BOX_H = 180
  const COL_GAP = 80
  const ROW_GAP = 20
  const PAD = 16

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

  // Assign x/y positions
  const posMap = new Map<string, { x: number; y: number }>()
  const sortedCols = Array.from(byCol.keys()).sort((a, b) => a - b)
  let x = PAD
  for (const col of sortedCols) {
    const jobs = byCol.get(col)!
    let y = PAD
    for (const id of jobs) {
      posMap.set(id, { x, y })
      y += BOX_H + ROW_GAP
    }
    x += BOX_W + COL_GAP
  }

  return Array.from(allIds).map(id => {
    const meta = jobMeta.get(id)
    const logs = jobLogMap.value.get(id) ?? { logCount: 0, hasError: false, isComplete: false, instances: new Map() }
    const pos = posMap.get(id) ?? { x: PAD, y: PAD }
    const hasStarted = logs.logCount > 0
    // Mark job as complete if it logged "Job succeeded/failed" OR if the overall run has ended.
    const isComplete = logs.isComplete || (hasStarted && runIsTerminal.value)
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
      hasError: logs.hasError,
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
 * Set of job IDs connected to the currently selected job (all ancestors + descendants via edges).
 * Used for visual highlighting of related nodes.
 */
const connectedJobIds = computed<Set<string>>(() => {
  if (!selectedJob.value) return new Set()
  const edges = store.currentRunGraph?.edges ?? []
  const connected = new Set<string>()

  // BFS forward (downstream) and backward (upstream)
  const queue = [selectedJob.value]
  const visited = new Set([selectedJob.value])
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

// ── SVG graph layout ───────────────────────────────────────────────────────────

interface SvgEdge { path: string; highlighted: boolean }

const graphLayout = computed<{ svgWidth: number; svgHeight: number; edges: SvgEdge[] }>(() => {
  const BOX_W = 220
  const BOX_H = 180
  const PAD = 16

  if (!visibleJobs.value.length) return { svgWidth: 0, svgHeight: 0, edges: [] }

  const visibleIds = new Set(visibleJobs.value.map(j => j.id))
  const posMap = new Map(visibleJobs.value.map(j => [j.id, { x: j.x, y: j.y }]))
  const highlightedIds = connectedJobIds.value
  const sel = selectedJob.value

  const edges = (store.currentRunGraph?.edges ?? [])
    .filter(e => visibleIds.has(e.from) && visibleIds.has(e.to))
    .map(e => {
    const from = posMap.get(e.from)
    const to = posMap.get(e.to)
    if (!from || !to) return null

    const x1 = from.x + BOX_W
    const y1 = from.y + BOX_H / 2
    const x2 = to.x
    const y2 = to.y + BOX_H / 2
    const cx = (x1 + x2) / 2
    const highlighted = sel !== null && (e.from === sel || e.to === sel || highlightedIds.has(e.from) || highlightedIds.has(e.to))

    return { path: `M ${x1} ${y1} C ${cx} ${y1}, ${cx} ${y2}, ${x2} ${y2}`, highlighted }
  }).filter((e): e is SvgEdge => e !== null)

  const maxX = Math.max(...visibleJobs.value.map(j => j.x)) + BOX_W + PAD
  const maxY = Math.max(...visibleJobs.value.map(j => j.y)) + BOX_H + PAD

  return { svgWidth: maxX, svgHeight: maxY, edges }
})

function toggleJobFilter(jobId: string) {
  if (selectedJob.value === jobId) {
    selectedJob.value = null
    selectedMatrixRawId.value = null
  } else {
    selectedJob.value = jobId
    selectedMatrixRawId.value = null
    // Reset collapsed steps so each job starts with all steps collapsed by default.
    collapsedSteps.value = new Set()
    seenStepIds.value = new Set()
  }
}

function selectMatrixInstance(jobId: string, rawId: string) {
  selectedJob.value = jobId
  selectedMatrixRawId.value = selectedMatrixRawId.value === rawId ? null : rawId
  collapsedSteps.value = new Set()
  seenStepIds.value = new Set()
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
  await store.fetchRun(runId)
  await store.fetchTestResults(runId)

  // Connect to the CiCd output hub to receive live log lines and run-completed events
  await connectCicd()
  if (cicdConnection.value) {
    await cicdConnection.value.invoke('JoinRun', runId).catch((e: unknown) => { console.warn('Failed to join run group', e) })
    cicdConnection.value.on('LogLine', (event: { runId: string; payload: string }) => {
      try {
        const data = JSON.parse(event.payload) as { event?: string; stream?: string; line?: string; jobId?: string; stepId?: string; timestamp?: string }
        if (data.event === 'run-completed') {
          now.value = Date.now()
          // Refresh only run metadata (status, endedAt) — do NOT re-fetch logs to avoid losing scroll position
          store.fetchRunOnly(runId)
          // Fetch test results now that the run has completed
          store.fetchTestResults(runId)
        } else if (data.event === 'run-heartbeat') {
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
