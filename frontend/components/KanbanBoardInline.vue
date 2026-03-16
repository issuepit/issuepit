<template>
  <div>
    <!-- Loading -->
    <div v-if="issueStore.loading || kanban.loading" class="flex items-center justify-center py-8">
      <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else>
      <!-- Board header: board selector + manage link -->
      <div class="flex items-center justify-between mb-4">
        <div class="flex items-center gap-2">
          <select v-if="kanban.boards.length > 1 && props.boardId === undefined" v-model="activeBoardId"
            class="bg-gray-800 border border-gray-700 rounded-lg px-2 py-1 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
            <option v-for="b in kanban.boards" :key="b.id" :value="b.id">{{ b.name }}</option>
          </select>
          <span v-else-if="activeBoard" class="text-xs text-gray-500">{{ activeBoard.name }}</span>
          <span class="text-xs text-gray-600">{{ totalIssues }} issues</span>
        </div>
        <NuxtLink :to="`/projects/${projectId}/kanban`"
          class="text-xs text-brand-400 hover:text-brand-300 transition-colors flex items-center gap-1">
          Full board →
        </NuxtLink>
      </div>

      <!-- Board columns -->
      <div class="flex gap-3 overflow-x-auto pb-3">
        <div v-for="col in boardColumns" :key="col.id"
          class="flex flex-col w-56 shrink-0 transition-opacity duration-150"
          :class="{
            'opacity-50': draggedColId === col.id,
            'opacity-40': draggedId && !draggedColId && !isValidDropTarget(col.id),
          }"
          @dragover.prevent
          @drop="onIssueDrop($event, col)">
          <!-- Column Header -->
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center gap-2">
              <span :class="statusDotColor(col.issueStatus)" class="w-2 h-2 rounded-full shrink-0"></span>
              <h3 class="text-xs font-semibold text-gray-400">{{ col.name }}</h3>
              <span class="text-xs text-gray-600 bg-gray-800 px-1.5 py-0.5 rounded-full">
                {{ issuesByLane[col.id]?.length ?? 0 }}
              </span>
            </div>
            <button @click.stop="openCreateForStatus(col.issueStatus)"
              class="text-gray-600 hover:text-gray-400 transition-colors p-0.5 rounded"
              title="Add issue to this column">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
              </svg>
            </button>
          </div>

          <!-- Cards -->
          <div class="flex-1 space-y-1.5 bg-gray-900/40 rounded-lg p-1.5 min-h-24 border border-gray-800/60"
            :class="{
              'border-brand-500/60 bg-brand-900/10': isValidDropTarget(col.id) && draggedId,
              'border-gray-700/30 bg-gray-900/20': draggedId && !draggedColId && !isValidDropTarget(col.id),
            }"
            @dragover.prevent="onIssueDragOver($event, col.id)"
            @dragleave="onIssueDragLeave"
            @drop="onIssueDrop($event, col)">
            <template v-for="(issue, idx) in visibleIssues(col.id)" :key="issue.id">
              <div v-if="draggedId && !draggedColId && isValidDropTarget(col.id) && dragHoverColId === col.id && dragHoverInsertIdx === idx"
                role="status" aria-label="Drop zone"
                class="rounded border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-10 animate-pulse">
              </div>
              <div
                :class="[
                  'bg-gray-900 border border-gray-800 hover:border-gray-700 rounded p-2 cursor-pointer group transition-all hover:shadow-md hover:-translate-y-0.5',
                  issue.id === draggedId ? 'invisible h-10' : '',
                ]"
                draggable="true"
                @dragstart="onDragStart($event, issue)"
                @dragend="onIssueDragEnd"
                @click="$router.push(`/projects/${projectId}/issues/${issue.number}`)">
                <div class="flex items-start justify-between gap-1 mb-1">
                  <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, projectsStore.currentProject) }}</span>
                  <span :class="priorityColor(issue.priority)" class="text-xs shrink-0">{{ priorityIcon(issue.priority) }}</span>
                </div>
                <p class="text-xs text-gray-200 leading-snug group-hover:text-white transition-colors line-clamp-2">
                  {{ issue.title }}
                </p>
              </div>
            </template>

            <!-- Drop zone at end -->
            <div v-if="draggedId && !draggedColId && isValidDropTarget(col.id) && dragHoverColId === col.id && dragHoverInsertIdx >= (issuesByLane[col.id]?.length ?? 0)"
              role="status" aria-label="Drop zone"
              class="rounded border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-10 animate-pulse">
            </div>

            <div v-if="!issuesByLane[col.id]?.length && !(draggedId && !draggedColId && isValidDropTarget(col.id) && dragHoverColId === col.id)"
              class="flex items-center justify-center h-12 text-gray-700 text-xs">
              Drop here
            </div>

            <!-- Truncation footer: show more link -->
            <NuxtLink v-if="props.maxItems && (issuesByLane[col.id]?.length ?? 0) > props.maxItems"
              :to="`/projects/${projectId}/kanban`"
              class="block text-center text-xs text-gray-600 hover:text-brand-400 transition-colors mt-1 py-0.5">
              {{ (issuesByLane[col.id]?.length ?? 0) - props.maxItems }} more — open board
            </NuxtLink>
          </div>
        </div>

        <!-- No board / no columns -->
        <div v-if="!activeBoard" class="flex items-center justify-center flex-1 py-8 text-gray-600 text-sm">
          No boards yet.
          <NuxtLink :to="`/projects/${projectId}/kanban`" class="text-brand-400 hover:text-brand-300 ml-1">Create one →</NuxtLink>
        </div>
        <div v-else-if="!boardColumns.length" class="flex items-center justify-center flex-1 py-8 text-gray-600 text-sm">
          No lanes yet.
          <NuxtLink :to="`/projects/${projectId}/kanban`" class="text-brand-400 hover:text-brand-300 ml-1">Add lanes →</NuxtLink>
        </div>
      </div>
    </template>

    <!-- Quick Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">
          Add to {{ boardColumns.find(c => c.issueStatus === createStatus)?.name }}
        </h2>
        <input v-model="createTitle" type="text" placeholder="Issue title..."
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
          @keyup.enter="submitCreate" />
        <div class="flex gap-3 mt-4">
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
import type { Issue, KanbanColumn } from '~/types'
import { IssueStatus, IssuePriority, IssueType, KanbanLaneProperty } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useKanbanStore } from '~/stores/kanban'
import { useProjectsStore } from '~/stores/projects'
import { formatIssueId } from '~/composables/useIssueFormat'

