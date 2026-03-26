<template>
  <div class="p-6 h-full flex flex-col overflow-y-auto">
    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 mb-6 shrink-0">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Kanban', to: `/projects/${id}/kanban`, icon: 'M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2' },
        { label: 'Manage Lanes', icon: 'M4 6h16M4 10h16M4 14h16M4 18h16' },
      ]" />
    </div>

    <!-- Board selector -->
    <div class="flex items-center gap-3 mb-6 shrink-0">
      <select v-if="kanban.boards.length" v-model="activeBoardId"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
        <option v-for="b in kanban.boards" :key="b.id" :value="b.id">{{ b.name }}</option>
      </select>
      <span v-else class="text-sm text-gray-500">No boards found</span>
    </div>

    <div v-if="kanban.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="activeBoardId">
      <!-- Lanes list -->
      <div class="mb-8">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-sm font-semibold text-gray-200">Lanes</h2>
          <span class="text-xs text-gray-500">{{ boardColumns.length }} lane{{ boardColumns.length !== 1 ? 's' : '' }}</span>
        </div>

        <div class="space-y-2">
          <div v-for="col in boardColumns" :key="col.id"
            class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <div class="flex items-center gap-3">
              <!-- Drag handle -->
              <svg class="w-4 h-4 text-gray-600 cursor-grab shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 8h16M4 16h16" />
              </svg>

              <!-- Name edit -->
              <div v-if="editingColumnId === col.id" class="flex-1 flex items-center gap-2">
                <input v-model="editColumnName" type="text"
                  class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                  @keyup.enter="saveColumnName(col)"
                  @keyup.escape="editingColumnId = null" />
                <button @click="saveColumnName(col)"
                  class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors">Save</button>
                <button @click="editingColumnId = null"
                  class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">Cancel</button>
              </div>
              <div v-else class="flex-1 flex items-center gap-2">
                <span class="text-sm text-gray-200 font-medium">{{ col.name }}</span>
                <span v-if="col.issueStatus" class="text-xs text-gray-500 bg-gray-800 px-1.5 py-0.5 rounded">{{ col.issueStatus }}</span>
                <button @click="startEditColumn(col)"
                  class="text-gray-600 hover:text-gray-300 transition-colors ml-1">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
              </div>

              <!-- Position controls -->
              <div class="flex items-center gap-1 shrink-0">
                <button @click="moveColumnUp(col.id)"
                  class="p-1 text-gray-600 hover:text-gray-300 transition-colors rounded"
                  title="Move up">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7" />
                  </svg>
                </button>
                <button @click="moveColumnDown(col.id)"
                  class="p-1 text-gray-600 hover:text-gray-300 transition-colors rounded"
                  title="Move down">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                  </svg>
                </button>
              </div>

              <!-- Delete -->
              <button @click="requestDeleteColumn(col.id)"
                class="text-gray-600 hover:text-red-400 transition-colors shrink-0">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>
        </div>

        <!-- Add lane -->
        <div class="mt-4 border-t border-gray-800 pt-4">
          <p class="text-xs text-gray-500 mb-3">Add lane</p>
          <div class="flex gap-2">
            <input v-model="newColName" type="text" placeholder="Lane name"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
              @keyup.enter="addColumn" />
            <button @click="addColumn"
              class="bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              Add
            </button>
          </div>
        </div>
      </div>

      <!-- Transitions with requirements -->
      <div>
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-sm font-semibold text-gray-200">Transitions &amp; Requirements</h2>
          <span class="text-xs text-gray-500">{{ kanban.transitions.length }} transition{{ kanban.transitions.length !== 1 ? 's' : '' }}</span>
        </div>

        <div class="space-y-3">
          <div v-for="t in kanban.transitions" :key="t.id"
            class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <div class="flex items-center gap-3 mb-3">
              <span class="text-sm text-gray-200 font-medium flex-1">{{ t.name }}</span>
              <span class="text-xs text-gray-500">{{ columnName(t.fromColumnId) }} → {{ columnName(t.toColumnId) }}</span>
              <span v-if="t.isAuto" class="text-xs bg-blue-900/40 text-blue-300 px-1.5 py-0.5 rounded">auto</span>
            </div>
            <!-- Requirement indicators -->
            <div class="flex flex-wrap gap-2">
              <span v-if="t.requireGreenCiCd" class="text-xs bg-green-900/30 text-green-400 px-2 py-0.5 rounded-full">✓ CI/CD</span>
              <span v-if="t.requireCodeReview" class="text-xs bg-purple-900/30 text-purple-400 px-2 py-0.5 rounded-full">✓ Code Review</span>
              <span v-if="t.requirePlanComment" class="text-xs bg-yellow-900/30 text-yellow-400 px-2 py-0.5 rounded-full">✓ Plan</span>
              <span v-if="t.requireTasksDone" class="text-xs bg-blue-900/30 text-blue-400 px-2 py-0.5 rounded-full">✓ Tasks Done</span>
              <span v-if="t.requireSubIssuesDone" class="text-xs bg-indigo-900/30 text-indigo-400 px-2 py-0.5 rounded-full">✓ Sub-issues Done</span>
              <span v-if="!t.requireGreenCiCd && !t.requireCodeReview && !t.requirePlanComment && !t.requireTasksDone && !t.requireSubIssuesDone"
                class="text-xs text-gray-600">No requirements</span>
            </div>
          </div>
          <div v-if="!kanban.transitions.length" class="text-sm text-gray-600 text-center py-6">
            No transitions defined. Go to the <NuxtLink :to="`/projects/${id}/kanban`" class="text-brand-400 hover:text-brand-300">Kanban board</NuxtLink> to add transitions.
          </div>
        </div>
      </div>
    </template>

    <!-- Delete column confirmation -->
    <div v-if="deletingColumnId" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-sm p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-2">Delete Lane</h2>
        <p class="text-sm text-gray-400 mb-5">Are you sure you want to delete this lane? This action cannot be undone.</p>
        <div class="flex gap-3">
          <button @click="confirmDeleteColumn"
            class="flex-1 bg-red-600 hover:bg-red-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Delete
          </button>
          <button @click="deletingColumnId = null"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useKanbanStore } from '~/stores/kanban'
