<template>
  <!-- Trigger button + run count header -->
  <div class="flex items-center justify-between mb-4">
    <p class="text-sm text-gray-400">{{ runs.length }} run(s)</p>
    <button
      :disabled="triggering"
      class="flex items-center gap-1.5 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm px-3 py-1.5 rounded-lg transition-colors"
      @click="$emit('trigger')"
    >
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
      </svg>
      {{ triggering ? 'Triggered…' : triggerLabel }}
    </button>
  </div>

  <!-- Loading spinner -->
  <div v-if="loading" class="flex items-center justify-center py-16">
    <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
  </div>

  <!-- Empty state -->
  <div v-else-if="runs.length === 0" class="py-16 text-center">
    <p class="text-gray-500">No runs yet. Trigger one to see history here.</p>
  </div>

  <!-- Runs table -->
  <div v-else class="rounded-xl border border-gray-800 overflow-hidden">
    <table class="w-full text-sm">
      <thead class="bg-gray-900">
        <tr>
          <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
          <th class="text-left px-4 py-3 text-gray-400 font-medium">Summary</th>
          <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
          <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
          <th class="px-4 py-3" />
        </tr>
      </thead>
      <tbody class="divide-y divide-gray-800">
        <tr
          v-for="run in runs"
          :key="run.id"
          class="hover:bg-gray-900/50 transition-colors cursor-pointer"
          @click="$emit('open-run', run.id)"
        >
          <td class="px-4 py-3">
            <span :class="statusClass(run.status)" class="text-xs px-2 py-0.5 rounded-full font-medium">
              {{ statusLabel(run.status) }}
            </span>
          </td>
          <td class="px-4 py-3 text-gray-300 text-xs">{{ run.summary || '—' }}</td>
          <td class="px-4 py-3 text-gray-400 text-xs">{{ formatDate(run.startedAt) }}</td>
          <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(run.startedAt, run.completedAt) }}</td>
          <td class="px-4 py-3 text-right">
            <button class="text-xs text-brand-400 hover:text-brand-300" @click.stop="$emit('open-run', run.id)">
              View logs →
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Log detail modal -->
  <div
    v-if="selectedRun"
    class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4"
    @click.self="selectedRun = null"
  >
    <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-2xl shadow-xl flex flex-col max-h-[80vh]">
      <div class="flex items-center justify-between px-6 py-4 border-b border-gray-800">
        <div>
          <h2 class="text-base font-bold text-white">Run Logs</h2>
          <p class="text-xs text-gray-500 mt-0.5">
            <span :class="statusClass(selectedRun.status)" class="px-1.5 py-0.5 rounded-full font-medium">
              {{ statusLabel(selectedRun.status) }}
            </span>
            <span class="ml-2">{{ formatDate(selectedRun.startedAt) }}</span>
            <span v-if="selectedRun.summary" class="ml-2">— {{ selectedRun.summary }}</span>
          </p>
        </div>
        <button class="text-gray-500 hover:text-gray-300 text-xl leading-none" @click="selectedRun = null">&times;</button>
      </div>
      <div class="overflow-y-auto p-4 font-mono text-xs space-y-0.5">
        <div v-if="!selectedRun.logs?.length" class="text-gray-600 text-center py-6">No log entries.</div>
        <div
          v-for="log in selectedRun.logs"
          :key="log.id"
          :class="logLineClass(log.level)"
        >
          <span class="text-gray-600 mr-2">{{ formatTime(log.timestamp) }}</span>
          <span :class="logBadgeClass(log.level)" class="mr-2 text-xs px-1 rounded">
            [{{ logLevelLabel(log.level) }}]
          </span>
          {{ log.message }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { GitHubSyncRun, GitHubSyncRunDetail } from '~/types'
import { GitHubSyncRunStatus, GitHubSyncLogLevel } from '~/types'

const props = withDefaults(defineProps<{
  runs: GitHubSyncRun[]
  loading?: boolean
  triggering?: boolean
  triggerLabel?: string
  fetchRunDetail?: (runId: string) => Promise<GitHubSyncRunDetail | undefined>
}>(), {
  loading: false,
  triggering: false,
  triggerLabel: 'Trigger Now',
})

defineEmits<{
  trigger: []
  'open-run': [runId: string]
}>()

const selectedRun = ref<GitHubSyncRunDetail | null>(null)

// Expose openRun so parent pages can still call it via template ref,
// but the default implementation uses fetchRunDetail if provided.
async function openRun(runId: string) {
  if (props.fetchRunDetail) {
    const detail = await props.fetchRunDetail(runId)
    if (detail) selectedRun.value = detail
  }
}

defineExpose({ openRun })

function statusLabel(status: GitHubSyncRunStatus): string {
  switch (status) {
    case GitHubSyncRunStatus.Pending: return 'Pending'
    case GitHubSyncRunStatus.Running: return 'Running'
    case GitHubSyncRunStatus.Succeeded: return 'Succeeded'
    case GitHubSyncRunStatus.Failed: return 'Failed'
    default: return String(status)
  }
}

function statusClass(status: GitHubSyncRunStatus): string {
  switch (status) {
    case GitHubSyncRunStatus.Succeeded: return 'bg-green-900/40 text-green-300'
    case GitHubSyncRunStatus.Failed: return 'bg-red-900/40 text-red-300'
    case GitHubSyncRunStatus.Running: return 'bg-blue-900/40 text-blue-300'
    default: return 'bg-gray-800 text-gray-400'
  }
}

function logLevelLabel(level: GitHubSyncLogLevel): string {
  switch (level) {
    case GitHubSyncLogLevel.Warn: return 'WARN'
    case GitHubSyncLogLevel.Error: return 'ERR'
    default: return 'INFO'
  }
}

function logLineClass(level: GitHubSyncLogLevel): string {
  switch (level) {
    case GitHubSyncLogLevel.Warn: return 'text-yellow-300'
    case GitHubSyncLogLevel.Error: return 'text-red-400'
    default: return 'text-gray-300'
  }
}

function logBadgeClass(level: GitHubSyncLogLevel): string {
  switch (level) {
    case GitHubSyncLogLevel.Warn: return 'bg-yellow-900/40 text-yellow-300'
    case GitHubSyncLogLevel.Error: return 'bg-red-900/40 text-red-300'
    default: return 'bg-gray-800 text-gray-500'
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString()
}

function duration(start: string, end?: string | null): string {
  if (!end) return '—'
  const ms = new Date(end).getTime() - new Date(start).getTime()
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${Math.round(ms / 1000)}s`
  return `${Math.round(ms / 60000)}m`
}
</script>
