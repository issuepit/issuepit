<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-2 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Runs', to: `/projects/${id}/runs?tab=agent`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
        { label: 'opencode Sessions', to: `/projects/${id}/runs/opencode-sessions`, icon: 'M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z' },
      ]" />
    </div>

    <!-- Description -->
    <div class="mb-6">
      <p class="text-sm text-gray-400">
        Browse preserved opencode sessions. Sessions with a saved database snapshot can be continued in the next agent run on the same issue.
      </p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Sessions with opencode IDs -->
    <template v-else>
      <div v-if="openCodeSessions.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Issue</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Agent</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">opencode Session</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
              <th class="px-4 py-3" />
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="session in openCodeSessions" :key="session.id"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateTo(`/projects/${id}/runs/agent-sessions/${session.id}`)">
              <td class="px-4 py-3">
                <AgentSessionStatusChip :session="session" />
              </td>
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${id}/issues/${session.issueNumber}`"
                  class="text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop>
                  #{{ formatIssueId(session.issueNumber, projectsStore.currentProject) }} {{ session.issueTitle }}
                </NuxtLink>
              </td>
              <td class="px-4 py-3 text-gray-300 text-xs">{{ session.agentName }}</td>
              <td class="px-4 py-3">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-xs text-gray-300 font-mono bg-gray-800 px-1.5 py-0.5 rounded">{{ session.openCodeSessionId }}</span>
                  <!-- Link to opencode web UI when server is still running -->
                  <a v-if="session.serverWebUiUrl"
                    :href="session.serverWebUiUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="flex items-center gap-1 text-xs text-brand-400 hover:text-brand-300 transition-colors"
                    @click.stop>
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                    </svg>
                    Open UI
                  </a>
                </div>
              </td>
              <td class="px-4 py-3 text-gray-400 text-xs"><DateDisplay :date="session.startedAt" mode="auto" /></td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(session.startedAt, session.endedAt) }}</td>
              <td class="px-4 py-3 text-right">
                <!-- Preserved indicator -->
                <span v-if="preservedIds.has(session.id)"
                  class="inline-flex items-center gap-1 text-xs text-green-400">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                  </svg>
                  Preserved
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-else class="flex flex-col items-center justify-center py-16 text-center">
        <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mb-3">
          <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No opencode sessions yet</p>
        <p class="text-gray-600 text-sm mt-1">Sessions with opencode IDs will appear here once agent runs complete.</p>
      </div>
    </template>

    <ErrorBox v-if="error" :error="error" />
  </div>
</template>

<script setup lang="ts">
import type { AgentSession } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useApi } from '~/composables/useApi'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const api = useApi()

const loading = ref(true)
const error = ref<string | null>(null)
const sessions = ref<AgentSession[]>([])

// Sessions that have an opencode session ID
const openCodeSessions = computed(() =>
  sessions.value.filter(s => s.openCodeSessionId),
)

// IDs of sessions that have a preserved DB snapshot (can be restored in the next container run)
const preservedIds = computed(() =>
  new Set(sessions.value.filter(s => s.openCodeDbS3Url).map(s => s.id)),
)

onMounted(async () => {
  await projectsStore.fetchProject(id)
  try {
    sessions.value = await api.get<AgentSession[]>(`/api/projects/${id}/agent-sessions`)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load sessions'
  } finally {
    loading.value = false
  }
})

function duration(start: string, end?: string | null) {
  const ms = (end ? new Date(end).getTime() : Date.now()) - new Date(start).getTime()
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ${s % 60}s`
  return `${Math.floor(m / 60)}h ${m % 60}m`
}
</script>
