<template>
  <div class="p-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <div class="flex items-center gap-3">
          <PageBreadcrumb :items="[
            { label: feedFilter ? filterTitle : 'Issues', to: feedFilter ? `/issues?filter=${feedFilter}` : '/issues', icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2' },
          ]" />
          <span v-if="!feedFilter" class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full font-normal">
            {{ filteredIssues.length }}
          </span>
        </div>
        <p v-if="feedFilter" class="text-gray-400 mt-1 text-sm">{{ filterDescription }}</p>
      </div>
    </div>

    <!-- Filters (only for non-feed mode) -->
    <div v-if="!feedFilter" class="flex flex-wrap items-center gap-2 mb-5">
      <input v-model="search" type="text" placeholder="Search issues..."
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 w-56" />

      <select v-model="filterStatus"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Status</option>
        <option value="open">Open (non-closed)</option>
        <option v-for="s in statuses" :key="s.value" :value="s.value">{{ s.label }}</option>
      </select>

      <button v-if="hasFilters" @click="clearFilters"
        class="text-xs text-gray-400 hover:text-gray-200 px-2 py-1.5">Clear</button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Issues Table -->
    <div v-else class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
      <div v-if="displayedIssues.length === 0" class="py-16 text-center">
        <p class="text-gray-400">No issues found</p>
        <NuxtLink to="/projects" class="mt-3 inline-block text-brand-400 hover:text-brand-300 text-sm">
          Browse Projects →
        </NuxtLink>
      </div>

      <table v-else class="w-full">
        <thead>
          <tr class="border-b border-gray-800">
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 w-8"></th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3">Title</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden md:table-cell">Project</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden md:table-cell">Status</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden lg:table-cell">Priority</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="issue in displayedIssues" :key="issue.id"
            class="border-b border-gray-800/50 hover:bg-gray-800/40 cursor-pointer transition-colors"
            @click="$router.push(`/projects/${issue.projectId}/issues/${issue.number}`)">
            <td class="px-4 py-3">
              <span :class="statusDotColor(issue.status)" class="w-3.5 h-3.5 rounded-full block"></span>
            </td>
            <td class="px-4 py-3">
              <div class="flex items-center gap-2">
                <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, projectsStore.projects.find(p => p.id === issue.projectId)) }}</span>
                <span class="text-sm text-gray-200 hover:text-white">{{ issue.title }}</span>
              </div>
            </td>
            <td class="px-4 py-3 hidden md:table-cell">
              <NuxtLink :to="`/projects/${issue.projectId}`" class="text-xs text-gray-400 hover:text-brand-300 transition-colors" @click.stop>
                {{ projectName(issue.projectId) }}
              </NuxtLink>
            </td>
            <td class="px-4 py-3 hidden md:table-cell">
              <span :class="statusBadgeClass(issue.status)" class="text-xs px-2 py-0.5 rounded-full font-medium">
                {{ statusLabel(issue.status) }}
              </span>
            </td>
            <td class="px-4 py-3 hidden lg:table-cell">
              <span class="text-xs text-gray-400">{{ priorityLabel(issue.priority) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus } from '~/types'
import type { IssuePriority } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useProjectsStore } from '~/stores/projects'
import { formatIssueId } from '~/composables/useIssueFormat'

const store = useIssuesStore()
const projectsStore = useProjectsStore()
const route = useRoute()
const router = useRouter()

type FeedFilter = 'my' | 'open' | 'unassigned' | 'waiting'

// Feed filter from sidebar navigation (?filter=)
const feedFilter = computed<FeedFilter | null>(() => {
  const f = route.query.filter as string
  if (f === 'my' || f === 'open' || f === 'unassigned' || f === 'waiting') return f
  return null
})

const filterTitle = computed(() => {
  switch (feedFilter.value) {
    case 'open': return 'Open Issues'
    case 'unassigned': return 'Unassigned Issues'
    case 'waiting': return 'Waiting for Human'
    case 'my': return 'My Issues'
    default: return 'Issues'
  }
})

const filterDescription = computed(() => {
  switch (feedFilter.value) {
    case 'open': return 'All open issues across your projects'
    case 'unassigned': return 'Issues with no assignees'
    case 'waiting': return 'Issues assigned to an agent waiting for human input'
    case 'my': return 'Issues assigned to you across all projects'
    default: return ''
  }
})

// Search/status filters for non-feed mode
const OPEN_FILTER = 'open'
const search = ref('')
const filterStatus = ref<IssueStatus | 'open' | ''>('')

const statuses = [
  { value: IssueStatus.Backlog, label: 'Backlog' },
  { value: IssueStatus.Todo, label: 'Todo' },
  { value: IssueStatus.InProgress, label: 'In Progress' },
  { value: IssueStatus.InReview, label: 'In Review' },
  { value: IssueStatus.Done, label: 'Done' },
  { value: IssueStatus.Cancelled, label: 'Cancelled' }
]

const hasFilters = computed(() => search.value || filterStatus.value)

watch([search, filterStatus], () => {
  if (filterStatus.value === OPEN_FILTER) {
    store.setFilters({ search: search.value || undefined })
  } else {
    store.setFilters({
      search: search.value || undefined,
      status: (filterStatus.value as IssueStatus) || undefined
    })
  }
})

// Issues to display — feed issues (server-side) or filtered store issues (client-side)
const displayedIssues = computed(() => {
  if (feedFilter.value) return store.issues
  if (filterStatus.value === OPEN_FILTER) {
    return store.filteredIssues.filter(
      i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled
    )
  }
  return store.filteredIssues
})

const filteredIssues = computed(() => store.filteredIssues)

// When sidebar feed filter changes, reload from server
watch(feedFilter, async (val) => {
  if (val) await store.fetchFeed(val)
}, { immediate: true })

onMounted(async () => {
  const statusParam = route.query.status as string | undefined
  if (statusParam) {
    filterStatus.value = statusParam as IssueStatus | 'open'
  }
  if (!feedFilter.value) {
    await Promise.allSettled([
      store.fetchIssues(),
      projectsStore.fetchProjects()
    ])
  } else {
    await projectsStore.fetchProjects()
  }
})

function clearFilters() {
  search.value = ''
  filterStatus.value = ''
  store.clearFilters()
  router.replace({ query: {} })
}

function projectName(projectId: string) {
  return projectsStore.projects.find(p => p.id === projectId)?.name ?? projectId
}

function statusDotColor(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400'
  }
  return map[status] ?? 'bg-gray-500'
}

function statusBadgeClass(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-800 text-gray-400',
    [IssueStatus.Todo]: 'bg-blue-900/40 text-blue-300',
    [IssueStatus.InProgress]: 'bg-yellow-900/40 text-yellow-300',
    [IssueStatus.InReview]: 'bg-purple-900/40 text-purple-300',
    [IssueStatus.Done]: 'bg-green-900/40 text-green-300',
    [IssueStatus.Cancelled]: 'bg-red-900/40 text-red-400'
  }
  return map[status] ?? 'bg-gray-800 text-gray-400'
}

function statusLabel(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'Backlog',
    [IssueStatus.Todo]: 'Todo',
    [IssueStatus.InProgress]: 'In Progress',
    [IssueStatus.InReview]: 'In Review',
    [IssueStatus.Done]: 'Done',
    [IssueStatus.Cancelled]: 'Cancelled'
  }
  return map[status] ?? status
}

const { priorityIcon, priorityLabel: priorityText } = usePriority()
function priorityLabel(priority: IssuePriority) {
  return `${priorityIcon(priority)} ${priorityText(priority)}`
}
</script>
