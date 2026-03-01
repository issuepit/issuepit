<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-6">
      <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </NuxtLink>
      <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M13 10V3L4 14h7v7l9-11h-7z" />
      </svg>
      <h1 class="text-xl font-bold text-white">Runs</h1>
    </div>

    <!-- Tabs -->
    <div class="flex gap-1 mb-6 border-b border-gray-800">
      <button v-for="tab in tabs" :key="tab"
        :class="[
          'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
          activeTab === tab
            ? 'text-white border-brand-500'
            : 'text-gray-500 border-transparent hover:text-gray-300'
        ]"
        @click="activeTab = tab">
        {{ tab }}
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- CI/CD Runs -->
    <template v-else-if="activeTab === 'CI/CD Runs'">
      <div v-if="store.runs.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Workflow</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Commit</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Source</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="run in store.runs" :key="run.id" class="hover:bg-gray-900/50 transition-colors">
              <td class="px-4 py-3">
                <span :class="statusClass(run.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                  <span :class="statusDot(run.status)" class="w-1.5 h-1.5 rounded-full" />
                  {{ run.statusName }}
                </span>
              </td>
              <td class="px-4 py-3 text-gray-300">{{ run.workflow || '—' }}</td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ run.branch || '—' }}</td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ run.commitSha?.slice(0, 7) || '—' }}</td>
              <td class="px-4 py-3">
                <span v-if="run.externalSource"
                  class="text-xs bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded">
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
      <div v-else class="flex flex-col items-center justify-center py-16 text-center">
        <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mb-3">
          <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No CI/CD runs yet</p>
        <p class="text-gray-600 text-sm mt-1">Runs will appear here once triggered</p>
      </div>
    </template>

    <!-- Agent Runs -->
    <template v-else-if="activeTab === 'Agent Runs'">
      <div v-if="store.agentSessions.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Agent</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Issue</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Commit</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="session in store.agentSessions" :key="session.id" class="hover:bg-gray-900/50 transition-colors">
              <td class="px-4 py-3">
                <span :class="statusClass(session.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                  <span :class="statusDot(session.status)" class="w-1.5 h-1.5 rounded-full" />
                  {{ session.statusName }}
                </span>
              </td>
              <td class="px-4 py-3 text-gray-300">{{ session.agentName }}</td>
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${id}/issues`"
                  class="text-brand-400 hover:text-brand-300 transition-colors">
                  #{{ session.issueNumber }} {{ session.issueTitle }}
                </NuxtLink>
              </td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ session.gitBranch || '—' }}</td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ session.commitSha?.slice(0, 7) || '—' }}</td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ formatDate(session.startedAt) }}</td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(session.startedAt, session.endedAt) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-else class="flex flex-col items-center justify-center py-16 text-center">
        <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mb-3">
          <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No agent runs yet</p>
        <p class="text-gray-600 text-sm mt-1">Agent sessions will appear here once started</p>
      </div>
    </template>

    <ErrorBox :error="store.error" />
  </div>
</template>

<script setup lang="ts">
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { CiCdRunStatus } from '~/types'

const route = useRoute()
const id = route.params.id as string

const store = useCiCdRunsStore()
const tabs = ['CI/CD Runs', 'Agent Runs'] as const
const activeTab = ref<typeof tabs[number]>('CI/CD Runs')

onMounted(async () => {
  await Promise.all([
    store.fetchRuns(id),
    store.fetchAgentSessions(id),
  ])
})

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
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
