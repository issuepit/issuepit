<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6 shrink-0">
      <div class="flex items-center gap-2">
        <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <h1 class="text-xl font-bold text-white">Kanban Board</h1>
      </div>
      <div class="flex items-center gap-2 text-xs text-gray-500">
        <span>{{ totalIssues }} issues</span>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center flex-1">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Board -->
    <div v-else class="flex gap-4 overflow-x-auto flex-1 pb-4">
      <div v-for="col in columns" :key="col.status"
        class="flex flex-col w-72 shrink-0">
        <!-- Column Header -->
        <div class="flex items-center justify-between mb-3">
          <div class="flex items-center gap-2">
            <span :class="col.dotColor" class="w-2.5 h-2.5 rounded-full"></span>
            <h3 class="text-sm font-semibold text-gray-300">{{ col.title }}</h3>
            <span class="text-xs text-gray-600 bg-gray-800 px-1.5 py-0.5 rounded-full">
              {{ issuesByStatus[col.status]?.length ?? 0 }}
            </span>
          </div>
          <button @click="openCreateForStatus(col.status)"
            class="text-gray-600 hover:text-gray-400 transition-colors p-0.5 rounded">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
          </button>
        </div>

        <!-- Cards -->
        <div class="flex-1 space-y-2 bg-gray-900/40 rounded-xl p-2 min-h-32 border border-gray-800/60"
          @dragover.prevent @drop="onDrop($event, col.status)">
          <div v-for="issue in issuesByStatus[col.status]" :key="issue.id"
            draggable="true"
            @dragstart="onDragStart($event, issue.id)"
            class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-lg p-3 cursor-pointer group transition-all hover:shadow-lg hover:-translate-y-0.5"
            @click="$router.push(`/projects/${id}/issues/${issue.id}`)">
            <div class="flex items-start justify-between gap-2 mb-2">
              <span class="text-xs text-gray-600">#{{ issue.number }}</span>
              <span :class="priorityColor(issue.priority)" class="text-xs shrink-0">
                {{ priorityIcon(issue.priority) }}
              </span>
            </div>
            <p class="text-sm text-gray-200 leading-snug mb-3 group-hover:text-white transition-colors">
              {{ issue.title }}
            </p>
            <div class="flex items-center justify-between">
              <span :class="typeBadge(issue.type)"
                class="text-xs px-1.5 py-0.5 rounded font-medium capitalize">
                {{ issue.type }}
              </span>
              <span v-if="issue.estimate" class="text-xs text-gray-600">{{ issue.estimate }}pt</span>
            </div>
          </div>

          <!-- Empty placeholder -->
          <div v-if="!issuesByStatus[col.status]?.length"
            class="flex items-center justify-center h-16 text-gray-700 text-xs">
            Drop issues here
          </div>
        </div>
      </div>
    </div>

    <!-- Quick Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">
          Add to {{ columns.find(c => c.status === createStatus)?.title }}
        </h2>
        <div class="space-y-4">
          <div>
            <input v-model="createTitle" type="text" placeholder="Issue title..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
              @keyup.enter="submitCreate" />
          </div>
          <div>
            <select v-model="createPriority"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option value="no_priority">⚪ No Priority</option>
              <option value="urgent">🔴 Urgent</option>
              <option value="high">🟠 High</option>
              <option value="medium">🟡 Medium</option>
              <option value="low">🔵 Low</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create
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

const route = useRoute()
const id = route.params.id as string
const store = useIssuesStore()

const showCreate = ref(false)
const createTitle = ref('')
const createPriority = ref<IssuePriority>(IssuePriority.NoPriority)
const createStatus = ref<IssueStatus>(IssueStatus.Backlog)
const draggedId = ref<string | null>(null)

const columns = [
  { status: IssueStatus.Backlog, title: 'Backlog', dotColor: 'bg-gray-500' },
  { status: IssueStatus.Todo, title: 'Todo', dotColor: 'bg-blue-400' },
  { status: IssueStatus.InProgress, title: 'In Progress', dotColor: 'bg-yellow-400' },
  { status: IssueStatus.InReview, title: 'In Review', dotColor: 'bg-purple-400' },
  { status: IssueStatus.Done, title: 'Done', dotColor: 'bg-green-400' }
]

const issuesByStatus = computed(() => store.issuesByStatus)
const totalIssues = computed(() => store.issues.length)

onMounted(() => store.fetchIssues(id))

function onDragStart(e: DragEvent, issueId: string) {
  draggedId.value = issueId
  e.dataTransfer!.effectAllowed = 'move'
}

async function onDrop(e: DragEvent, status: IssueStatus) {
  e.preventDefault()
  if (!draggedId.value) return
  await store.updateIssueStatus(id, draggedId.value, status)
  draggedId.value = null
}

function openCreateForStatus(status: IssueStatus) {
  createStatus.value = status
  createTitle.value = ''
  createPriority.value = IssuePriority.NoPriority
  showCreate.value = true
}

async function submitCreate() {
  if (!createTitle.value) return
  await store.createIssue(id, {
    title: createTitle.value,
    status: createStatus.value,
    priority: createPriority.value,
    type: IssueType.Issue
  })
  showCreate.value = false
}

function priorityIcon(p: IssuePriority) {
  const map: Record<IssuePriority, string> = {
    [IssuePriority.Urgent]: '🔴',
    [IssuePriority.High]: '🟠',
    [IssuePriority.Medium]: '🟡',
    [IssuePriority.Low]: '🔵',
    [IssuePriority.NoPriority]: ''
  }
  return map[p] ?? ''
}

function priorityColor(p: IssuePriority) {
  return p === IssuePriority.Urgent ? 'text-red-400' : 'text-gray-500'
}

function typeBadge(type: IssueType) {
  const map: Record<IssueType, string> = {
    [IssueType.Bug]: 'bg-red-900/40 text-red-300',
    [IssueType.Feature]: 'bg-green-900/40 text-green-300',
    [IssueType.Epic]: 'bg-purple-900/40 text-purple-300',
    [IssueType.Task]: 'bg-blue-900/40 text-blue-300',
    [IssueType.Issue]: 'bg-gray-800 text-gray-400'
  }
  return map[type] ?? 'bg-gray-800 text-gray-400'
}
</script>
