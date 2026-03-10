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
            <p class="text-sm text-gray-300">{{ store.currentSession.agentName }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Issue</p>
            <NuxtLink :to="`/projects/${projectId}/issues/${store.currentSession.issueNumber}`"
              class="text-sm text-brand-400 hover:text-brand-300 transition-colors">
              #{{ formatIssueId(store.currentSession.issueNumber, projectsStore.currentProject) }} {{ store.currentSession.issueTitle }}
            </NuxtLink>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Branch</p>
            <p class="text-sm text-gray-300 font-mono">{{ store.currentSession.gitBranch || '—' }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Commit</p>
            <p class="text-sm text-gray-300 font-mono">{{ store.currentSession.commitSha?.slice(0, 7) || '—' }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Started</p>
            <p class="text-sm text-gray-400">{{ formatDate(store.currentSession.startedAt) }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 mb-1">Duration</p>
            <p class="text-sm text-gray-400">{{ duration(store.currentSession.startedAt, store.currentSession.endedAt) }}</p>
          </div>
        </div>
        <!-- Retry button for failed/cancelled sessions -->
        <div v-if="store.currentSession.status === AgentSessionStatus.Failed || store.currentSession.status === AgentSessionStatus.Cancelled"
          class="mt-4 pt-4 border-t border-gray-800 flex justify-end">
          <button
            :disabled="retrying"
            class="flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300 disabled:opacity-50 transition-colors"
            @click="showRetryModal = true">
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
        <div v-if="showRetryModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showRetryModal = false">
          <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-md">
            <h3 class="text-base font-semibold text-white mb-4">Retry Session</h3>

            <div class="mb-4 space-y-2 text-sm text-gray-400">
              <div class="flex gap-2">
                <span class="text-gray-500 w-16 shrink-0">Issue</span>
                <span class="text-gray-300">#{{ formatIssueId(store.currentSession.issueNumber, projectsStore.currentProject) }} {{ store.currentSession.issueTitle }}</span>
              </div>
              <div class="flex gap-2">
                <span class="text-gray-500 w-16 shrink-0">Agent</span>
                <span class="text-gray-300">{{ store.currentSession.agentName }}</span>
              </div>
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

            <p class="text-xs text-gray-500 mb-5">A new session will be started for the same issue and agent.</p>

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

        <!-- Logs tab -->
        <template v-if="activeSection === 'logs'">
          <div v-if="filteredLogs.length" class="bg-gray-950 p-4 font-mono text-xs overflow-auto max-h-[600px]">
            <div v-for="log in filteredLogs" :key="log.id" class="flex gap-3 leading-5">
              <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
              <span :class="log.stream === 'stderr' ? 'text-red-400' : 'text-gray-300'" class="whitespace-pre-wrap break-all">{{ log.line }}</span>
            </div>
          </div>
          <div v-else class="py-10 text-center text-sm text-gray-500">No logs available</div>
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
                  <span :class="statusClass(run.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                    <span :class="statusDot(run.status)" class="w-1.5 h-1.5 rounded-full" />
                    {{ run.statusName }}
                  </span>
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
                <td class="px-4 py-3 text-gray-400 text-xs">{{ formatDate(run.startedAt) }}</td>
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
import { CiCdRunStatus, AgentSessionStatus, type AgentSessionLog } from '~/types'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const projectId = route.params.id as string
const sessionId = route.params.sessionId as string

const store = useCiCdRunsStore()
const projectsStore = useProjectsStore()

const sectionTabs = [
  { label: 'Logs', value: 'logs' },
  { label: 'Details', value: 'details' },
]
const activeSection = ref<'logs' | 'details'>('logs')

const streamTabs = [
  { label: 'All', value: null },
  { label: 'Stdout', value: 'stdout' },
  { label: 'Stderr', value: 'stderr' },
]
const activeStream = ref<string | null>(null)

const filteredLogs = computed(() =>
  activeStream.value === null
    ? store.currentSessionLogs
    : store.currentSessionLogs.filter(l => l.stream === activeStream.value)
)

const debugMetadata = computed(() => {
  const entries: Array<{ key: string; value: string }> = []
  for (const log of store.currentSessionLogs) {
    // Match lines like: [DEBUG] Key name   : value (space-colon-space separator)
    const m = log.line.match(/^\[DEBUG\]\s+([^:]+?)\s*:\s(.+)$/)
    if (m) entries.push({ key: m[1].trim(), value: m[2].trim() })
  }
  return entries
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
        const data = JSON.parse(payload) as { event?: string; stream?: string; line?: string; timestamp?: string; status?: string }
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

const agentImageOptions = [
  {
    value: 'ghcr.io/issuepit/issuepit-helper-opencode-act:latest',
    description: 'Most recent stable release — recommended for production use.',
    isDefault: true,
  },
  {
    value: 'ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24',
    description: 'Latest build from the main branch — may include unreleased changes.',
    isDefault: false,
  },
]

// Default to the first (stable/latest) option
const retryDockerImage = ref(agentImageOptions[0].value)
const retryCustomDockerImage = ref('')

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
    await store.retrySession(sessionId, imageOverride ? { dockerImageOverride: imageOverride } : undefined)
    await store.fetchAgentSessions(projectId)
    navigateTo(`/projects/${projectId}/runs?tab=agent`)
  } finally {
    retrying.value = false
  }
}

async function copyLogsToClipboard() {
  const text = store.currentSessionLogs.map(l => `${formatLogTime(l.timestamp)} ${l.line}`).join('\n')
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
