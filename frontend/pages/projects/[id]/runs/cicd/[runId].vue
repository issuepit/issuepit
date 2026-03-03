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

      <!-- Logs / Details -->
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
    </template>

    <div v-else-if="!store.loading" class="flex flex-col items-center justify-center py-16 text-center">
      <p class="text-gray-400 font-medium">{{ store.error || 'Run not found' }}</p>
    </div>

    <ErrorBox :error="store.error" />
  </div>
</template>

<script setup lang="ts">
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { CiCdRunStatus } from '~/types'

const route = useRoute()
const projectId = route.params.id as string
const runId = route.params.runId as string

const store = useCiCdRunsStore()

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
})
const retryConflict = ref<{ message: string; activeRunId: string } | null>(null)

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
    ? store.currentRunLogs
    : store.currentRunLogs.filter(l => l.stream === activeStream.value)
)

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

  // Connect to the CiCd output hub to receive live log lines and run-completed events
  await connectCicd()
  if (cicdConnection.value) {
    await cicdConnection.value.invoke('JoinRun', runId).catch((e: unknown) => { console.warn('Failed to join run group', e) })
    cicdConnection.value.on('LogLine', (event: { runId: string; payload: string }) => {
      try {
        const data = JSON.parse(event.payload) as { event?: string; stream?: string; line?: string; timestamp?: string }
        if (data.event === 'run-completed') {
          now.value = Date.now()
          // Refresh the run info (status, endedAt) now that the run is finished
          store.fetchRun(runId)
        } else if (data.event === 'run-heartbeat') {
          now.value = Date.now()
        } else if (data.line !== undefined) {
          store.currentRunLogs.push({
            id: crypto.randomUUID(),
            line: data.line,
            stream: data.stream ?? 'stdout',
            streamName: data.stream ? (data.stream.charAt(0).toUpperCase() + data.stream.slice(1)) : 'Stdout',
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
      if (data.runId === runId) store.fetchRun(runId)
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
