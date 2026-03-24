<template>
  <div class="p-8">
    <!-- Breadcrumb + Header -->
    <div class="flex items-center gap-2 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${projectId}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Runs', to: `/projects/${projectId}/runs?tab=agent`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
        { label: 'Agent Session', to: `/projects/${projectId}/runs/agent-sessions/${sessionId}`, icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
      ]" />
      <!-- Live indicator when session is active -->
      <span v-if="isActive && isConnected" class="flex items-center gap-1 text-xs text-green-400 font-normal ml-1">
        <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
        Live
      </span>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="store.currentSession">
      <!-- Session Info -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div>
            <p class="text-xs text-gray-500 mb-1">Status</p>
            <span :class="statusClass(store.currentSession.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
              <span :class="statusDot(store.currentSession.status)" class="w-1.5 h-1.5 rounded-full" />
              {{ store.currentSession.statusName }}
            </span>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Agent</p>
            <NuxtLink :to="`/agents/${store.currentSession.agentId}`"
              class="text-sm text-brand-400 hover:text-brand-300 transition-colors">
              {{ store.currentSession.agentName }}
            </NuxtLink>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Issue</p>
            <NuxtLink :to="`/projects/${projectId}/issues/${store.currentSession.issueNumber}`"
              class="text-sm text-brand-400 hover:text-brand-300 transition-colors">
              #{{ formatIssueId(store.currentSession.issueNumber, projectsStore.currentProject) }} {{ store.currentSession.issueTitle }}
            </NuxtLink>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Branch / Commit</p>
            <p class="text-sm text-gray-300 font-mono">{{ store.currentSession.gitBranch || '—' }}</p>
            <p v-if="store.currentSession.commitSha" class="text-xs text-gray-500 font-mono mt-0.5">{{ store.currentSession.commitSha.slice(0, 7) }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Started</p>
            <p class="text-sm text-gray-400"><DateDisplay :date="store.currentSession.startedAt" mode="auto" /></p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Duration</p>
            <p class="text-sm text-gray-400">{{ store.currentSession.status === AgentSessionStatus.Pending ? '—' : duration(store.currentSession.startedAt, store.currentSession.endedAt) }}</p>
          </div>
          <!-- opencode Session ID — shown when available -->
          <div v-if="store.currentSession.openCodeSessionId" class="col-span-2 md:col-span-4">
            <p class="text-xs text-gray-500 mb-1">opencode Session</p>
            <div class="flex items-center gap-2 flex-wrap">
              <span class="text-xs text-gray-300 font-mono bg-gray-800 px-2 py-0.5 rounded">{{ store.currentSession.openCodeSessionId }}</span>
              <a v-if="store.currentSession.serverWebUiUrl"
                :href="store.currentSession.serverWebUiUrl"
                target="_blank"
                rel="noopener noreferrer"
                class="flex items-center gap-1 text-xs text-brand-400 hover:text-brand-300 transition-colors">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                </svg>
                Open in opencode UI
              </a>
              <span v-if="store.currentSession.openCodeDbS3Url" class="flex items-center gap-1 text-xs text-green-400">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M5 13l4 4L19 7" />
                </svg>
                Session preserved
              </span>
            </div>
          </div>
        </div>
        <!-- Cancel button for active sessions -->
        <div v-if="store.currentSession.status === AgentSessionStatus.Running || store.currentSession.status === AgentSessionStatus.Pending"
          class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
          <button
            :disabled="cancelling"
            class="flex items-center gap-1.5 text-sm text-red-400 hover:text-red-300 disabled:opacity-50 transition-colors"
            @click="cancelSession">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M6 18L18 6M6 6l12 12" />
            </svg>
            {{ cancelling ? 'Cancelling…' : 'Cancel Session' }}
          </button>
        </div>
        <!-- Retry button for failed/cancelled sessions -->
        <div v-if="store.currentSession.status === AgentSessionStatus.Failed || store.currentSession.status === AgentSessionStatus.Cancelled"
          class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
          <button
            :disabled="retrying"
            class="flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300 disabled:opacity-50 transition-colors"
            @click="openRetryModal">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ retrying ? 'Retrying…' : 'Retry Session' }}
          </button>
        </div>
      </div>

      <!-- Retry options modal -->
      <Teleport to="body">
        <div v-if="showRetryModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="showRetryModal = false">
          <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-lg max-h-[90vh] overflow-y-auto">
            <h3 class="text-base font-semibold text-white mb-4">Retry Session</h3>

            <div class="mb-4 space-y-1 text-sm text-gray-400">
              <div class="flex gap-2">
                <span class="text-gray-500 w-16 shrink-0">Issue</span>
                <span class="text-gray-300">#{{ formatIssueId(store.currentSession.issueNumber, projectsStore.currentProject) }} {{ store.currentSession.issueTitle }}</span>
              </div>
            </div>

            <!-- Agent selector -->
            <div class="mb-4">
              <label class="block text-xs text-gray-500 mb-1.5">Agent</label>
              <select v-model="retryAgentId"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-2.5 py-1.5 focus:outline-none focus:border-brand-500">
                <option v-for="agent in agentsStore.agents" :key="agent.id" :value="agent.id">
                  {{ agent.name }}{{ agent.id === store.currentSession?.agentId ? ' (current)' : '' }}
                </option>
              </select>
            </div>

            <!-- Model override -->
            <div class="mb-4">
              <label class="block text-xs text-gray-500 mb-1.5">Model override</label>
              <input
                v-model="retryModel"
                type="text"
                placeholder="Leave blank to use agent default (e.g. anthropic/claude-opus-4-5)"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
            </div>

            <!-- CLI selector -->
            <div class="mb-4">
              <label class="block text-xs text-gray-500 mb-1.5">CLI / Runner mode</label>
              <select v-model="retryCli"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-2.5 py-1.5 focus:outline-none focus:border-brand-500">
                <option v-for="opt in cliOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
              </select>
            </div>

            <!-- Runtime selector -->
            <div class="mb-4">
              <label class="block text-xs text-gray-500 mb-1.5">Runtime</label>
              <select v-model="retryRuntimeType"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-gray-300 px-2.5 py-1.5 focus:outline-none focus:border-brand-500">
                <option v-for="opt in runtimeOptions" :key="String(opt.value)" :value="opt.value">{{ opt.label }}</option>
              </select>
            </div>

            <!-- Docker image selection -->
            <div class="mb-4">
              <label class="block text-xs text-gray-500 mb-2">Docker Image</label>
              <div class="space-y-2">
                <label v-for="opt in agentImageOptions" :key="opt.value" class="flex items-start gap-2.5 cursor-pointer">
                  <input
                    v-model="retryDockerImage"
                    type="radio"
                    :value="opt.value"
                    class="mt-0.5 text-brand-500 focus:ring-brand-500 bg-gray-800 border-gray-600" />
                  <span class="text-sm">
                    <span class="text-gray-300 font-mono text-xs">{{ opt.value }}</span>
                    <span v-if="opt.isDefault" class="ml-1.5 text-xs text-gray-600">(default)</span>
                    <span class="block text-xs text-gray-500 mt-0.5">{{ opt.description }}</span>
                  </span>
                </label>
                <label class="flex items-start gap-2.5 cursor-pointer">
                  <input
                    v-model="retryDockerImage"
                    type="radio"
                    value="custom"
                    class="mt-0.5 text-brand-500 focus:ring-brand-500 bg-gray-800 border-gray-600" />
                  <span class="text-sm text-gray-300">Custom image</span>
                </label>
                <input
                  v-if="retryDockerImage === 'custom'"
                  v-model="retryCustomDockerImage"
                  type="text"
                  placeholder="e.g. ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24"
                  class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 placeholder-gray-600 focus:outline-none focus:border-brand-500" />
              </div>
            </div>

            <p class="text-xs text-gray-500 mb-4">A new session will be started for the same issue.</p>

            <!-- Keep container option -->
            <label class="flex items-start gap-2.5 cursor-pointer mb-5">
              <input
                v-model="retryKeepContainer"
                type="checkbox"
                class="mt-0.5 text-brand-500 focus:ring-brand-500 bg-gray-800 border-gray-600 rounded" />
              <span class="text-sm">
                <span class="text-gray-300">Keep container after exit</span>
                <span class="block text-xs text-gray-500 mt-0.5">Container will not be removed on exit — useful for debugging (docker exec, logs, inspect).</span>
              </span>
            </label>

            <div class="flex justify-end gap-2">
              <button
                class="px-4 py-1.5 text-sm text-gray-400 hover:text-gray-200 transition-colors"
                @click="showRetryModal = false">
                Cancel
              </button>
              <button
                :disabled="retrying"
                class="px-4 py-1.5 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-md transition-colors"
                @click="retrySession">
                {{ retrying ? 'Retrying…' : 'Retry Session' }}
              </button>
            </div>
          </div>
        </div>
      </Teleport>

      <!-- Warnings -->
      <div v-if="sessionWarnings.length" class="mb-6 space-y-2">
        <div v-for="(warning, idx) in sessionWarnings" :key="idx"
          class="flex items-start gap-3 bg-yellow-950/40 border border-yellow-800/50 rounded-lg px-4 py-3">
          <svg class="w-4 h-4 text-yellow-400 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
          <p class="text-sm text-yellow-300">{{ warning }}</p>
        </div>
      </div>

      <!-- Logs / Details -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden mb-6">
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

          <!-- Log stream filter (only in Logs tab and Steps tab selected-step view) -->
          <div v-if="activeSection === 'logs' || (activeSection === 'steps' && selectedStep)" class="flex items-center gap-2 flex-wrap">
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
            <!-- Search -->
            <div class="relative">
              <svg class="absolute left-2 top-1/2 -translate-y-1/2 w-3 h-3 text-gray-500 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
              <input
                v-model="logSearchQuery"
                type="text"
                placeholder="Search logs…"
                class="bg-gray-900 border border-gray-700 rounded-md text-xs text-gray-300 pl-6 pr-2 py-1 placeholder-gray-600 focus:outline-none focus:border-brand-500 w-40 transition-colors" />
            </div>
            <!-- Word wrap toggle -->
            <button
              :class="['px-2 py-0.5 text-xs rounded-md border transition-colors', wordWrap ? 'border-brand-700 text-brand-300 bg-brand-950/30' : 'border-gray-700 text-gray-500 hover:border-gray-600']"
              title="Toggle word wrap"
              @click="wordWrap = !wordWrap">
              Wrap
            </button>
            <!-- Verbose toggle (show/hide dnsmasq DNS-proxy noise) -->
            <button
              :class="['px-2 py-0.5 text-xs rounded-md border transition-colors', verboseLogs ? 'border-brand-700 text-brand-300 bg-brand-950/30' : 'border-gray-700 text-gray-500 hover:border-gray-600']"
              title="Show/hide DNS proxy (dnsmasq) lines"
              @click="verboseLogs = !verboseLogs">
              Verbose
            </button>
            <button
              v-if="store.currentSessionLogs.length"
              class="px-2.5 py-1 text-xs font-medium rounded-md text-gray-500 hover:text-gray-300 transition-colors"
              title="Copy full log to clipboard"
              @click="copyLogsToClipboard">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
            </button>
          </div>
        </div>

        <!-- Steps tab — chain/tree view of workflow phases (always linear today; no branching) -->
        <template v-if="activeSection === 'steps'">
          <div v-if="sessionStepGroups.length" class="p-4">
            <!-- Chain of phase cards, one per section -->
            <div class="flex flex-col gap-3">
              <div
                v-for="(group, gi) in sessionStepGroups"
                :key="group.key"
                class="flex gap-3 items-start">
                <!-- Vertical connector line between cards -->
                <div class="flex flex-col items-center" style="width: 20px;">
                  <div
                    :class="[
                      'w-4 h-4 rounded-full border-2 shrink-0 mt-1',
                      group.hasError ? 'border-red-500 bg-red-950/40' : 'border-green-500 bg-green-950/40',
                    ]" />
                  <div v-if="gi < sessionStepGroups.length - 1" class="flex-1 w-px bg-gray-700 mt-1" style="min-height: 20px;" />
                </div>
                <!-- Phase card -->
                <div
                  class="step-box flex-1 border rounded-lg p-3 cursor-pointer transition-colors mb-1"
                  :class="[
                    selectedStep === group.key
                      ? 'border-brand-500 bg-brand-950/20'
                      : group.hasError
                        ? 'border-red-800/60 bg-gray-800/60 hover:border-red-700'
                        : 'border-gray-700 bg-gray-800/60 hover:border-gray-600',
                  ]"
                  @click="selectedStep = selectedStep === group.key ? null : group.key">
                  <div class="flex items-center gap-2">
                    <span class="text-sm font-medium" :class="group.hasError ? 'text-red-300' : 'text-white'">
                      {{ group.label }}
                    </span>
                    <span v-if="stepDuration(group)" class="text-xs text-gray-500 font-mono">{{ stepDuration(group) }}</span>
                    <span class="ml-auto text-xs text-gray-600">{{ group.logs.length }} line{{ group.logs.length === 1 ? '' : 's' }}</span>
                    <!-- Link to CI/CD run page when available -->
                    <NuxtLink
                      v-if="cicdRunForStep(group)"
                      :to="`/projects/${projectId}/runs/cicd/${cicdRunForStep(group)!.id}`"
                      class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
                      title="Open CI/CD run"
                      @click.stop>
                      Open run →
                    </NuxtLink>
                  </div>
                  <span v-if="group.hasError" class="text-xs text-red-400 mt-1 block">Contains errors</span>
                </div>
              </div>
            </div>

            <!-- Logs for the selected step -->
            <div v-if="selectedStep" class="mt-4">
              <div class="flex items-center gap-2 mb-2 flex-wrap">
                <span class="text-xs text-gray-400">
                  Showing logs for: <span class="text-white font-mono">{{ sessionStepGroups.find(g => g.key === selectedStep)?.label }}</span>
                </span>
                <button class="text-xs text-gray-500 hover:text-gray-300 transition-colors" @click="selectedStep = null">Clear filter</button>
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
                <div v-if="selectedStepLogs.length">
                  <div v-for="log in selectedStepLogs" :key="log.id" class="flex gap-3 leading-5">
                    <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
                    <!-- eslint-disable-next-line vue/no-v-html -->
                    <span :class="[log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300', wordWrap ? 'whitespace-pre-wrap break-all' : 'whitespace-pre']" v-html="renderLogLine(log.line, logSearchQuery)" />
                  </div>
                </div>
                <div v-else class="text-gray-500 text-center py-4">{{ logSearchQuery ? 'No matching log lines' : 'No logs for this step' }}</div>
              </div>
            </div>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">No step data yet — steps appear as the agent runs</div>
        </template>

        <!-- Logs tab -->
        <template v-else-if="activeSection === 'logs'">
          <div v-if="filteredLogs.length" class="bg-gray-950 p-4 font-mono text-xs overflow-auto max-h-[600px]">
            <!-- When section data exists, group logs into collapsible sections -->
            <template v-if="hasSections && logsBySection.length">
              <template v-for="(group, gi) in logsBySection" :key="gi">
                <!-- Section header: collapsible -->
                <div
                  class="flex items-center gap-2 mt-3 mb-1 first:mt-0 select-none cursor-pointer group"
                  @click="toggleSection(group.key)">
                  <span class="text-gray-600 transition-transform" :class="collapsedSections.has(group.key) ? '' : 'rotate-90'">▶</span>
                  <span class="text-xs font-semibold text-gray-400 tracking-wide uppercase group-hover:text-gray-200 transition-colors">{{ group.label }}</span>
                  <span v-if="stepDuration(group)" class="text-[10px] text-gray-600 font-mono">{{ stepDuration(group) }}</span>
                  <span class="flex-1 border-t border-gray-800" />
                </div>
                <template v-if="!collapsedSections.has(group.key)">
                  <div v-for="log in group.logs.filter(l => !logSearchQuery.trim() || stripAnsiCodes(l.line).toLowerCase().includes(logSearchQuery.toLowerCase()))" :key="log.id" class="flex gap-3 leading-5">
                    <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
                    <!-- eslint-disable-next-line vue/no-v-html -->
                    <span :class="[log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300', wordWrap ? 'whitespace-pre-wrap break-all' : 'whitespace-pre']" v-html="renderLogLine(log.line, logSearchQuery)" />
                  </div>
                </template>
              </template>
            </template>
            <!-- Flat log list for sessions without section data -->
            <template v-else>
              <div v-for="log in filteredLogs" :key="log.id" class="flex gap-3 leading-5">
                <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
                <!-- eslint-disable-next-line vue/no-v-html -->
                <span :class="[log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300', wordWrap ? 'whitespace-pre-wrap break-all' : 'whitespace-pre']" v-html="renderLogLine(log.line, logSearchQuery)" />
              </div>
            </template>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">{{ logSearchQuery ? 'No matching log lines' : 'No logs available' }}</div>
        </template>

        <!-- Terminal tab (manual mode only) -->
        <template v-else-if="activeSection === 'terminal'">
          <div class="p-4">
            <AgentTerminal
              :session-id="sessionId"
              :container-id="store.currentSession.containerId"
              :active="store.currentSession.status === AgentSessionStatus.Running" />
          </div>
        </template>

        <template v-else-if="activeSection === 'details'">
          <!-- Git remote availability -->
          <div v-if="gitRemoteChecks.length" class="p-4 border-b border-gray-800">
            <h3 class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-3">Git Remotes</h3>
            <div class="space-y-2">
              <div v-for="r in gitRemoteChecks" :key="r.repoId"
                class="flex items-center gap-3 text-xs">
                <!-- Availability icon -->
                <span class="shrink-0 w-4 text-center">
                  <span v-if="r.available === true" class="text-green-400">✓</span>
                  <span v-else-if="r.available === false" class="text-red-400">✗</span>
                  <span v-else class="text-gray-500">?</span>
                </span>
                <!-- Mode badge -->
                <span class="shrink-0 text-xs px-1.5 py-0.5 rounded-full font-medium"
                  :class="remoteModeBadgeClass(r.mode)">
                  {{ r.mode }}
                </span>
                <!-- URL + branch — links to git settings -->
                <NuxtLink
                  :to="`/projects/${projectId}/settings#git-origins`"
                  class="min-w-0 flex-1 font-mono text-gray-300 hover:text-white truncate transition-colors"
                  :title="r.remoteUrl">
                  {{ r.remoteUrl }}
                </NuxtLink>
                <span class="shrink-0 text-gray-500 font-mono">{{ r.defaultBranch ?? '—' }}</span>
                <!-- Selected indicator -->
                <span v-if="r.selected" class="shrink-0 text-xs text-brand-400 font-medium">selected</span>
                <!-- Unavailable note -->
                <span v-else-if="r.available === false" class="shrink-0 text-xs text-red-400">branch missing</span>
              </div>
            </div>
          </div>

          <!-- Debug metadata (from [DEBUG] log lines) -->
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
          <div v-if="!gitRemoteChecks.length && !debugMetadata.length" class="py-10 text-center text-sm text-gray-500">No details available</div>
        </template>
      </div>

      <!-- Associated CI/CD Runs -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div class="px-5 py-3 border-b border-gray-800">
          <h2 class="text-sm font-medium text-white">CI/CD Runs</h2>
        </div>
        <div v-if="store.currentSession.ciCdRuns.length" class="overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900/50">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Status</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Workflow</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Branch</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Commit</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Source</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Started</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Duration</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="run in store.currentSession.ciCdRuns" :key="run.id"
                class="hover:bg-gray-800/50 transition-colors cursor-pointer"
                @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.id}`)">
                <td class="px-4 py-3">
                  <CiCdStatusChip :runs="[run]" />
                </td>
                <td class="px-4 py-3 text-gray-300 text-xs">{{ run.workflow || '—' }}</td>
                <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ run.branch || '—' }}</td>
                <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ run.commitSha?.slice(0, 7) || '—' }}</td>
                <td class="px-4 py-3">
                  <span v-if="run.externalSource" class="text-xs bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded">
                    {{ run.externalSource }}
                  </span>
                  <span v-else class="text-gray-600 text-xs">local</span>
                </td>
                <td class="px-4 py-3 text-gray-400 text-xs"><DateDisplay :date="run.startedAt" mode="auto" /></td>
                <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(run.startedAt, run.endedAt) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-else class="py-10 text-center text-sm text-gray-500">No CI/CD runs for this session</div>
      </div>
    </template>

    <div v-else-if="!store.loading" class="flex flex-col items-center justify-center py-16 text-center">
      <p class="text-gray-400 font-medium">{{ store.error || 'Session not found' }}</p>
    </div>

    <ErrorBox :error="store.error" />
  </div>
</template>

<script setup lang="ts">
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { useProjectsStore } from '~/stores/projects'
import { useAgentsStore } from '~/stores/agents'
import { CiCdRunStatus, AgentSessionStatus, RunnerType, RuntimeTypeLabels, type AgentSessionLog, type GitRemoteCheckResult } from '~/types'
import { formatIssueId } from '~/composables/useIssueFormat'
import { parseAnsiToHtml, stripAnsiCodes } from '~/composables/useAnsiParser'

const route = useRoute()
const projectId = route.params.id as string
const sessionId = route.params.sessionId as string

const store = useCiCdRunsStore()
const projectsStore = useProjectsStore()
const agentsStore = useAgentsStore()
const { prefs } = useUserPreferences()

function renderLogLine(line: string, highlight?: string): string {
  let html = prefs.value.ansiColors ? parseAnsiToHtml(line) : stripAnsiCodes(line)

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

const isManualMode = computed(() => store.currentSession?.isManualMode === true)

const sectionTabs = computed(() => [
  { label: 'Logs', value: 'logs' },
  { label: 'Steps', value: 'steps' },
  ...(isManualMode.value ? [{ label: 'Terminal', value: 'terminal' }] : []),
  { label: 'Details', value: 'details' },
])
const activeSection = ref<'steps' | 'logs' | 'details' | 'terminal'>('logs')

const streamTabs = [
  { label: 'All', value: null },
  { label: 'Stdout', value: 'stdout' },
  { label: 'Stderr', value: 'stderr' },
]
const activeStream = ref<string | null>(null)

/** When false (default), dnsmasq DNS-proxy lines are hidden to reduce noise. */
const verboseLogs = ref(false)

/** Log search query — filters displayed lines and highlights matching text. */
const logSearchQuery = ref('')

/** Word wrap toggle — off by default to match CiCd run page behaviour. */
const wordWrap = ref(false)

/** Pattern that identifies noisy DNS-proxy lines emitted by dnsmasq inside the container. */
const DNSMASQ_RE = /dnsmasq\[/

const filteredLogs = computed(() => {
  let logs = store.currentSessionLogs
  if (activeStream.value !== null)
    logs = logs.filter(l => l.stream === activeStream.value)
  if (!verboseLogs.value)
    logs = logs.filter(l => !DNSMASQ_RE.test(l.line))
  if (logSearchQuery.value.trim())
    logs = logs.filter(l => stripAnsiCodes(l.line).toLowerCase().includes(logSearchQuery.value.toLowerCase()))
  return logs
})

// ── Step / section logic ────────────────────────────────────────────────────

/** Maps a backend section string to a human-readable label. */
function sectionLabel(section: string, index: number): string {
  switch (section) {
    case 'InitialAgentRun': return 'Initial Agent Run'
    case 'PostRun': return 'Post Run'
    case 'UncommittedChangesFix': return 'Uncommitted Changes Fix'
    case 'CiCdRun': return `CI/CD Run ${index}`
    case 'CiCdFixRun': return `CI/CD Fix Run ${index}`
    default: return section
  }
}

interface SessionStepGroup {
  key: string
  section: string
  sectionIndex: number
  label: string
  logs: AgentSessionLog[]
  hasError: boolean
  startTs?: string
  endTs?: string
}

/**
 * Derives the ordered list of distinct phases from the current session logs.
 * Each phase groups logs by (section, sectionIndex). Logs without a section
 * are grouped together as an unlabelled pre-section group.
 *
 * The chain is always linear today (no branching). Future enhancements could
 * introduce forking (e.g. parallel git branches per fix attempt).
 */
const sessionStepGroups = computed<SessionStepGroup[]>(() => {
  const groups: SessionStepGroup[] = []
  const keyToGroup = new Map<string, SessionStepGroup>()

  for (const log of store.currentSessionLogs) {
    if (DNSMASQ_RE.test(log.line)) continue
    const key = log.section ? `${log.section}:${log.sectionIndex}` : ''
    if (!keyToGroup.has(key)) {
      const group: SessionStepGroup = {
        key,
        section: log.section ?? '',
        sectionIndex: log.sectionIndex ?? 0,
        label: log.section ? sectionLabel(log.section, log.sectionIndex ?? 0) : 'Session',
        logs: [],
        hasError: false,
        startTs: log.timestamp,
      }
      keyToGroup.set(key, group)
      groups.push(group)
    }
    const g = keyToGroup.get(key)!
    g.logs.push(log)
    g.endTs = log.timestamp
    // A step is considered to have errors only when a line with [ERROR] prefix is present.
    // stderr alone is not an error indicator — opencode writes verbose output to stderr during normal operation.
    if (log.line.includes('[ERROR]')) g.hasError = true
  }

  return groups
})

/** Whether any log has section info (used to decide between flat vs. grouped view). */
const hasSections = computed(() => store.currentSessionLogs.some(l => !!l.section))

/** Logs grouped by step for the collapsible view in the Logs tab. */
const logsBySection = computed<SessionStepGroup[]>(() => {
  if (!hasSections.value) return []
  // Apply stream + verbose filter but NOT the search filter (search is applied at render time).
  const logs = store.currentSessionLogs.filter((l) => {
    if (activeStream.value !== null && l.stream !== activeStream.value) return false
    if (!verboseLogs.value && DNSMASQ_RE.test(l.line)) return false
    return true
  })
  const groups: SessionStepGroup[] = []
  const keyToGroup = new Map<string, SessionStepGroup>()
  for (const log of logs) {
    const key = log.section ? `${log.section}:${log.sectionIndex}` : ''
    if (!keyToGroup.has(key)) {
      const group: SessionStepGroup = {
        key,
        section: log.section ?? '',
        sectionIndex: log.sectionIndex ?? 0,
        label: log.section ? sectionLabel(log.section, log.sectionIndex ?? 0) : 'Session',
        logs: [],
        hasError: false,
        startTs: log.timestamp,
      }
      keyToGroup.set(key, group)
      groups.push(group)
    }
    const g = keyToGroup.get(key)!
    g.logs.push(log)
    g.endTs = log.timestamp
    // A step is considered to have errors only when a line with [ERROR] prefix is present.
    // stderr alone is not an error indicator — opencode writes verbose output to stderr during normal operation.
    if (log.line.includes('[ERROR]')) g.hasError = true
  }
  return groups
})

/** Tracks which step sections are collapsed in the Logs tab (by key). */
const collapsedSections = ref(new Set<string>())

function toggleSection(key: string) {
  if (collapsedSections.value.has(key)) collapsedSections.value.delete(key)
  else collapsedSections.value.add(key)
}

/** Returns step duration string or empty string. */
function stepDuration(group: SessionStepGroup): string {
  if (!group.startTs || !group.endTs) return ''
  const ms = new Date(group.endTs).getTime() - new Date(group.startTs).getTime()
  if (ms < 1000) return `${ms}ms`
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  return `${Math.floor(s / 60)}m ${s % 60}s`
}

/** The step currently selected in the Steps tab (null = all). */
const selectedStep = ref<string | null>(null)

/** Logs for the selected step (filtered by stream + search). */
const selectedStepLogs = computed<AgentSessionLog[]>(() => {
  if (!selectedStep.value) return []
  const group = sessionStepGroups.value.find(g => g.key === selectedStep.value)
  if (!group) return []
  let logs = group.logs
  if (activeStream.value !== null) logs = logs.filter(l => l.stream === activeStream.value)
  if (!verboseLogs.value) logs = logs.filter(l => !DNSMASQ_RE.test(l.line))
  if (logSearchQuery.value.trim())
    logs = logs.filter(l => stripAnsiCodes(l.line).toLowerCase().includes(logSearchQuery.value.toLowerCase()))
  return logs
})

/** Returns the matching CI/CD run for a CiCdRun step, based on order (1st CiCdRun → 1st ciCdRun). */
function cicdRunForStep(group: SessionStepGroup) {
  if (group.section !== 'CiCdRun') return undefined
  const cicdRuns = store.currentSession?.ciCdRuns ?? []
  return cicdRuns[group.sectionIndex - 1]
}

const debugMetadata = computed(() => {
  const entries: Array<{ key: string; value: string }> = []
  for (const log of store.currentSessionLogs) {
    // Match lines like: [DEBUG] Key name   : value (space-colon-space separator)
    // Strip ANSI codes before matching so control sequences don't break the regex.
    const m = stripAnsiCodes(log.line).match(/^\[DEBUG\]\s+([^:]+?)\s*:\s(.+)$/)
    if (m) entries.push({ key: m[1].trim(), value: m[2].trim() })
  }
  return entries
})

/** Parsed git remote availability check results from the session JSON field. */
const gitRemoteChecks = computed<GitRemoteCheckResult[]>(() => {
  const raw = store.currentSession?.gitRemoteCheckResultsJson
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed as GitRemoteCheckResult[] : []
  } catch {
    return []
  }
})

function remoteModeBadgeClass(mode: GitRemoteCheckResult['mode']): string {
  if (mode === 'Working') return 'bg-brand-900/40 text-brand-400'
  if (mode === 'Release') return 'bg-purple-900/40 text-purple-400'
  return 'bg-gray-700/60 text-gray-400'
}

/** Parsed warnings array from the session's JSON warnings field. */
const sessionWarnings = computed<string[]>(() => {
  const raw = store.currentSession?.warnings
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed.filter((w): w is string => typeof w === 'string') : []
  } catch (e) {
    console.warn('Failed to parse session warnings JSON:', e)
    return []
  }
})

// `now` is updated on each server-pushed event so the duration display stays live without a timer
const now = ref(Date.now())

// SignalR: connect to project hub to receive RunsUpdated events (updates the CI/CD runs table)
const { connection, isConnected, connect } = useSignalR('/hubs/project')

// SignalR: connect to agent output hub for live log streaming
const { connection: agentConnection, connect: connectAgent } = useSignalR('/hubs/agent-output')

// Whether the session is still in an active (non-terminal) state
const isActive = computed(() =>
  store.currentSession?.statusName === 'Pending' ||
  store.currentSession?.statusName === 'Running'
)

onMounted(async () => {
  projectsStore.fetchProject(projectId)
  await store.fetchAgentSession(sessionId)

  // Auto-switch to terminal tab for active manual mode sessions.
  if (store.currentSession?.isManualMode && store.currentSession?.status === AgentSessionStatus.Running) {
    activeSection.value = 'terminal'
  }

  // Connect to project hub so the session and its CI/CD runs table refresh in real time
  await connect()
  if (connection.value) {
    await connection.value.invoke('JoinProject', projectId).catch((e: unknown) => { console.warn('Failed to join project group', e) })
    connection.value.on('RunsUpdated', async () => {
      now.value = Date.now()
      if (store.currentSession) await store.fetchAgentSessionOnly(sessionId)
    })
  }

  // Connect to agent output hub for live log lines (published by IssueWorker via Redis)
  await connectAgent()
  if (agentConnection.value) {
    await agentConnection.value.invoke('JoinSession', sessionId).catch((e: unknown) => { console.warn('Failed to join agent session group', e) })
    agentConnection.value.on('LogLine', ({ payload }: { sessionId: string; payload: string }) => {
      try {
        const data = JSON.parse(payload) as { event?: string; stream?: string; line?: string; timestamp?: string; status?: string; section?: string; sectionIndex?: number }
        if (data.event === 'session-completed') {
          now.value = Date.now()
          // Refresh session metadata (status, endedAt) without replacing logs
          store.fetchAgentSessionOnly(sessionId)
        } else if (data.event === 'session-heartbeat') {
          now.value = Date.now()
        } else if (data.line !== undefined) {
          store.currentSessionLogs.push({
            id: crypto.randomUUID(),
            line: data.line,
            stream: data.stream ?? 'stdout',
            streamName: data.stream ? (data.stream.charAt(0).toUpperCase() + data.stream.slice(1)) : 'Stdout',
            timestamp: data.timestamp ?? new Date().toISOString(),
            section: data.section,
            sectionIndex: data.sectionIndex ?? 0,
          } satisfies AgentSessionLog)
          now.value = Date.now()
        }
      }
      catch (e) { console.warn('Failed to parse agent LogLine payload', e) }
    })
  }
})

const retrying = ref(false)
const showRetryModal = ref(false)
const cancelling = ref(false)

async function cancelSession() {
  cancelling.value = true
  try {
    await store.cancelSession(sessionId)
    await store.fetchAgentSessionOnly(sessionId)
  } finally {
    cancelling.value = false
  }
}

const agentImageOptions = [
  {
    value: 'ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24',
    description: 'Latest build from the main branch — recommended.',
    isDefault: true,
  },
]

// Default to the first (stable/latest) option
const retryDockerImage = ref(agentImageOptions[0].value)
const retryCustomDockerImage = ref('')
const retryKeepContainer = ref(false)

// Retry override state — all default to "use original / agent default"
const retryAgentId = ref<string>('')
const retryModel = ref('')
// CLI mode: combines RunnerType + UseHttpServer into a single choice.
// '' = use agent defaults; 'opencode' = OpenCode CLI; 'opencode-server' = OpenCode HTTP Server;
// 'codex' = Codex CLI; 'copilot' = GitHub Copilot CLI; 'none' = no runner (entrypoint default)
const retryCli = ref('')
const retryRuntimeType = ref<number | ''>('')

// CLI options shown in the retry modal dropdown
const cliOptions = [
  { value: '', label: '— Use agent defaults' },
  { value: 'opencode', label: 'OpenCode (CLI)' },
  { value: 'opencode-server', label: 'OpenCode (HTTP Server)' },
  { value: 'codex', label: 'Codex CLI' },
  { value: 'copilot', label: 'GitHub Copilot CLI' },
]

// Runtime options shown in the retry modal dropdown
const runtimeOptions = [
  { value: '', label: '— Use org default' },
  ...Object.entries(RuntimeTypeLabels).map(([k, v]) => ({ value: Number(k), label: v })),
]

async function openRetryModal() {
  // Load available agents for the agent-selector dropdown
  if (!agentsStore.agents.length) await agentsStore.fetchAgents()
  // Pre-select the current agent
  retryAgentId.value = store.currentSession?.agentId ?? ''
  retryModel.value = ''
  retryCli.value = ''
  retryRuntimeType.value = ''
  retryDockerImage.value = agentImageOptions[0].value
  retryCustomDockerImage.value = ''
  retryKeepContainer.value = false
  showRetryModal.value = true
}

async function retrySession() {
  showRetryModal.value = false
  retrying.value = true
  try {
    let imageOverride: string | undefined
    if (retryDockerImage.value === 'custom') {
      imageOverride = retryCustomDockerImage.value.trim() || undefined
    } else if (retryDockerImage.value !== agentImageOptions[0].value) {
      imageOverride = retryDockerImage.value
    }

    // Compute CLI overrides from the combined retryCli selector
    let runnerTypeOverride: number | undefined
    let useHttpServerOverride: boolean | undefined
    if (retryCli.value === 'opencode') {
      runnerTypeOverride = RunnerType.OpenCode
      useHttpServerOverride = false
    } else if (retryCli.value === 'opencode-server') {
      runnerTypeOverride = RunnerType.OpenCode
      useHttpServerOverride = true
    } else if (retryCli.value === 'codex') {
      runnerTypeOverride = RunnerType.Codex
      useHttpServerOverride = false
    } else if (retryCli.value === 'copilot') {
      runnerTypeOverride = RunnerType.GitHubCopilotCli
      useHttpServerOverride = false
    }

    const result = await store.retrySession(sessionId, {
      dockerImageOverride: imageOverride,
      keepContainer: retryKeepContainer.value || undefined,
      agentIdOverride: retryAgentId.value !== store.currentSession?.agentId ? retryAgentId.value : undefined,
      modelOverride: retryModel.value.trim() || undefined,
      runnerTypeOverride,
      useHttpServerOverride,
      runtimeTypeOverride: retryRuntimeType.value !== '' ? retryRuntimeType.value as number : undefined,
    })
    if (result?.retriedSessionId) {
      navigateTo(`/projects/${projectId}/runs/agent-sessions/${result.retriedSessionId}`)
    } else {
      console.error('Retry response missing retriedSessionId — falling back to runs list', result)
      await store.fetchAgentSessions(projectId)
      navigateTo(`/projects/${projectId}/runs?tab=agent`)
    }
  } finally {
    retrying.value = false
  }
}

async function copyLogsToClipboard() {
  const text = store.currentSessionLogs.map(l => `${formatLogTime(l.timestamp)} ${stripAnsiCodes(l.line)}`).join('\n')
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

function formatLogTime(d: string) {
  const dt = new Date(d)
  return `${String(dt.getHours()).padStart(2, '0')}:${String(dt.getMinutes()).padStart(2, '0')}:${String(dt.getSeconds()).padStart(2, '0')}`
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

function statusClass(status: CiCdRunStatus | AgentSessionStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded:
    case AgentSessionStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running:
    case AgentSessionStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed:
    case AgentSessionStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled:
    case AgentSessionStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function statusDot(status: CiCdRunStatus | AgentSessionStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded:
    case AgentSessionStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running:
    case AgentSessionStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed:
    case AgentSessionStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled:
    case AgentSessionStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}
</script>