import { useProjectsStore } from '~/stores/projects'
import { IssueStatus } from '~/types'
import type { KanbanColumn } from '~/types'

const route = useRoute()
const id = route.params.id as string

const kanban = useKanbanStore()
const projectsStore = useProjectsStore()

const activeBoardId = ref<string>('')
const editingColumnId = ref<string | null>(null)
const editColumnName = ref('')
const newColName = ref('')
const deletingColumnId = ref<string | null>(null)

const activeBoard = computed(() => kanban.boards.find(b => b.id === activeBoardId.value) ?? null)
const boardColumns = computed(() =>
  (activeBoard.value?.columns ?? []).slice().sort((a, b) => a.position - b.position),
)

function columnName(columnId: string) {
  return boardColumns.value.find(c => c.id === columnId)?.name ?? columnId
}

function startEditColumn(col: KanbanColumn) {
  editingColumnId.value = col.id
  editColumnName.value = col.name
}

async function saveColumnName(col: KanbanColumn) {
  if (!editColumnName.value.trim()) return
  await kanban.updateColumn(activeBoardId.value, col.id, editColumnName.value.trim(), col.position, col.issueStatus, col.laneValue)
  editingColumnId.value = null
}

async function moveColumnUp(columnId: string) {
  const cols = boardColumns.value.slice()
  const idx = cols.findIndex(c => c.id === columnId)
  if (idx <= 0) return
  const temp = cols[idx - 1]
  cols[idx - 1] = cols[idx]
  cols[idx] = temp
  await kanban.reorderColumns(activeBoardId.value, cols.map(c => c.id))
}

async function moveColumnDown(columnId: string) {
  const cols = boardColumns.value.slice()
  const idx = cols.findIndex(c => c.id === columnId)
  if (idx < 0 || idx >= cols.length - 1) return
  const temp = cols[idx]
  cols[idx] = cols[idx + 1]
  cols[idx + 1] = temp
  await kanban.reorderColumns(activeBoardId.value, cols.map(c => c.id))
}

function requestDeleteColumn(columnId: string) {
  deletingColumnId.value = columnId
}

async function confirmDeleteColumn() {
  if (!deletingColumnId.value) return
  await kanban.deleteColumn(activeBoardId.value, deletingColumnId.value)
  deletingColumnId.value = null
}

async function addColumn() {
  if (!newColName.value.trim() || !activeBoardId.value) return
  const pos = boardColumns.value.length
  await kanban.addColumn(activeBoardId.value, newColName.value.trim(), pos, IssueStatus.Backlog)
  newColName.value = ''
}

watch(activeBoardId, (bid) => {
  if (bid) {
    kanban.selectBoard(kanban.boards.find(b => b.id === bid)!)
    kanban.fetchTransitions(bid)
  }
})

onMounted(async () => {
  await Promise.all([
    kanban.fetchBoards(id),
    projectsStore.fetchProject(id),
  ])
  if (kanban.boards.length) {
    activeBoardId.value = kanban.boards[0].id
  }
})
</script>
