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
              <div v-if="editingColumnId === col.id" class="flex-1 flex flex-col gap-2">
                <div class="flex items-center gap-2">
                  <input v-model="editColumnName" type="text"
                    class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                    @keyup.enter="saveColumnName(col)"
                    @keyup.escape="editingColumnId = null" />
                </div>
                <div class="flex items-center gap-2">
                  <label class="text-xs text-gray-500 shrink-0">Agent:</label>
                  <select v-model="editColumnAgentId"
                    class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-2 py-1 text-xs text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                    <option :value="null">None</option>
                    <option v-for="agent in agentsStore.agents" :key="agent.id" :value="agent.id">{{ agent.name }}</option>
                  </select>
                </div>
                <div class="flex items-center gap-2">
                  <button @click="saveColumnName(col)"
                    class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors">Save</button>
                  <button @click="editingColumnId = null"
                    class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">Cancel</button>
                </div>
              </div>
              <div v-else class="flex-1 flex items-center gap-2">
                <span class="text-sm text-gray-200 font-medium">{{ col.name }}</span>
                <span v-if="col.issueStatus" class="text-xs text-gray-500 bg-gray-800 px-1.5 py-0.5 rounded">{{ col.issueStatus }}</span>
                <span v-if="agentName(col.defaultAgentId)" class="text-xs text-blue-400 bg-blue-900/30 px-1.5 py-0.5 rounded flex items-center gap-1">
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
                  </svg>
                  {{ agentName(col.defaultAgentId) }}
                </span>
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

      <!-- Orchestrator Schedule -->
      <div class="mt-8">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-sm font-semibold text-gray-200">Orchestrator Schedule</h2>
          <span v-if="schedule?.isEnabled" class="text-xs bg-green-900/30 text-green-400 px-2 py-0.5 rounded-full">enabled</span>
          <span v-else-if="schedule" class="text-xs bg-gray-800 text-gray-500 px-2 py-0.5 rounded-full">disabled</span>
        </div>

        <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 space-y-4">
          <p class="text-xs text-gray-500">
            Configure an agent to automatically orchestrate this board on a schedule.
            The agent will only run if the board state has changed since the last run.
          </p>

          <!-- Current schedule info -->
          <div v-if="schedule && !editingSchedule" class="space-y-2">
            <div class="flex items-center gap-3 text-sm">
              <span class="text-gray-400 w-24 shrink-0">Agent:</span>
              <span class="text-gray-200">{{ agentName(schedule.agentId) ?? schedule.agentId }}</span>
            </div>
            <div class="flex items-center gap-3 text-sm">
              <span class="text-gray-400 w-24 shrink-0">Interval:</span>
              <span class="text-gray-200">Every {{ schedule.intervalMinutes }} min</span>
            </div>
            <div class="flex items-center gap-3 text-sm">
              <span class="text-gray-400 w-24 shrink-0">Last run:</span>
              <span class="text-gray-200">
                <DateDisplay v-if="schedule.lastRunAt" :date="schedule.lastRunAt" mode="relative" />
                <span v-else class="text-gray-600">Never</span>
              </span>
            </div>
            <div class="flex gap-2 pt-2">
              <button @click="editingSchedule = true"
                class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
                Edit
              </button>
              <button @click="triggerOrchestratorNow" :disabled="triggeringOrchestrator"
                class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
                {{ triggeringOrchestrator ? 'Triggering…' : 'Run now' }}
              </button>
              <button @click="deleteSchedule"
                class="text-xs bg-gray-800 hover:bg-red-900 text-gray-400 hover:text-red-300 px-3 py-1.5 rounded-lg transition-colors">
                Remove
              </button>
            </div>
            <div v-if="scheduleMessage" class="text-xs mt-2" :class="scheduleMessageIsError ? 'text-red-400' : 'text-green-400'">
              {{ scheduleMessage }}
            </div>
          </div>

          <!-- Edit / create schedule form -->
          <div v-else class="space-y-3">
            <div class="flex items-center gap-3">
              <label class="text-xs text-gray-500 w-24 shrink-0">Agent:</label>
              <select v-model="scheduleAgentId"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-2 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option value="">Select agent…</option>
                <option v-for="agent in agentsStore.agents" :key="agent.id" :value="agent.id">{{ agent.name }}</option>
              </select>
            </div>
            <div class="flex items-center gap-3">
              <label class="text-xs text-gray-500 w-24 shrink-0">Interval (min):</label>
              <input v-model.number="scheduleIntervalMinutes" type="number" min="5" max="1440"
                class="w-24 bg-gray-800 border border-gray-700 rounded-lg px-2 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="flex items-center gap-3">
              <label class="text-xs text-gray-500 w-24 shrink-0">Enabled:</label>
              <input v-model="scheduleEnabled" type="checkbox" class="rounded" />
            </div>
            <div class="flex gap-2 pt-1">
              <button @click="saveSchedule" :disabled="!scheduleAgentId"
                class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
                Save
              </button>
              <button @click="cancelEditSchedule"
                class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
                Cancel
              </button>
            </div>
            <div v-if="scheduleMessage" class="text-xs mt-1" :class="scheduleMessageIsError ? 'text-red-400' : 'text-green-400'">
              {{ scheduleMessage }}
            </div>
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
import { useAgentsStore } from '~/stores/agents'
import { IssueStatus } from '~/types'
import type { KanbanColumn } from '~/types'
import DateDisplay from '~/components/DateDisplay.vue'

const route = useRoute()
const id = route.params.id as string

