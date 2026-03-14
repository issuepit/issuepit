<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-2 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Runs', to: `/projects/${id}/runs`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
      ]" />
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
      <div class="flex justify-end mb-3">
        <button @click="triggerModal.open = true"
          class="flex items-center gap-1.5 bg-brand-600 hover:bg-brand-700 text-white text-sm px-3 py-1.5 rounded-lg transition-colors">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Trigger Run
        </button>
      </div>
      <div v-if="store.runs.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Trigger</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Workflow</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Commit</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Source</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
              <th class="px-4 py-3" />
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="run in store.runs" :key="run.id"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateTo(`/projects/${id}/runs/cicd/${run.id}`)">
              <td class="px-4 py-3">
                <CiCdStatusChip :runs="[run]" />
              </td>
              <td class="px-4 py-3">
                <span v-if="run.eventName" class="text-xs bg-gray-800 text-gray-300 px-1.5 py-0.5 rounded font-mono">
                  {{ run.eventName }}
                </span>
                <span v-else class="text-gray-600 text-xs">—</span>
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
              <td class="px-4 py-3 text-right">
                <button v-if="run.status === CiCdRunStatus.Pending || run.status === CiCdRunStatus.Running"
                  class="text-xs text-red-400 hover:text-red-300 transition-colors"
                  @click.stop="cancelRun(run.id)">
                  Cancel
                </button>
                <button v-else-if="run.status === CiCdRunStatus.Failed || run.status === CiCdRunStatus.Cancelled"
                  class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop="retryRun(run.id)">
                  Retry
                </button>
              </td>
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
              <th class="px-4 py-3" />
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="session in store.agentSessions" :key="session.id"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateTo(`/projects/${id}/runs/agent-sessions/${session.id}`)">
              <td class="px-4 py-3">
                <AgentSessionStatusChip :session="session" />
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${id}/issues/${session.issueNumber}`"
                  class="text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop>
                  #{{ formatIssueId(session.issueNumber, projectsStore.currentProject) }} {{ session.issueTitle }}
                </NuxtLink>
              </td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ session.gitBranch || '—' }}</td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ session.commitSha?.slice(0, 7) || '—' }}</td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ formatDate(session.startedAt) }}</td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(session.startedAt, session.endedAt) }}</td>
              <td class="px-4 py-3 text-right">
                <button v-if="session.status === AgentSessionStatus.Failed || session.status === AgentSessionStatus.Cancelled"
                  class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop="retrySession(session.id)">
                  Retry
                </button>
              </td>
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

    <!-- Trigger CI/CD modal -->
    <TriggerCiCdModal
      v-if="triggerModal.open"
      :project-id="id"
      :commit-sha="triggerModal.commitSha"
      @close="triggerModal.open = false"
      @triggered="onRunTriggered"
    />
  </div>
</template>

<script setup lang="ts">
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { useProjectsStore } from '~/stores/projects'
import { CiCdRunStatus, AgentSessionStatus } from '~/types'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const id = route.params.id as string

const store = useCiCdRunsStore()
const projectsStore = useProjectsStore()
const tabs = ['CI/CD Runs', 'Agent Runs'] as const
const activeTab = ref<typeof tabs[number]>(route.query.tab === 'agent' ? 'Agent Runs' : 'CI/CD Runs')

const triggerModal = reactive({ open: false, commitSha: '' })

function onRunTriggered() {
  triggerModal.open = false
  store.fetchRuns(id)
}

const { connection, isConnected, connect } = useSignalR('/hubs/project')

// `now` is updated on each server push so the duration column stays live without a client-side timer
const now = ref(Date.now())

async function refreshRunsData() {
  await Promise.all([
    store.fetchRuns(id),
    store.fetchAgentSessions(id),
  ])
  now.value = Date.now()
}

onMounted(async () => {
  projectsStore.fetchProject(id)
  await refreshRunsData()

  // Connect to SignalR for live run updates
  await connect()
  if (connection.value) {
    await connection.value.invoke('JoinProject', id).catch((e) => { console.warn('Failed to join project group', e) })
    connection.value.on('RunsUpdated', refreshRunsData)
  }
})

async function cancelRun(runId: string) {
  await store.cancelRun(runId)
}

async function retrySession(sessionId: string) {
  await store.retrySession(sessionId)
  await store.fetchAgentSessions(id)
}

async function retryRun(runId: string) {
  await store.retryRun(runId)
  await store.fetchRuns(id)
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
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

</script>
