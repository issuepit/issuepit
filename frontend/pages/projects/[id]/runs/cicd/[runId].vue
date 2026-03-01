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
            @click="retryRun">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ retrying ? 'Retrying…' : 'Retry Run' }}
          </button>
        </div>
      </div>

      <!-- Logs -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div class="flex items-center justify-between px-4 py-3 border-b border-gray-800">
          <h2 class="text-sm font-medium text-white">Logs</h2>
          <div class="flex gap-1">
            <button v-for="s in streamTabs" :key="s.value"
              :class="[
                'px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                activeStream === s.value ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'
              ]"
              @click="activeStream = s.value">
              {{ s.label }}
            </button>
          </div>
        </div>
        <div v-if="filteredLogs.length" class="bg-gray-950 p-4 font-mono text-xs overflow-auto max-h-[600px]">
          <div v-for="log in filteredLogs" :key="log.id" class="flex gap-3 leading-5">
            <span class="text-gray-600 shrink-0 select-none">{{ formatLogTime(log.timestamp) }}</span>
            <span :class="log.stream === 2 ? 'text-red-400' : 'text-gray-300'" class="whitespace-pre-wrap break-all">{{ log.line }}</span>
          </div>
        </div>
        <div v-else class="py-10 text-center text-sm text-gray-500">No logs available</div>
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

const streamTabs = [
  { label: 'All', value: null },
  { label: 'Stdout', value: 1 },
  { label: 'Stderr', value: 2 },
]
const activeStream = ref<number | null>(null)

const filteredLogs = computed(() =>
  activeStream.value === null
    ? store.currentRunLogs
    : store.currentRunLogs.filter(l => l.stream === activeStream.value)
)

onMounted(async () => {
  await store.fetchRun(runId)
})

async function retryRun() {
  retrying.value = true
  try {
    await store.retryRun(runId)
    navigateTo(`/projects/${projectId}/runs`)
  } finally {
    retrying.value = false
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
  const ms = (end ? new Date(end).getTime() : Date.now()) - new Date(start).getTime()
  if (ms < 0) return '—'
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ${s % 60}s`
  return `${Math.floor(m / 60)}h ${m % 60}m`
}

function statusClass(status: number) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function statusDot(status: number) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}
</script>
