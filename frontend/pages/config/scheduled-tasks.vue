<template>
  <div>
    <!-- Page header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Scheduled Tasks</h2>
        <p class="text-sm text-gray-400 mt-0.5">Monitor background task runs across all projects.</p>
      </div>
    </div>

    <!-- Filters bar -->
    <div class="flex flex-wrap gap-3 mb-6">
      <!-- Project filter -->
      <select
        v-model="filterProject"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500"
        @change="applyFilters"
      >
        <option value="">All Projects</option>
        <option
          v-for="proj in store.projects"
          :key="proj.projectId"
          :value="proj.projectId"
        >
          {{ proj.name }}
        </option>
      </select>

      <!-- Type filter -->
      <select
        v-model="filterType"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500"
        @change="applyFilters"
      >
        <option value="">All Types</option>
        <option value="GitHubSync">GitHub Sync</option>
        <option value="BranchDetection">Branch Detection</option>
        <option value="ConfigRepoSync">Config Repo Sync</option>
        <option value="SimilarIssues">Similar Issues</option>
      </select>

      <!-- Status filter -->
      <select
        v-model="filterStatus"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500"
        @change="applyFilters"
      >
        <option value="">All Statuses</option>
        <option value="Pending">Pending</option>
        <option value="Running">Running</option>
        <option value="Succeeded">Succeeded</option>
        <option value="Failed">Failed</option>
      </select>

      <button
        v-if="hasActiveFilters"
        class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-1.5"
        @click="clearFilters"
      >
        Clear filters ×
      </button>

      <div class="flex-1" />

      <button
        :disabled="store.loading"
        class="flex items-center gap-1.5 text-sm bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50"
        @click="applyFilters"
      >
        <svg
          class="w-4 h-4"
          :class="store.loading ? 'animate-spin' : ''"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
          />
        </svg>
        Refresh
      </button>
    </div>

    <!-- Loading -->
    <div
      v-if="store.loading"
      class="flex items-center justify-center py-16"
    >
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="filteredRuns.length === 0"
      class="py-16 text-center"
    >
      <p class="text-gray-500">No scheduled task runs found.</p>
      <p
        v-if="hasActiveFilters"
        class="text-xs text-gray-600 mt-1"
      >
        Try clearing the filters.
      </p>
    </div>

    <!-- Runs table -->
    <div
      v-else
      class="rounded-xl border border-gray-800 overflow-hidden"
    >
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Type</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Project</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Summary</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr
            v-for="run in filteredRuns"
            :key="run.id"
            class="hover:bg-gray-900/50 transition-colors"
          >
            <td class="px-4 py-3">
              <span
                :class="statusClass(run.status)"
                class="text-xs px-2 py-0.5 rounded-full font-medium"
              >
                {{ statusLabel(run.status) }}
              </span>
            </td>
            <td class="px-4 py-3">
              <NuxtLink
                :to="typeSettingsLink(run)"
                class="text-xs bg-blue-900/30 text-blue-300 px-2 py-0.5 rounded-full font-medium hover:bg-blue-900/50 transition-colors"
              >
                {{ typeLabel(run.type) }}
              </NuxtLink>
            </td>
            <td class="px-4 py-3 text-gray-300 text-xs font-medium">
              <NuxtLink
                v-if="run.projectId"
                :to="`/projects/${run.projectId}/github-sync`"
                class="hover:text-brand-400 transition-colors"
              >
                {{ run.projectName }}
              </NuxtLink>
              <span
                v-else
                class="text-gray-500"
              >—</span>
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs max-w-xs truncate">
              {{ run.summary || '—' }}
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs whitespace-nowrap">
              <DateDisplay :date="run.startedAt" mode="auto" />
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs whitespace-nowrap">
              {{ duration(run.startedAt, run.completedAt) }}
            </td>
            <td class="px-4 py-3 text-right whitespace-nowrap">
              <NuxtLink
                v-if="run.type === 'GitHubSync'"
                :to="`/projects/${run.projectId}/github-sync?tab=Sync+Runs`"
                class="text-xs text-brand-400 hover:text-brand-300"
              >
                Details →
              </NuxtLink>
              <button
                v-else
                class="text-xs text-brand-400 hover:text-brand-300"
                @click="openRunLogs(run)"
              >
                View logs →
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Run count footer -->
    <p
      v-if="filteredRuns.length > 0"
      class="text-xs text-gray-600 mt-3 text-right"
    >
      Showing {{ filteredRuns.length }} run(s)
    </p>

    <!-- Run logs modal -->
    <div
      v-if="logsModal"
      class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4"
      @click.self="logsModal = null; logsModalError = null"
    >
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-2xl shadow-xl flex flex-col max-h-[80vh]">
        <div class="flex items-center justify-between px-6 py-4 border-b border-gray-800">
          <div>
            <h2 class="text-base font-bold text-white">{{ typeLabel(logsModal.type) }} Run Logs</h2>
            <p class="text-xs text-gray-500 mt-0.5">
              <span :class="statusClass(logsModal.status)" class="px-1.5 py-0.5 rounded-full font-medium">
                {{ statusLabel(logsModal.status) }}
              </span>
              <span class="ml-2"><DateDisplay :date="logsModal.startedAt" mode="auto" /></span>
              <span v-if="logsModal.summary" class="ml-2">— {{ logsModal.summary }}</span>
            </p>
          </div>
          <button class="text-gray-500 hover:text-gray-300 text-xl leading-none" @click="logsModal = null; logsModalError = null">&times;</button>
        </div>
        <div class="overflow-y-auto p-4 font-mono text-xs space-y-0.5">
          <div v-if="logsModalLoading" class="text-gray-600 text-center py-6">Loading…</div>
          <div v-else-if="logsModalError" class="text-red-400 text-center py-6">{{ logsModalError }}</div>
          <div v-else-if="!logsModal.logs?.length" class="text-gray-600 text-center py-6">No log entries.</div>
          <div
            v-for="log in logsModal.logs"
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
  </div>
