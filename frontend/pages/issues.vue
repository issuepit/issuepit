<template>
  <div class="p-8">
    <div class="flex items-center justify-between mb-6">
<<<<<<< HEAD
      <div>
        <h1 class="text-2xl font-bold text-white">{{ filterTitle }}</h1>
        <p class="text-gray-400 mt-1 text-sm">{{ filterDescription }}</p>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"/>
=======
      <h1 class="text-2xl font-bold text-white">
        Issues
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full ml-2 font-normal">
          {{ filteredIssues.length }}
        </span>
      </h1>
    </div>

    <!-- Filters -->
    <div class="flex flex-wrap items-center gap-2 mb-5">
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
>>>>>>> 71675ebf9a147fe8bc8ac33c63d5271af8b214a2
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

<<<<<<< HEAD
    <!-- Issue list -->
    <div v-if="!store.loading" class="space-y-2">
      <NuxtLink
        v-for="issue in store.issues"
        :key="issue.id"
        :to="`/projects/${issue.projectId}/issues/${issue.id}`"
        class="flex items-center gap-3 bg-gray-900 border border-gray-800 rounded-lg px-4 py-3 hover:border-gray-700 transition-colors group"
      >
        <!-- Status dot -->
        <span
          class="w-2.5 h-2.5 rounded-full shrink-0"
          :class="statusColor(issue.status)"
        />
        <!-- Title -->
        <span class="flex-1 text-sm text-gray-200 group-hover:text-white truncate">{{ issue.title }}</span>
        <!-- Priority badge -->
        <span class="text-xs px-1.5 py-0.5 rounded" :class="priorityClass(issue.priority)">
          {{ priorityLabel(issue.priority) }}
        </span>
        <!-- Assignees -->
        <div class="flex -space-x-1">
          <div
            v-for="assignee in issue.assignees?.slice(0, 3)"
            :key="assignee.id"
            class="w-5 h-5 rounded-full bg-brand-700 border border-gray-900 flex items-center justify-center text-[10px] font-bold text-white"
            :title="assignee.user?.username ?? assignee.agent?.name ?? 'Unknown'"
          >
            {{ (assignee.user?.username ?? assignee.agent?.name ?? '?').charAt(0).toUpperCase() }}
          </div>
        </div>
      </NuxtLink>

      <!-- Empty state -->
      <div v-if="store.issues.length === 0 && !store.loading" class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
        <p class="text-gray-400">No issues found for this filter.</p>
      </div>
=======
    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Issues Table -->
    <div v-else class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
      <div v-if="filteredIssues.length === 0" class="py-16 text-center">
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
          <tr v-for="issue in filteredIssues" :key="issue.id"
            class="border-b border-gray-800/50 hover:bg-gray-800/40 cursor-pointer transition-colors"
            @click="$router.push(`/projects/${issue.projectId}/issues/${issue.id}`)">
            <td class="px-4 py-3">
              <span :class="statusDotColor(issue.status)" class="w-3.5 h-3.5 rounded-full block"></span>
            </td>
            <td class="px-4 py-3">
              <div class="flex items-center gap-2">
                <span class="text-xs text-gray-600">#{{ issue.number }}</span>
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
>>>>>>> 71675ebf9a147fe8bc8ac33c63d5271af8b214a2
    </div>
  </div>
</template>

<script setup lang="ts">
<<<<<<< HEAD
import { useIssuesStore } from '~/stores/issues'
import { IssueStatus, IssuePriority } from '~/types'

const route = useRoute()
const store = useIssuesStore()

type FeedFilter = 'my' | 'open' | 'unassigned' | 'waiting'

const filter = computed<FeedFilter>(() => {
  const f = route.query.filter as string
  if (f === 'open' || f === 'unassigned' || f === 'waiting') return f
  return 'my'
})

const filterTitle = computed(() => {
  switch (filter.value) {
    case 'open': return 'Open Issues'
    case 'unassigned': return 'Unassigned Issues'
    case 'waiting': return 'Waiting for Human'
    default: return 'My Issues'
  }
})

const filterDescription = computed(() => {
  switch (filter.value) {
    case 'open': return 'All open issues across your projects'
    case 'unassigned': return 'Issues with no assignees'
    case 'waiting': return 'Issues assigned to an agent waiting for human input'
    default: return 'Issues assigned to you across all projects'
  }
})

watch(filter, (val) => {
  store.fetchFeed(val)
}, { immediate: true })

function statusColor(status: IssueStatus) {
  switch (status) {
    case IssueStatus.Backlog: return 'bg-gray-500'
    case IssueStatus.Todo: return 'bg-blue-500'
    case IssueStatus.InProgress: return 'bg-yellow-500'
    case IssueStatus.InReview: return 'bg-purple-500'
    case IssueStatus.Done: return 'bg-green-500'
    case IssueStatus.Cancelled: return 'bg-red-400'
    default: return 'bg-gray-500'
  }
}

function priorityLabel(priority: IssuePriority) {
  switch (priority) {
    case IssuePriority.Urgent: return 'Urgent'
    case IssuePriority.High: return 'High'
    case IssuePriority.Medium: return 'Medium'
    case IssuePriority.Low: return 'Low'
    default: return ''
  }
}

function priorityClass(priority: IssuePriority) {
  switch (priority) {
    case IssuePriority.Urgent: return 'bg-red-900/50 text-red-400'
    case IssuePriority.High: return 'bg-orange-900/50 text-orange-400'
    case IssuePriority.Medium: return 'bg-yellow-900/50 text-yellow-400'
    case IssuePriority.Low: return 'bg-blue-900/50 text-blue-400'
    default: return 'hidden'
  }
}
</script>

=======
import { IssueStatus, IssuePriority } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useProjectsStore } from '~/stores/projects'

const store = useIssuesStore()
const projectsStore = useProjectsStore()
const route = useRoute()
const router = useRouter()

// Initialise filters from query params
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

// Apply filters to the store whenever they change
watch([search, filterStatus], () => {
  if (filterStatus.value === OPEN_FILTER) {
    // "Open" means everything except Done and Cancelled — we filter client-side below
    store.setFilters({ search: search.value || undefined })
  } else {
    store.setFilters({
      search: search.value || undefined,
      status: (filterStatus.value as IssueStatus) || undefined
    })
  }
})

// Override filteredIssues for the "open" pseudo-filter
const filteredIssues = computed(() => {
  if (filterStatus.value === OPEN_FILTER) {
    return store.filteredIssues.filter(
      i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled
    )
  }
  return store.filteredIssues
})

onMounted(async () => {
  const statusParam = route.query.status as string | undefined
  if (statusParam) {
    filterStatus.value = statusParam as IssueStatus | 'open'
  }
  await Promise.allSettled([
    store.fetchIssues(),
    projectsStore.fetchProjects()
  ])
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

function priorityLabel(priority: IssuePriority) {
  const map: Record<IssuePriority, string> = {
    [IssuePriority.Urgent]: '🔴 Urgent',
    [IssuePriority.High]: '🟠 High',
    [IssuePriority.Medium]: '🟡 Medium',
    [IssuePriority.Low]: '🔵 Low',
    [IssuePriority.NoPriority]: '⚪ None'
  }
  return map[priority] ?? priority
}
</script>
>>>>>>> 71675ebf9a147fe8bc8ac33c63d5271af8b214a2