const props = defineProps<{ projectId: string; boardId?: string | null; maxItems?: number | null }>()

const issueStore = useIssuesStore()
const kanban = useKanbanStore()
const projectsStore = useProjectsStore()
const { priorityIcon, priorityColor } = usePriority()

const activeBoardId = ref('')
const activeBoard = computed(() => kanban.boards.find(b => b.id === activeBoardId.value) ?? null)
const boardColumns = computed(() =>
  (activeBoard.value?.columns ?? []).slice().sort((a, b) => a.position - b.position)
)

// ── Lane-property-aware issue grouping ────────────────────────────────────
const issuesByLane = computed<Record<string, Issue[]>>(() => {
  const result: Record<string, Issue[]> = {}
  for (const col of boardColumns.value) {
    result[col.id] = []
  }
  const lp = activeBoard.value?.laneProperty ?? KanbanLaneProperty.Status
  for (const issue of issueStore.issues) {
    switch (lp) {
      case KanbanLaneProperty.Status: {
        const col = boardColumns.value.find(c => c.issueStatus === issue.status)
        if (col) result[col.id].push(issue)
        break
      }
      case KanbanLaneProperty.Priority: {
        const col = boardColumns.value.find(c => c.laneValue === issue.priority)
        if (col) result[col.id].push(issue)
        break
      }
      case KanbanLaneProperty.Type: {
        const col = boardColumns.value.find(c => c.laneValue === issue.type)
        if (col) result[col.id].push(issue)
        break
      }
      case KanbanLaneProperty.Label: {
        if (!issue.labels?.length) {
          const col = boardColumns.value.find(c => c.laneValue === '')
          if (col) result[col.id].push(issue)
        } else {
          for (const label of issue.labels) {
            const col = boardColumns.value.find(c => c.laneValue === label.id)
            if (col) result[col.id].push(issue)
          }
        }
        break
      }
      case KanbanLaneProperty.Agent: {
        const agentAssignees = issue.assignees?.filter(a => a.agentId) ?? []
        if (!agentAssignees.length) {
          const col = boardColumns.value.find(c => c.laneValue === '')
          if (col) result[col.id].push(issue)
        } else {
          for (const a of agentAssignees) {
            const col = boardColumns.value.find(c => c.laneValue === a.agentId)
            if (col) result[col.id].push(issue)
          }
        }
        break
      }
      case KanbanLaneProperty.Milestone: {
        const mId = issue.milestoneId ?? ''
        const col = boardColumns.value.find(c => c.laneValue === mId)
        if (col) result[col.id].push(issue)
        break
      }
    }
  }
  for (const colId of Object.keys(result)) {
    result[colId].sort((a, b) => a.kanbanRank - b.kanbanRank || a.createdAt.localeCompare(b.createdAt))
  }
  return result
})

const totalIssues = computed(() => issueStore.issues.length)

// ── Visible issues (truncated by maxItems) ────────────────────────────────
function visibleIssues(colId: string): Issue[] {
  const all = issuesByLane.value[colId] ?? []
  return props.maxItems ? all.slice(0, props.maxItems) : all
}

