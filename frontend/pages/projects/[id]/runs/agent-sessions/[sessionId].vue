<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-6">
      <NuxtLink :to="`/projects/${projectId}/runs?tab=agent`" class="text-gray-500 hover:text-gray-300 transition-colors">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </NuxtLink>
      <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
          d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
      </svg>
      <h1 class="text-xl font-bold text-white">Agent Session</h1>
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
            <NuxtLink :to="`/projects/${projectId}/issues/${store.currentSession.issueId}`"
              class="text-sm text-brand-400 hover:text-brand-300 transition-colors">
              #{{ store.currentSession.issueNumber }} {{ store.currentSession.issueTitle }}
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
import { CiCdRunStatus } from '~/types'

const route = useRoute()
const projectId = route.params.id as string
const sessionId = route.params.sessionId as string

const store = useCiCdRunsStore()

onMounted(async () => {
  await store.fetchAgentSession(sessionId)
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
