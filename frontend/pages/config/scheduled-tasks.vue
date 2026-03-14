<template>
  <div>
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
              <span class="text-xs bg-blue-900/30 text-blue-300 px-2 py-0.5 rounded-full font-medium">
                {{ typeLabel(run.type) }}
              </span>
            </td>
            <td class="px-4 py-3 text-gray-300 text-xs font-medium">
              <NuxtLink
                :to="`/projects/${run.projectId}/github-sync`"
                class="hover:text-brand-400 transition-colors"
              >
                {{ run.projectName }}
              </NuxtLink>
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs max-w-xs truncate">
              {{ run.summary || '—' }}
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs whitespace-nowrap">
              {{ formatDate(run.startedAt) }}
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs whitespace-nowrap">
              {{ duration(run.startedAt, run.completedAt) }}
            </td>
            <td class="px-4 py-3 text-right">
              <NuxtLink
                :to="`/projects/${run.projectId}/github-sync?tab=Sync+Runs`"
                class="text-xs text-brand-400 hover:text-brand-300"
              >
                Details →
              </NuxtLink>
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
  </div>
</template>

<script setup lang="ts">
import type { ScheduledTaskType } from '~/types'
import { GitHubSyncRunStatus } from '~/types'
import { useScheduledTasksStore } from '~/stores/scheduled-tasks'

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
    default: return type
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
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
