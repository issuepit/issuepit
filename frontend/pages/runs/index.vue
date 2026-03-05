<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-6">
      <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M13 10V3L4 14h7v7l9-11h-7z" />
      </svg>
      <h1 class="text-xl font-bold text-white">All Runs</h1>
    </div>

    <!-- Tabs -->
    <div class="flex gap-1 mb-4 border-b border-gray-800">
      <button v-for="tab in tabs" :key="tab"
        :class="[
          'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
          activeTab === tab
            ? 'text-white border-brand-500'
            : 'text-gray-500 border-transparent hover:text-gray-300'
        ]"
        @click="setTab(tab)">
        {{ tab }}
      </button>
    </div>

    <!-- Filters -->
    <div class="flex flex-wrap gap-3 mb-6">
      <select v-model="filterOrg"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500">
        <option value="">All Orgs</option>
        <option v-for="org in orgsStore.orgs" :key="org.id" :value="org.id">{{ org.name }}</option>
      </select>
      <select v-model="filterProject"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500">
        <option value="">All Projects</option>
        <option v-for="project in filteredProjectOptions" :key="project.id" :value="project.id">{{ project.name }}</option>
      </select>
      <select v-model="filterStatus"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500">
        <option value="">All Statuses</option>
        <option value="pending">Pending</option>
        <option value="running">Running</option>
        <option value="succeeded">Succeeded</option>
        <option value="failed">Failed</option>
        <option value="cancelled">Cancelled</option>
      </select>
      <input v-model="filterBranch" type="text" placeholder="Filter by branch…"
        class="bg-gray-900 border border-gray-700 text-sm text-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-brand-500 w-44" />
      <button v-if="hasActiveFilters"
        class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-1.5"
        @click="clearFilters">
        Clear filters ×
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- All Runs (mixed) -->
    <template v-else-if="activeTab === 'All Runs'">
      <div v-if="allRunsMixed.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Type</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Project</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Description</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="item in allRunsMixed" :key="item.id"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateTo(item.href)">
              <td class="px-4 py-3">
                <span :class="statusClass(item.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                  <span :class="statusDot(item.status)" class="w-1.5 h-1.5 rounded-full" />
                  {{ item.statusName }}
                </span>
              </td>
              <td class="px-4 py-3">
                <span :class="item.type === 'cicd' ? 'bg-sky-900/30 text-sky-400' : 'bg-fuchsia-900/30 text-fuchsia-400'"
                  class="text-xs px-1.5 py-0.5 rounded font-medium">
                  {{ item.type === 'cicd' ? 'CI/CD' : 'Agent' }}
                </span>
              </td>
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${item.projectId}/runs`"
                  class="text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop>
                  {{ item.projectName || '—' }}
                </NuxtLink>
              </td>
              <td class="px-4 py-3 text-gray-300 max-w-xs truncate">{{ item.description }}</td>
              <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ item.branch || '—' }}</td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ formatDate(item.startedAt) }}</td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(item.startedAt, item.endedAt) }}</td>
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
        <p class="text-gray-400 font-medium">No runs found</p>
        <p class="text-gray-600 text-sm mt-1">{{ hasActiveFilters ? 'Try adjusting your filters' : 'Runs will appear here once started' }}</p>
      </div>
    </template>

    <!-- CI/CD Runs -->
    <template v-else-if="activeTab === 'CI/CD Runs'">
      <div v-if="filteredCiCdRuns.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Project</th>
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
            <tr v-for="run in filteredCiCdRuns" :key="run.id"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)">
              <td class="px-4 py-3">
                <span :class="statusClass(run.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                  <span :class="statusDot(run.status)" class="w-1.5 h-1.5 rounded-full" />
                  {{ run.statusName }}
                </span>
              </td>
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${run.projectId}/runs`"
                  class="text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop>
                  {{ run.projectName || '—' }}
                </NuxtLink>
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
        <p class="text-gray-400 font-medium">No CI/CD runs found</p>
        <p class="text-gray-600 text-sm mt-1">{{ hasActiveFilters ? 'Try adjusting your filters' : 'Runs will appear here once triggered' }}</p>
      </div>
    </template>

    <!-- Agent Runs -->
    <template v-else-if="activeTab === 'Agent Runs'">
      <div v-if="filteredAgentSessions.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Project</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Agent</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Issue</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Commit</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="session in filteredAgentSessions" :key="session.id"
              class="hover:bg-gray-900/50 transition-colors cursor-pointer"
              @click="navigateTo(`/projects/${session.projectId}/runs/agent-sessions/${session.id}`)">
              <td class="px-4 py-3">
                <span :class="statusClass(session.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                  <span :class="statusDot(session.status)" class="w-1.5 h-1.5 rounded-full" />
                  {{ session.statusName }}
                </span>
              </td>
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${session.projectId}/runs?tab=agent`"
                  class="text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop>
                  {{ session.projectName }}
                </NuxtLink>
              </td>
              <td class="px-4 py-3 text-gray-300">{{ session.agentName }}</td>
              <td class="px-4 py-3">
                <NuxtLink :to="`/projects/${session.projectId}/issues/${session.issueNumber}`"
                  class="text-brand-400 hover:text-brand-300 transition-colors"
                  @click.stop>
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
        <p class="text-gray-400 font-medium">No agent runs found</p>
        <p class="text-gray-600 text-sm mt-1">{{ hasActiveFilters ? 'Try adjusting your filters' : 'Agent sessions will appear here once started' }}</p>
      </div>
    </template>

    <ErrorBox :error="store.error" />
  </div>
</template>

<script setup lang="ts">
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { useProjectsStore } from '~/stores/projects'
import { useOrgsStore } from '~/stores/orgs'
import { CiCdRunStatus, type AgentSessionStatus } from '~/types'

const store = useCiCdRunsStore()
const projectsStore = useProjectsStore()
const orgsStore = useOrgsStore()

const router = useRouter()
const route = useRoute()

const tabs = ['All Runs', 'CI/CD Runs', 'Agent Runs'] as const

const TAB_QUERY = {
  'All Runs': 'all',
  'CI/CD Runs': 'cicd',
  'Agent Runs': 'agent',
} as const

function getTabFromQueryParam(query: unknown): typeof tabs[number] {
  if (query === TAB_QUERY['CI/CD Runs']) return 'CI/CD Runs'
  if (query === TAB_QUERY['Agent Runs']) return 'Agent Runs'
  return 'All Runs'
}

const activeTab = ref<typeof tabs[number]>(getTabFromQueryParam(route.query.tab))
const filterOrg = ref((route.query.org as string) || '')
const filterProject = ref((route.query.project as string) || '')
const filterStatus = ref((route.query.status as string) || '')
const filterBranch = ref((route.query.branch as string) || '')

function getQueryParamFromTab(tab: typeof tabs[number]): string {
  return TAB_QUERY[tab]
}

function setTab(tab: typeof tabs[number]) {
  activeTab.value = tab
}

watch([activeTab, filterOrg, filterProject, filterStatus, filterBranch], () => {
  router.replace({
    query: {
      tab: getQueryParamFromTab(activeTab.value),
      ...(filterOrg.value ? { org: filterOrg.value } : {}),
      ...(filterProject.value ? { project: filterProject.value } : {}),
      ...(filterStatus.value ? { status: filterStatus.value } : {}),
      ...(filterBranch.value ? { branch: filterBranch.value } : {}),
    },
  })
})

const hasActiveFilters = computed(() =>
  !!(filterOrg.value || filterProject.value || filterStatus.value || filterBranch.value)
)

function clearFilters() {
  filterOrg.value = ''
  filterProject.value = ''
  filterStatus.value = ''
  filterBranch.value = ''
}

// Projects filtered by selected org
const filteredProjectOptions = computed(() => {
  if (!filterOrg.value) return projectsStore.projects
  return projectsStore.projects.filter(p => p.orgId === filterOrg.value)
})

// Case-insensitive status label matching
function statusLabelMatches(statusName: string, filter: string): boolean {
  return statusName.toLowerCase() === filter.toLowerCase()
}

// Looks up a project's organization ID by project ID. Returns empty string if project not found.
function getProjectOrgId(projectId: string): string {
  return projectsStore.projects.find(p => p.id === projectId)?.orgId ?? ''
}

const filteredCiCdRuns = computed(() => {
  return store.runs.filter(run => {
    if (filterOrg.value && getProjectOrgId(run.projectId) !== filterOrg.value) return false
    if (filterProject.value && run.projectId !== filterProject.value) return false
    if (filterStatus.value && !statusLabelMatches(run.statusName, filterStatus.value)) return false
    if (filterBranch.value && !(run.branch || '').toLowerCase().includes(filterBranch.value.toLowerCase())) return false
    return true
  })
})

const filteredAgentSessions = computed(() => {
  return store.dashboardSessions.filter(session => {
    if (filterOrg.value && getProjectOrgId(session.projectId) !== filterOrg.value) return false
    if (filterProject.value && session.projectId !== filterProject.value) return false
    if (filterStatus.value && !statusLabelMatches(session.statusName, filterStatus.value)) return false
    if (filterBranch.value && !(session.gitBranch || '').toLowerCase().includes(filterBranch.value.toLowerCase())) return false
    return true
  })
})

interface MixedRunItem {
  id: string
  type: 'cicd' | 'agent'
  status: CiCdRunStatus | AgentSessionStatus
  statusName: string
  projectId: string
  projectName?: string
  description: string
  branch?: string
  startedAt: string
  endedAt?: string
  href: string
}

const allRunsMixed = computed((): MixedRunItem[] => {
  const cicdItems: MixedRunItem[] = filteredCiCdRuns.value.map(run => ({
    id: run.id,
    type: 'cicd',
    status: run.status,
    statusName: run.statusName,
    projectId: run.projectId,
    projectName: run.projectName,
    description: run.workflow || run.branch || '—',
    branch: run.branch,
    startedAt: run.startedAt,
    endedAt: run.endedAt,
    href: `/projects/${run.projectId}/runs/cicd/${run.id}`,
  }))

  const agentItems: MixedRunItem[] = filteredAgentSessions.value.map(session => ({
    id: session.id,
    type: 'agent',
    status: session.status,
    statusName: session.statusName,
    projectId: session.projectId,
    projectName: session.projectName,
    description: `#${session.issueNumber} ${session.issueTitle}`,
    branch: session.gitBranch,
    startedAt: session.startedAt,
    endedAt: session.endedAt,
    href: `/projects/${session.projectId}/runs/agent-sessions/${session.id}`,
  }))

  return [...cicdItems, ...agentItems].sort(
    (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime()
  )
})

onMounted(async () => {
  await Promise.all([
    store.fetchRuns(),
    store.fetchDashboardSessions(),
    projectsStore.fetchProjects(),
    orgsStore.fetchOrgs(),
  ])
})

async function cancelRun(runId: string) {
  await store.cancelRun(runId)
  await store.fetchRuns()
}

async function retryRun(runId: string) {
  await store.retryRun(runId)
  await store.fetchRuns()
}

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

function statusClass(status: CiCdRunStatus | AgentSessionStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function statusDot(status: CiCdRunStatus | AgentSessionStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}
</script>