// ── Issue drag state ──────────────────────────────────────────────────────
const draggedId = ref<string | null>(null)
const draggedSourceColId = ref<string | null>(null)
const dragHoverColId = ref<string | null>(null)
const dragHoverInsertIdx = ref<number>(0)
const draggedColId = ref<string | null>(null)

// ── Quick create ──────────────────────────────────────────────────────────
const showCreate = ref(false)
const createTitle = ref('')
const createStatus = ref<IssueStatus>(IssueStatus.Backlog)

function openCreateForStatus(status: IssueStatus) {
  createStatus.value = status
  createTitle.value = ''
  showCreate.value = true
}

async function submitCreate() {
  if (!createTitle.value) return
  await issueStore.createIssue(props.projectId, {
    title: createTitle.value,
    status: createStatus.value,
    priority: IssuePriority.NoPriority,
    type: IssueType.Issue,
  })
  showCreate.value = false
  createTitle.value = ''
}

// ── Drag & drop ───────────────────────────────────────────────────────────
function isValidDropTarget(targetColId: string): boolean {
  if (!draggedId.value || !draggedSourceColId.value) return false
  if (draggedSourceColId.value === targetColId) return true
  if (kanban.transitions.length === 0) return true
  return kanban.transitions.some(t => t.fromColumnId === draggedSourceColId.value && t.toColumnId === targetColId)
}

function onDragStart(e: DragEvent, issue: Issue) {
  draggedId.value = issue.id
  // Find the source column based on the current lane grouping
  const sourceCol = boardColumns.value.find(c => (issuesByLane.value[c.id] ?? []).some(i => i.id === issue.id))
  draggedSourceColId.value = sourceCol?.id ?? null
  e.dataTransfer!.effectAllowed = 'move'
}

function onIssueDragEnd() {
  draggedId.value = null
  draggedSourceColId.value = null
  dragHoverColId.value = null
  dragHoverInsertIdx.value = 0
}

function onIssueDragOver(e: DragEvent, colId: string) {
  if (!draggedId.value || draggedColId.value) return
  dragHoverColId.value = colId
  const container = e.currentTarget as HTMLElement
  const items = Array.from(container.querySelectorAll<HTMLElement>('[draggable="true"]'))
  let insertIdx = items.length
  for (let i = 0; i < items.length; i++) {
    const rect = items[i].getBoundingClientRect()
    if (e.clientY < rect.top + rect.height / 2) {
      insertIdx = i
      break
    }
  }
  dragHoverInsertIdx.value = insertIdx
}

function onIssueDragLeave(e: DragEvent) {
  const relatedTarget = e.relatedTarget as Node | null
  if (relatedTarget && (e.currentTarget as Node)?.contains(relatedTarget)) return
  dragHoverColId.value = null
}

async function onIssueDrop(e: DragEvent, targetCol: KanbanColumn) {
  e.preventDefault()
  if (draggedColId.value || !draggedId.value) return
  if (!isValidDropTarget(targetCol.id)) return
  const insertIdx = dragHoverInsertIdx.value
  const isSameColumn = draggedSourceColId.value === targetCol.id
  if (!isSameColumn) {
    await issueStore.updateIssueStatus(props.projectId, draggedId.value, targetCol.issueStatus)
  }
  await kanban.moveIssue(activeBoardId.value, draggedId.value, targetCol.id, insertIdx)
  const issues = issueStore.issues.filter(i => i.status === targetCol.issueStatus).sort((a, b) => a.kanbanRank - b.kanbanRank || a.createdAt.localeCompare(b.createdAt))
  const moved = issues.find(i => i.id === draggedId.value)
  if (moved) {
    const reordered = issues.filter(i => i.id !== draggedId.value)
    reordered.splice(Math.min(insertIdx, reordered.length), 0, moved)
    reordered.forEach((issue, idx) => { issue.kanbanRank = idx })
  }
  draggedId.value = null
  draggedSourceColId.value = null
  dragHoverColId.value = null
  dragHoverInsertIdx.value = 0
}

// ── Helpers ───────────────────────────────────────────────────────────────
function statusDotColor(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-500',
  }
  return map[status] ?? 'bg-gray-500'
}

onMounted(async () => {
  await Promise.all([
    issueStore.fetchIssues(props.projectId),
    kanban.fetchBoards(props.projectId),
  ])
  if (kanban.boards.length) {
    activeBoardId.value = props.boardId || kanban.boards[0].id
    await kanban.fetchTransitions(activeBoardId.value)
  }
})

watch(() => props.boardId, async (bid) => {
  if (bid && bid !== activeBoardId.value) {
    activeBoardId.value = bid
    await kanban.fetchTransitions(bid)
  }
})

watch(activeBoardId, async (bid) => {
  if (bid) {
    kanban.selectBoard(kanban.boards.find(b => b.id === bid)!)
    await kanban.fetchTransitions(bid)
  }
})
</script>