const kanban = useKanbanStore()
const projectsStore = useProjectsStore()
const agentsStore = useAgentsStore()

const activeBoardId = ref<string>('')
const editingColumnId = ref<string | null>(null)
const editColumnName = ref('')
const editColumnAgentId = ref<string | null>(null)
const newColName = ref('')
const deletingColumnId = ref<string | null>(null)

// Orchestrator schedule state
interface OrchestratorSchedule {
  id: string
  boardId: string
  agentId: string
  isEnabled: boolean
  intervalMinutes: number
  lastRunAt: string | null
  lastBoardStateHash: string | null
  lastSessionId: string | null
}
const schedule = ref<OrchestratorSchedule | null>(null)
const editingSchedule = ref(false)
const scheduleAgentId = ref('')
const scheduleIntervalMinutes = ref(60)
const scheduleEnabled = ref(true)
const scheduleMessage = ref('')
const scheduleMessageIsError = ref(false)
const triggeringOrchestrator = ref(false)

const activeBoard = computed(() => kanban.boards.find(b => b.id === activeBoardId.value) ?? null)
const boardColumns = computed(() =>
  (activeBoard.value?.columns ?? []).slice().sort((a, b) => a.position - b.position),
)

function columnName(columnId: string) {
  return boardColumns.value.find(c => c.id === columnId)?.name ?? columnId
}

function agentName(agentId?: string | null) {
  if (!agentId) return null
  return agentsStore.agents.find(a => a.id === agentId)?.name ?? agentId
}

function startEditColumn(col: KanbanColumn) {
  editingColumnId.value = col.id
  editColumnName.value = col.name
  editColumnAgentId.value = col.defaultAgentId ?? null
}

async function saveColumnName(col: KanbanColumn) {
  if (!editColumnName.value.trim()) return
  await kanban.updateColumn(activeBoardId.value, col.id, editColumnName.value.trim(), col.position, col.issueStatus, col.laneValue, editColumnAgentId.value)
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

// ── Orchestrator Schedule ────────────────────────────────────────────────────

async function fetchSchedule(boardId: string) {
  schedule.value = null
  try {
    const api = useApi()
    const res = await api.get<OrchestratorSchedule>(`/api/kanban/boards/${boardId}/orchestrator-schedule`)
    schedule.value = res
    editingSchedule.value = false
  } catch {
    // 404 means no schedule configured yet — that's fine
    schedule.value = null
    editingSchedule.value = false
  }
}

function cancelEditSchedule() {
  editingSchedule.value = false
  scheduleMessage.value = ''
  if (schedule.value) {
    scheduleAgentId.value = schedule.value.agentId
    scheduleIntervalMinutes.value = schedule.value.intervalMinutes
    scheduleEnabled.value = schedule.value.isEnabled
  }
}

async function saveSchedule() {
  if (!scheduleAgentId.value || !activeBoardId.value) return
  scheduleMessage.value = ''
  scheduleMessageIsError.value = false
  try {
    const api = useApi()
    const res = await api.put<OrchestratorSchedule>(`/api/kanban/boards/${activeBoardId.value}/orchestrator-schedule`, {
      agentId: scheduleAgentId.value,
      intervalMinutes: scheduleIntervalMinutes.value,
      isEnabled: scheduleEnabled.value,
    })
    schedule.value = res
    editingSchedule.value = false
    scheduleMessage.value = 'Schedule saved.'
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Failed to save schedule'
    scheduleMessage.value = msg
    scheduleMessageIsError.value = true
  }
}

async function deleteSchedule() {
  if (!activeBoardId.value) return
  scheduleMessage.value = ''
  scheduleMessageIsError.value = false
  try {
    const api = useApi()
    await api.del(`/api/kanban/boards/${activeBoardId.value}/orchestrator-schedule`)
    schedule.value = null
    editingSchedule.value = false
    scheduleMessage.value = 'Schedule removed.'
    scheduleMessageIsError.value = false
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Failed to remove schedule'
    scheduleMessage.value = msg
    scheduleMessageIsError.value = true
  }
}

async function triggerOrchestratorNow() {
  if (!activeBoardId.value) return
  triggeringOrchestrator.value = true
  scheduleMessage.value = ''
  scheduleMessageIsError.value = false
  try {
    const api = useApi()
    const res = await api.post<{ triggered: boolean; reason: string; sessionId?: string }>(
      `/api/kanban/boards/${activeBoardId.value}/orchestrator-schedule/trigger`, {})
    if (res?.triggered) {
      scheduleMessage.value = `Orchestrator triggered. Session: ${res.sessionId}`
    } else {
      scheduleMessage.value = res?.reason ?? 'Not triggered (board unchanged).'
    }
    scheduleMessageIsError.value = false
    await fetchSchedule(activeBoardId.value)
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : 'Failed to trigger'
    scheduleMessage.value = msg
    scheduleMessageIsError.value = true
  } finally {
    triggeringOrchestrator.value = false
  }
}

watch(activeBoardId, (bid) => {
  if (bid) {
    kanban.selectBoard(kanban.boards.find(b => b.id === bid)!)
    kanban.fetchTransitions(bid)
    fetchSchedule(bid)
  }
})

onMounted(async () => {
  await Promise.all([
    kanban.fetchBoards(id),
    projectsStore.fetchProject(id),
    agentsStore.fetchAgents(),
  ])
  if (kanban.boards.length) {
    activeBoardId.value = kanban.boards[0].id
  }
  // Initialize schedule form defaults
  scheduleIntervalMinutes.value = 60
  scheduleEnabled.value = true
})
</script>