</template>

<script setup lang="ts">
import type { ScheduledTaskType } from '~/types'
import { GitHubSyncRunStatus, GitHubSyncLogLevel } from '~/types'
import { useScheduledTasksStore } from '~/stores/scheduled-tasks'
import { useApi } from '~/composables/useApi'

const store = useScheduledTasksStore()

const filterProject = ref('')
const filterType = ref('')
const filterStatus = ref('')

const hasActiveFilters = computed(
  () => !!filterProject.value || !!filterType.value || !!filterStatus.value,
)

// Type filtering is client-side (currently only one type exists, but this allows future extension)
const filteredRuns = computed(() => {
  if (!filterType.value) return store.runs
  return store.runs.filter(r => r.type === filterType.value)
})

async function applyFilters() {
  await store.fetchRuns({
    projectId: filterProject.value || undefined,
    status: filterStatus.value || undefined,
  })
}

function clearFilters() {
  filterProject.value = ''
  filterType.value = ''
  filterStatus.value = ''
  applyFilters()
}

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

function typeLabel(type: ScheduledTaskType): string {
  switch (type) {
    case 'GitHubSync': return 'GitHub Sync'
    case 'BranchDetection': return 'Branch Detection'
    case 'ConfigRepoSync': return 'Config Repo Sync'
    case 'SimilarIssues': return 'Similar Issues'
    default: return type
  }
}

function typeSettingsLink(run: { type: ScheduledTaskType; projectId?: string | null }): string {
  switch (run.type) {
    case 'GitHubSync':
      return run.projectId ? `/projects/${run.projectId}/github-sync` : '/config/keys'
    case 'BranchDetection':
      return run.projectId ? `/projects/${run.projectId}/github-sync` : '/config/keys'
    case 'SimilarIssues':
      return run.projectId ? `/projects/${run.projectId}/settings` : '/config/keys'
    case 'ConfigRepoSync':
      return '/admin/tenants'
    default:
      return '/config/keys'
  }
}

interface RunLog { id: string; level: GitHubSyncLogLevel; message: string; timestamp: string }
interface RunDetail {
  id: string
  type: ScheduledTaskType
  status: GitHubSyncRunStatus
  summary?: string
  startedAt: string
  completedAt?: string | null
  logs?: RunLog[]
}

const logsModal = ref<RunDetail | null>(null)
const logsModalLoading = ref(false)
const logsModalError = ref<string | null>(null)

async function openRunLogs(run: { id: string; type: ScheduledTaskType; status: GitHubSyncRunStatus; summary?: string; startedAt: string; completedAt?: string | null }) {
  logsModalLoading.value = true
  logsModalError.value = null
  logsModal.value = { id: run.id, type: run.type, status: run.status, summary: run.summary, startedAt: run.startedAt, completedAt: run.completedAt }
  try {
    const { get } = useApi()
    let detail: Omit<RunDetail, 'type'>
    if (run.type === 'SimilarIssues') {
      detail = await get<Omit<RunDetail, 'type'>>(`/api/similar-issue-runs/${run.id}`)
    } else if (run.type === 'BranchDetection') {
      detail = await get<Omit<RunDetail, 'type'>>(`/api/scheduled-tasks/branch-detection-runs/${run.id}`)
    } else if (run.type === 'ConfigRepoSync') {
      detail = await get<Omit<RunDetail, 'type'>>(`/api/scheduled-tasks/config-repo-sync-runs/${run.id}`)
    } else {
      logsModalLoading.value = false
      return
    }
    logsModal.value = { ...detail, type: run.type }
  } catch {
    logsModalError.value = 'Failed to load logs. Please try again.'
  } finally {
    logsModalLoading.value = false
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

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('en-GB', { hour12: false })
}

function duration(start: string, end?: string | null): string {
  if (!end) return '—'
  const startMs = new Date(start).getTime()
  const endMs = new Date(end).getTime()
  if (Number.isNaN(startMs) || Number.isNaN(endMs)) return '—'
  const ms = endMs - startMs
  if (ms < 0) return '—'
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${Math.round(ms / 1000)}s`
  return `${Math.round(ms / 60000)}m`
}

onMounted(async () => {
  await Promise.all([store.fetchProjects(), store.fetchRuns()])
})
</script>
