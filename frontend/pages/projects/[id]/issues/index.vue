<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-2">
        <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <h1 class="text-xl font-bold text-white">Issues</h1>
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full">
          {{ store.filteredIssues.length }}
        </span>
      </div>
      <button @click="showCreate = true"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Issue
      </button>
    </div>

    <!-- Filters -->
    <div class="flex flex-wrap items-center gap-2 mb-5">
      <input v-model="search" type="text" placeholder="Search issues..."
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 w-56" />

      <select v-model="filterStatus"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Status</option>
        <option v-for="s in statuses" :key="s.value" :value="s.value">{{ s.label }}</option>
      </select>

      <select v-model="filterPriority"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Priority</option>
        <option v-for="p in priorities" :key="p.value" :value="p.value">{{ p.label }}</option>
      </select>

      <select v-model="filterType"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Types</option>
        <option v-for="t in types" :key="t.value" :value="t.value">{{ t.label }}</option>
      </select>

      <select v-model="filterMilestone"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Milestones</option>
        <option v-for="m in milestonesStore.milestones" :key="m.id" :value="m.id">{{ m.title }}</option>
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
      <div v-if="store.filteredIssues.length === 0" class="py-16 text-center">
        <p class="text-gray-400">No issues found</p>
        <button @click="showCreate = true" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
          Create the first issue →
        </button>
      </div>

      <table v-else class="w-full">
        <thead>
          <tr class="border-b border-gray-800">
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 w-8"></th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3">Title</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden md:table-cell">Status</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden lg:table-cell">Priority</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden lg:table-cell">Type</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden xl:table-cell">Updated</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="issue in store.filteredIssues" :key="issue.id"
            class="border-b border-gray-800/50 hover:bg-gray-800/40 cursor-pointer transition-colors"
            @click="$router.push(`/projects/${id}/issues/${issue.id}`)">
            <td class="px-4 py-3">
              <span :class="statusIcon(issue.status).color" class="w-3.5 h-3.5 rounded-full block"></span>
            </td>
            <td class="px-4 py-3">
              <div class="flex items-center gap-2">
                <span class="text-xs text-gray-600">#{{ issue.number }}</span>
                <span class="text-sm text-gray-200 hover:text-white">{{ issue.title }}</span>
              </div>
            </td>
            <td class="px-4 py-3 hidden md:table-cell">
              <StatusBadge :status="issue.status" />
            </td>
            <td class="px-4 py-3 hidden lg:table-cell">
              <PriorityBadge :priority="issue.priority" />
            </td>
            <td class="px-4 py-3 hidden lg:table-cell">
              <span class="text-xs text-gray-400 capitalize">{{ issue.type }}</span>
            </td>
            <td class="px-4 py-3 hidden xl:table-cell">
              <span class="text-xs text-gray-500">{{ formatDate(issue.updatedAt) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="form.title" type="text" placeholder="Issue title"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="form.body" rows="4" placeholder="Describe the issue..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Status</label>
              <select v-model="form.status"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="s in statuses" :key="s.value" :value="s.value">{{ s.label }}</option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Priority</label>
              <select v-model="form.priority"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="p in priorities" :key="p.value" :value="p.value">{{ p.label }}</option>
              </select>
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Type</label>
            <select v-model="form.type"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="t in types" :key="t.value" :value="t.value">{{ t.label }}</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="showCreate = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus, IssuePriority, IssueType } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useMilestonesStore } from '~/stores/milestones'

const route = useRoute()
const id = route.params.id as string
const store = useIssuesStore()
const milestonesStore = useMilestonesStore()

const showCreate = ref(false)
const search = ref('')
const filterStatus = ref<IssueStatus | ''>('')
const filterPriority = ref<IssuePriority | ''>('')
const filterType = ref<IssueType | ''>('')
const filterMilestone = ref<string>('')

const form = reactive({
  title: '',
  body: '',
  status: IssueStatus.Todo,
  priority: IssuePriority.Medium,
  type: IssueType.Issue
})

const statuses = [
  { value: IssueStatus.Backlog, label: 'Backlog' },
  { value: IssueStatus.Todo, label: 'Todo' },
  { value: IssueStatus.InProgress, label: 'In Progress' },
  { value: IssueStatus.InReview, label: 'In Review' },
  { value: IssueStatus.Done, label: 'Done' },
  { value: IssueStatus.Cancelled, label: 'Cancelled' }
]

const priorities = [
  { value: IssuePriority.Urgent, label: '🔴 Urgent' },
  { value: IssuePriority.High, label: '🟠 High' },
  { value: IssuePriority.Medium, label: '🟡 Medium' },
  { value: IssuePriority.Low, label: '🔵 Low' },
  { value: IssuePriority.NoPriority, label: '⚪ No Priority' }
]

const types = [
  { value: IssueType.Issue, label: '📋 Issue' },
  { value: IssueType.Bug, label: '🐛 Bug' },
  { value: IssueType.Feature, label: '✨ Feature' },
  { value: IssueType.Task, label: '✅ Task' },
  { value: IssueType.Epic, label: '⚡ Epic' }
]

const hasFilters = computed(() => search.value || filterStatus.value || filterPriority.value || filterType.value || filterMilestone.value)

watch([search, filterStatus, filterPriority, filterType, filterMilestone], () => {
  store.setFilters({
    search: search.value || undefined,
    status: filterStatus.value || undefined,
    priority: filterPriority.value || undefined,
    type: filterType.value || undefined,
    milestoneId: filterMilestone.value || undefined,
  })
})

onMounted(() => {
  store.fetchIssues(id)
  milestonesStore.fetchMilestones(id)
})

function clearFilters() {
  search.value = ''
  filterStatus.value = ''
  filterPriority.value = ''
  filterType.value = ''
  filterMilestone.value = ''
  store.clearFilters()
}

async function submitCreate() {
  if (!form.title) return
  await store.createIssue(id, form)
  showCreate.value = false
  Object.assign(form, { title: '', body: '', status: IssueStatus.Todo, priority: IssuePriority.Medium, type: IssueType.Issue })
}

function statusIcon(status: IssueStatus) {
  const map: Record<IssueStatus, { color: string }> = {
    [IssueStatus.Backlog]: { color: 'bg-gray-500' },
    [IssueStatus.Todo]: { color: 'bg-blue-400' },
    [IssueStatus.InProgress]: { color: 'bg-yellow-400' },
    [IssueStatus.InReview]: { color: 'bg-purple-400' },
    [IssueStatus.Done]: { color: 'bg-green-400' },
    [IssueStatus.Cancelled]: { color: 'bg-red-400' }
  }
  return map[status] ?? { color: 'bg-gray-500' }
}

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

// Inline badge components
const StatusBadge = defineComponent({
  props: { status: String },
  setup(props) {
    const map: Record<string, string> = {
      backlog: 'bg-gray-800 text-gray-400',
      todo: 'bg-blue-900/40 text-blue-300',
      in_progress: 'bg-yellow-900/40 text-yellow-300',
      in_review: 'bg-purple-900/40 text-purple-300',
      done: 'bg-green-900/40 text-green-300',
      cancelled: 'bg-red-900/40 text-red-400'
    }
    const labels: Record<string, string> = {
      backlog: 'Backlog', todo: 'Todo', in_progress: 'In Progress',
      in_review: 'In Review', done: 'Done', cancelled: 'Cancelled'
    }
    return () => h('span', {
      class: `text-xs px-2 py-0.5 rounded-full font-medium ${map[props.status!] ?? 'bg-gray-800 text-gray-400'}`
    }, labels[props.status!] ?? props.status)
  }
})

const PriorityBadge = defineComponent({
  props: { priority: String },
  setup(props) {
    const icons: Record<string, string> = {
      urgent: '🔴', high: '🟠', medium: '🟡', low: '🔵', no_priority: '⚪'
    }
    const labels: Record<string, string> = {
      urgent: 'Urgent', high: 'High', medium: 'Medium', low: 'Low', no_priority: 'None'
    }
    return () => h('span', { class: 'text-xs text-gray-400' },
      `${icons[props.priority!] ?? ''} ${labels[props.priority!] ?? props.priority}`
    )
  }
})
</script>
