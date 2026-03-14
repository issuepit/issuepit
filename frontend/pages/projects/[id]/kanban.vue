<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6 shrink-0">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Kanban', to: `/projects/${id}/kanban`, icon: 'M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2' },
      ]" />
      <div class="flex items-center gap-2">
        <!-- Board selector -->
        <select v-if="kanban.boards.length" v-model="activeBoardId"
          class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
          <option v-for="b in kanban.boards" :key="b.id" :value="b.id">{{ b.name }}</option>
        </select>

        <!-- Create board -->
        <button @click="showNewBoard = true"
          class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
          + Board
        </button>

        <!-- Manage lanes -->
        <button v-if="activeBoard" @click="showLanes = true"
          class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
          Lanes
        </button>

        <!-- Manage transitions -->
        <button v-if="activeBoard" @click="openTransitions"
          :class="[
            'text-xs bg-gray-800 hover:bg-gray-700 border text-gray-300 px-3 py-1.5 rounded-lg transition-colors',
            transitionsButtonAlert ? 'border-amber-400 animate-pulse text-amber-300' : 'border-gray-700'
          ]">
          Transitions
        </button>

        <!-- Milestone filter -->
        <select v-if="milestonesStore.milestones.length" v-model="filterMilestone"
          class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option value="">All Milestones</option>
          <option v-for="m in milestonesStore.milestones" :key="m.id" :value="m.id">{{ m.title }}</option>
        </select>

        <span class="text-xs text-gray-500">{{ totalIssues }} issues</span>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="issueStore.loading || kanban.loading" class="flex items-center justify-center flex-1">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Board -->
    <div v-else class="flex gap-4 overflow-x-auto flex-1 pb-4">
      <div v-for="col in boardColumns" :key="col.id"
        class="flex flex-col w-72 shrink-0 transition-opacity duration-150"
        :data-col-id="col.id"
        :class="{
          'opacity-50': draggedColId === col.id,
          'opacity-40': draggedId && !draggedColId && !isValidDropTarget(col.id),
        }"
        @dragover.prevent="onColDragOver($event, col.id)"
        @drop="onColDrop($event, col.id)">
        <!-- Column Header -->
        <div class="flex items-center justify-between mb-3 cursor-grab active:cursor-grabbing"
          draggable="true"
          @dragstart="onColDragStart($event, col.id)"
          @dragend="onColDragEnd">
          <div class="flex items-center gap-2">
            <span class="text-gray-600 select-none">⠿</span>
            <span :class="statusDotColor(col.issueStatus)" class="w-2.5 h-2.5 rounded-full"></span>
            <h3 class="text-sm font-semibold text-gray-300">{{ col.name }}</h3>
            <span class="text-xs text-gray-600 bg-gray-800 px-1.5 py-0.5 rounded-full">
              {{ issuesByColumn[col.id]?.length ?? 0 }}
            </span>
          </div>
          <button @click.stop="openCreateForStatus(col.issueStatus)"
            class="text-gray-600 hover:text-gray-400 transition-colors p-0.5 rounded">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
          </button>
        </div>

        <!-- Cards -->
        <div class="flex-1 space-y-2 bg-gray-900/40 rounded-xl p-2 min-h-32 border border-gray-800/60"
          :class="{
            'border-brand-500/60 bg-brand-900/10': isValidDropTarget(col.id) && draggedId,
            'border-gray-700/30 bg-gray-900/20': draggedId && !draggedColId && !isValidDropTarget(col.id),
          }"
          @dragover.prevent="onIssueDragOver($event, col.id)"
          @dragleave="onIssueDragLeave"
          @drop="onIssueDrop($event, col)">
          <template v-for="(issue, idx) in issuesByColumn[col.id]" :key="issue.id">
            <!-- Placeholder before this item -->
            <div v-if="draggedId && !draggedColId && isValidDropTarget(col.id) && dragHoverColId === col.id && dragHoverInsertIdx === idx"
              role="status" aria-label="Drop zone"
              class="rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-14 animate-pulse">
            </div>
            <div
              :class="[
                'bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-lg p-3 cursor-pointer group transition-all hover:shadow-lg hover:-translate-y-0.5',
                issue.id === draggedId ? 'invisible h-14' : '',
              ]"
              draggable="true"
              @dragstart="onDragStart($event, issue)"
              @dragend="onIssueDragEnd"
              @click="previewIssue = issue">
              <div class="flex items-start justify-between gap-2 mb-2">
                <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, projectsStore.currentProject) }}</span>
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
          </template>

          <!-- Drop zone placeholder at the end of the list -->
          <div v-if="draggedId && !draggedColId && isValidDropTarget(col.id) && dragHoverColId === col.id && dragHoverInsertIdx >= (issuesByColumn[col.id]?.length ?? 0)"
            role="status" aria-label="Drop zone"
            class="rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-14 animate-pulse">
          </div>

          <!-- Empty placeholder -->
          <div v-if="!issuesByColumn[col.id]?.length && !(draggedId && !draggedColId && isValidDropTarget(col.id) && dragHoverColId === col.id)"
            class="flex items-center justify-center h-16 text-gray-700 text-xs">
            Drop issues here
          </div>
        </div>
      </div>

      <!-- No board / no columns -->
      <div v-if="!activeBoard" class="flex items-center justify-center flex-1 text-gray-600 text-sm">
        No boards yet. Create one with the <strong class="text-gray-400 mx-1">+ Board</strong> button.
      </div>
      <div v-else-if="!boardColumns.length" class="flex items-center justify-center flex-1 text-gray-600 text-sm">
        No lanes yet. Click <strong class="text-gray-400 mx-1">Lanes</strong> to add columns.
      </div>
    </div>

    <!-- Quick Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">
          Add to {{ boardColumns.find(c => c.issueStatus === createStatus)?.name }}
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
              <option value="very_high">🟠 Very High</option>
              <option value="high">🟡 High</option>
              <option value="medium">🟢 Medium</option>
              <option value="low">🔵 Low</option>
              <option value="unknown">🟣 Unknown</option>
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

    <!-- New Board Modal -->
    <div v-if="showNewBoard" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">New Board</h2>
        <input v-model="newBoardName" type="text" placeholder="Board name..."
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
          @keyup.enter="submitNewBoard" />
        <div class="flex gap-3 mt-6">
          <button @click="submitNewBoard"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create
          </button>
          <button @click="showNewBoard = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Lane Management Modal -->
    <div v-if="showLanes && activeBoard" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Manage Lanes — {{ activeBoard.name }}</h2>

        <!-- Existing columns (draggable for reorder) -->
        <div class="space-y-2 mb-4 max-h-64 overflow-y-auto">
          <template v-for="(col, idx) in boardColumns" :key="col.id">
            <!-- Drop placeholder before this item -->
            <div v-if="draggedLaneId && draggedLaneId !== col.id && laneHoverInsertIdx === idx"
              class="h-8 rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 animate-pulse">
            </div>
            <div
              draggable="true"
              @dragstart="onLaneDragStart($event, col.id)"
              @dragover.prevent="onLaneDragOver($event, col.id, idx)"
              @drop.stop="onLaneDrop($event, col.id)"
              @dragend="onLaneDragEnd"
              :class="['flex items-center gap-3 bg-gray-800 rounded-lg px-3 py-2 cursor-grab active:cursor-grabbing', draggedLaneId === col.id ? 'invisible' : '']">
              <span class="text-gray-500 select-none">⠿</span>
              <span :class="statusDotColor(col.issueStatus)" class="w-2 h-2 rounded-full shrink-0"></span>
              <span class="text-sm text-gray-300 flex-1">{{ col.name }}</span>
              <span class="text-xs text-gray-600">pos {{ col.position }}</span>
              <button @click="deleteColumn(col.id)"
                class="text-gray-600 hover:text-red-400 transition-colors">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          </template>
          <!-- Drop placeholder at end -->
          <div v-if="draggedLaneId && laneHoverInsertIdx >= boardColumns.length"
            class="h-8 rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 animate-pulse">
          </div>
          <div v-if="!boardColumns.length" class="text-xs text-gray-600 text-center py-4">No lanes yet</div>
        </div>

        <!-- Add new column -->
        <div class="border-t border-gray-800 pt-4">
          <p class="text-xs text-gray-500 mb-3">Add lane</p>
          <div class="flex gap-2">
            <input v-model="newColName" type="text" placeholder="Lane name"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            <select v-model="newColStatus"
              class="bg-gray-800 border border-gray-700 rounded-lg px-2 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="s in statusOptions" :key="s.value" :value="s.value">{{ s.label }}</option>
            </select>
          </div>
          <button @click="submitAddColumn"
            class="mt-3 w-full bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Add Lane
          </button>
        </div>

        <button @click="showLanes = false"
          class="mt-3 w-full bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
          Done
        </button>
      </div>
    </div>

    <!-- Transitions Modal -->
    <div v-if="showTransitions && activeBoard" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Transitions — {{ activeBoard.name }}</h2>

        <!-- Existing transitions -->
        <div class="space-y-2 mb-4 max-h-64 overflow-y-auto">
          <div v-for="t in kanban.transitions" :key="t.id"
            class="flex items-center gap-3 bg-gray-800 rounded-lg px-3 py-2">
            <span class="text-sm text-gray-300 flex-1">{{ t.name }}</span>
            <span class="text-xs text-gray-600">
              {{ columnName(t.fromColumnId) }} → {{ columnName(t.toColumnId) }}
            </span>
            <span v-if="t.isAuto" class="text-xs bg-blue-900/40 text-blue-300 px-1.5 py-0.5 rounded">auto</span>
            <button @click="deleteTransition(t.id)"
              class="text-gray-600 hover:text-red-400 transition-colors">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <div v-if="!kanban.transitions.length" class="text-xs text-gray-600 text-center py-4">No transitions yet</div>
        </div>

        <!-- Add new transition -->
        <div class="border-t border-gray-800 pt-4">
          <p class="text-xs text-gray-500 mb-3">Add transition</p>
          <div class="space-y-2">
            <input v-model="newTransName" type="text" placeholder="Transition name"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            <div class="flex gap-2">
              <select v-model="newTransFrom"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-2 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option value="">From column</option>
                <option v-for="c in boardColumns" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
              <select v-model="newTransTo"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-2 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option value="">To column</option>
                <option v-for="c in boardColumns" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
            </div>
            <label class="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input v-model="newTransIsAuto" type="checkbox" class="accent-brand-500" />
              Auto-trigger (by agent)
            </label>
          </div>
          <button @click="submitAddTransition"
            class="mt-3 w-full bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Add Transition
          </button>
        </div>

        <button @click="showTransitions = false"
          class="mt-3 w-full bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
          Done
        </button>
      </div>
    </div>

    <!-- Issue Preview Sidebar -->
    <transition name="slide-right">
      <div v-if="previewIssue"
        class="fixed inset-y-0 right-0 w-96 bg-gray-900 border-l border-gray-800 shadow-2xl z-40 flex flex-col overflow-y-auto">
        <!-- Header -->
        <div class="flex items-center justify-between p-4 border-b border-gray-800 shrink-0">
          <span class="text-xs text-gray-500">{{ formatIssueId(previewIssue.number, projectsStore.currentProject) }}</span>
          <div class="flex items-center gap-2">
            <NuxtLink :to="`/projects/${id}/issues/${previewIssue.number}`"
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
              Open full
            </NuxtLink>
            <button @click="previewIssue = null" class="text-gray-500 hover:text-gray-300 transition-colors">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
        <!-- Content -->
        <div class="p-4 space-y-4 flex-1">
          <!-- Title -->
          <h2 class="text-base font-semibold text-white leading-snug">{{ previewIssue.title }}</h2>
          <!-- Meta row -->
          <div class="flex flex-wrap gap-2">
            <span :class="typeBadge(previewIssue.type)" class="text-xs px-1.5 py-0.5 rounded font-medium capitalize">{{ previewIssue.type }}</span>
            <span :class="priorityColor(previewIssue.priority)" class="text-xs">{{ priorityIcon(previewIssue.priority) }} {{ priorityLabel(previewIssue.priority) }}</span>
          </div>
          <!-- Status -->
          <div class="flex items-center gap-2">
            <span :class="statusDotColor(previewIssue.status)" class="w-2 h-2 rounded-full"></span>
            <span class="text-xs text-gray-400 capitalize">{{ previewIssue.status.replace('_', ' ') }}</span>
          </div>
          <!-- Labels -->
          <div v-if="previewIssue.labels?.length" class="flex flex-wrap gap-1.5">
            <span v-for="label in previewIssue.labels" :key="label.id"
              class="text-xs px-2 py-0.5 rounded-full font-medium"
              :style="{ backgroundColor: label.color + '33', color: label.color }">
              {{ label.name }}
            </span>
          </div>
          <!-- Assignees -->
          <div v-if="previewIssue.assignees?.length" class="flex items-center gap-2 flex-wrap">
            <span class="text-xs text-gray-500">Assigned:</span>
            <span v-for="a in previewIssue.assignees" :key="a.id" class="text-xs text-gray-300">
              {{ (a as { user?: { username?: string }, agent?: { name?: string } }).user?.username || (a as { user?: { username?: string }, agent?: { name?: string } }).agent?.name || 'Unknown' }}
            </span>
          </div>
          <!-- Milestone -->
          <div v-if="previewIssue.milestoneId" class="flex items-center gap-2">
            <span class="text-xs text-gray-500">Milestone:</span>
            <span class="text-xs text-gray-300">
              {{ milestonesStore.milestones.find(m => m.id === previewIssue!.milestoneId)?.title || 'Unknown' }}
            </span>
          </div>
          <!-- Body -->
          <div v-if="previewIssue.body" class="text-sm text-gray-400 border-t border-gray-800 pt-3 leading-relaxed line-clamp-6">
            {{ previewIssue.body }}
          </div>
        </div>
        <!-- Footer -->
        <div class="p-4 border-t border-gray-800 shrink-0">
          <NuxtLink :to="`/projects/${id}/issues/${previewIssue.number}`"
            class="block w-full text-center text-sm text-brand-400 hover:text-brand-300 border border-brand-700 hover:border-brand-600 rounded-lg py-2 transition-colors">
            View Full Issue →
          </NuxtLink>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus, IssuePriority, IssueType, KanbanLaneType } from '~/types'
import type { Issue, KanbanColumn } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useKanbanStore } from '~/stores/kanban'
import { useMilestonesStore } from '~/stores/milestones'
import { useProjectsStore } from '~/stores/projects'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const id = route.params.id as string
const issueStore = useIssuesStore()
const kanban = useKanbanStore()
const milestonesStore = useMilestonesStore()
const projectsStore = useProjectsStore()
const { priorityIcon, priorityColor, priorityLabel } = usePriority()

// ── Issue preview ──────────────────────────────────────────────────────────
const previewIssue = ref<Issue | null>(null)

// ── Issue create state ────────────────────────────────────────────────────
const showCreate = ref(false)
const createTitle = ref('')
const createPriority = ref<IssuePriority>(IssuePriority.NoPriority)
const createStatus = ref<IssueStatus>(IssueStatus.Backlog)
const filterMilestone = ref<string>('')

// ── Issue drag state ──────────────────────────────────────────────────────
const draggedId = ref<string | null>(null)
const draggedIssueStatus = ref<IssueStatus | null>(null)
const transitionsButtonAlert = ref(false)
const dragHoverColId = ref<string | null>(null)
const dragHoverInsertIdx = ref<number>(0)

// ── Column drag state (board view) ─────────────────────────────────────────
const draggedColId = ref<string | null>(null)

// ── Lane drag state (modal) ────────────────────────────────────────────────
const draggedLaneId = ref<string | null>(null)
const laneHoverInsertIdx = ref<number>(0)

// ── Board state ───────────────────────────────────────────────────────────
const showNewBoard = ref(false)
const newBoardName = ref('')
const activeBoardId = ref<string>('')

const activeBoard = computed(() => kanban.boards.find(b => b.id === activeBoardId.value) ?? null)
const boardColumns = computed(() =>
  (activeBoard.value?.columns ?? []).slice().sort((a, b) => a.position - b.position)
)

// ── Lane state ────────────────────────────────────────────────────────────
const showLanes = ref(false)
const newColName = ref('')
const newColStatus = ref<IssueStatus>(IssueStatus.Todo)

// ── Transition state ──────────────────────────────────────────────────────
const showTransitions = ref(false)
const newTransName = ref('')
const newTransFrom = ref('')
const newTransTo = ref('')
const newTransIsAuto = ref(false)

const issuesByColumn = computed(() => {
  const board = activeBoard.value
  if (!board) return {} as Record<string, Issue[]>
  const filteredIssues = filterMilestone.value
    ? issueStore.issues.filter(i => i.milestoneId === filterMilestone.value)
    : issueStore.issues

  const result: Record<string, Issue[]> = {}
  for (const col of boardColumns.value) {
    result[col.id] = []
  }

  if (!board.laneType || board.laneType === KanbanLaneType.Status) {
    for (const issue of filteredIssues) {
      const col = boardColumns.value.find(c => c.issueStatus === issue.status)
      if (col) result[col.id]?.push(issue)
    }
  }
  else if (board.laneType === KanbanLaneType.Label) {
    for (const issue of filteredIssues) {
      let placed = false
      for (const col of boardColumns.value) {
        if (col.filterValue && issue.labels.some(l => l.id === col.filterValue)) {
          result[col.id]?.push(issue)
          placed = true
          break
        }
      }
      if (!placed) {
        const unlabeledCol = boardColumns.value.find(c => !c.filterValue)
        if (unlabeledCol) result[unlabeledCol.id]?.push(issue)
      }
    }
  }
  else if (board.laneType === KanbanLaneType.Type) {
    for (const issue of filteredIssues) {
      const col = boardColumns.value.find(c => c.filterValue === issue.type)
      if (col) result[col.id]?.push(issue)
      else {
        const defaultCol = boardColumns.value.find(c => !c.filterValue)
        if (defaultCol) result[defaultCol.id]?.push(issue)
      }
    }
  }
  else if (board.laneType === KanbanLaneType.Agent) {
    for (const issue of filteredIssues) {
      let placed = false
      for (const col of boardColumns.value) {
        if (col.filterValue && issue.assignees.some((a: { agentId?: string | null }) => a.agentId === col.filterValue)) {
          result[col.id]?.push(issue)
          placed = true
          break
        }
      }
      if (!placed) {
        const defaultCol = boardColumns.value.find(c => !c.filterValue)
        if (defaultCol) result[defaultCol.id]?.push(issue)
      }
    }
  }
  else if (board.laneType === KanbanLaneType.Milestone) {
    for (const issue of filteredIssues) {
      const col = boardColumns.value.find(c => c.filterValue === (issue.milestoneId ?? null))
      if (col) result[col.id]?.push(issue)
      else {
        const defaultCol = boardColumns.value.find(c => !c.filterValue)
        if (defaultCol) result[defaultCol.id]?.push(issue)
      }
    }
  }

  for (const colId of Object.keys(result)) {
    result[colId].sort((a, b) => a.kanbanRank - b.kanbanRank || a.createdAt.localeCompare(b.createdAt))
  }
  return result
})
const totalIssues = computed(() => {
  if (!filterMilestone.value) return issueStore.issues.length
  return issueStore.issues.filter(i => i.milestoneId === filterMilestone.value).length
})
const statusOptions = [
  { value: IssueStatus.Backlog, label: 'Backlog' },
  { value: IssueStatus.Todo, label: 'Todo' },
  { value: IssueStatus.InProgress, label: 'In Progress' },
  { value: IssueStatus.InReview, label: 'In Review' },
  { value: IssueStatus.Done, label: 'Done' },
  { value: IssueStatus.Cancelled, label: 'Cancelled' },
]

onMounted(async () => {
  await Promise.all([
    issueStore.fetchIssues(id),
    kanban.fetchBoards(id),
    milestonesStore.fetchMilestones(id),
  ])
  if (kanban.boards.length) activeBoardId.value = kanban.boards[0].id
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') previewIssue.value = null })
})

watch(activeBoardId, (bid) => {
  if (bid) {
    kanban.selectBoard(kanban.boards.find(b => b.id === bid)!)
    kanban.fetchTransitions(bid)
  }
})

// ── Issue drag & drop ─────────────────────────────────────────────────────
function onDragStart(e: DragEvent, issue: Issue) {
  draggedId.value = issue.id
  draggedIssueStatus.value = issue.status
  e.dataTransfer!.effectAllowed = 'move'
  // Blink the Transitions button if the source column has no outgoing transitions
  const sourceCol = boardColumns.value.find(c => c.issueStatus === issue.status)
  if (sourceCol) {
    const hasOutgoing = kanban.transitions.some(t => t.fromColumnId === sourceCol.id)
    transitionsButtonAlert.value = !hasOutgoing
  } else {
    transitionsButtonAlert.value = false
  }
}

function onIssueDragEnd() {
  draggedId.value = null
  draggedIssueStatus.value = null
  dragHoverColId.value = null
  dragHoverInsertIdx.value = 0
  transitionsButtonAlert.value = false
}

function isValidDropTarget(targetColId: string): boolean {
  if (!draggedId.value || !draggedIssueStatus.value) return false
  const sourceCol = boardColumns.value.find(c => c.issueStatus === draggedIssueStatus.value)
  if (!sourceCol) return false
  // Same column: always valid (for within-column reordering)
  if (sourceCol.id === targetColId) return true
  // For non-status boards, all drops are valid (no status change happens)
  if (activeBoard.value && activeBoard.value.laneType && activeBoard.value.laneType !== KanbanLaneType.Status) return true
  // If no transitions are defined, all columns are valid drop targets (open board)
  if (kanban.transitions.length === 0) return true
  return kanban.transitions.some(t => t.fromColumnId === sourceCol.id && t.toColumnId === targetColId)
}

async function onIssueDrop(e: DragEvent, targetCol: KanbanColumn) {
  e.preventDefault()
  // Ignore if this is a column drag
  if (draggedColId.value) return
  if (!draggedId.value) return
  if (!isValidDropTarget(targetCol.id)) return
  const insertIdx = dragHoverInsertIdx.value
  const sourceCol = boardColumns.value.find(c => c.issueStatus === draggedIssueStatus.value)
  const isSameColumn = sourceCol?.id === targetCol.id
  const isStatusBoard = !activeBoard.value?.laneType || activeBoard.value.laneType === KanbanLaneType.Status
  if (!isSameColumn && isStatusBoard) {
    // Status change - optimistically update local state
    await issueStore.updateIssueStatus(id, draggedId.value, targetCol.issueStatus)
  }
  // Update kanban rank via kanban store (pass position for ordering)
  await kanban.moveIssue(activeBoardId.value, draggedId.value, targetCol.id, insertIdx)
  // Update local kanbanRank to reflect new order
  const issues = issueStore.issues.filter(i => i.status === targetCol.issueStatus).sort((a, b) => a.kanbanRank - b.kanbanRank || a.createdAt.localeCompare(b.createdAt))
  const moved = issues.find(i => i.id === draggedId.value)
  if (moved) {
    // Build reordered list: all siblings without the moved item, then splice moved in at target index
    const reordered = issues.filter(i => i.id !== draggedId.value)
    reordered.splice(Math.min(insertIdx, reordered.length), 0, moved)
    // Assign sequential ranks to all items including the moved one (moved is a reference in reordered)
    reordered.forEach((issue, idx) => { issue.kanbanRank = idx })
  }
  draggedId.value = null
  draggedIssueStatus.value = null
  dragHoverColId.value = null
  dragHoverInsertIdx.value = 0
  transitionsButtonAlert.value = false
}

function onIssueDragOver(e: DragEvent, colId: string) {
  if (!draggedId.value || draggedColId.value) return
  dragHoverColId.value = colId
  // Calculate insertion index from mouse position
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

// ── Column drag & drop (main board reorder) ────────────────────────────────
function onColDragStart(e: DragEvent, colId: string) {
  draggedColId.value = colId
  e.dataTransfer!.effectAllowed = 'move'
  // Prevent issue drag handlers from firing
  e.stopPropagation()
}

function onColDragEnd() {
  draggedColId.value = null
}

function onColDragOver(e: DragEvent, _colId: string) {
  if (!draggedColId.value) return
  e.preventDefault()
}

async function onColDrop(e: DragEvent, targetColId: string) {
  if (!draggedColId.value || draggedColId.value === targetColId) {
    draggedColId.value = null
    return
  }
  e.stopPropagation()
  const cols = [...boardColumns.value]
  const fromIdx = cols.findIndex(c => c.id === draggedColId.value)
  const toIdx = cols.findIndex(c => c.id === targetColId)
  if (fromIdx === -1 || toIdx === -1) {
    draggedColId.value = null
    return
  }
  const [moved] = cols.splice(fromIdx, 1)
  cols.splice(toIdx, 0, moved)
  draggedColId.value = null
  await kanban.reorderColumns(activeBoardId.value, cols.map(c => c.id))
}

// ── Lane drag & drop (modal reorder) ──────────────────────────────────────
function onLaneDragStart(e: DragEvent, laneId: string) {
  draggedLaneId.value = laneId
  laneHoverInsertIdx.value = boardColumns.value.findIndex(c => c.id === laneId)
  e.dataTransfer!.effectAllowed = 'move'
}

function onLaneDragEnd() {
  draggedLaneId.value = null
  laneHoverInsertIdx.value = 0
}

function onLaneDragOver(e: DragEvent, _laneId: string, idx: number) {
  if (!draggedLaneId.value) return
  e.preventDefault()
  laneHoverInsertIdx.value = idx
}

async function onLaneDrop(e: DragEvent, targetLaneId: string) {
  if (!draggedLaneId.value || draggedLaneId.value === targetLaneId) {
    draggedLaneId.value = null
    laneHoverInsertIdx.value = 0
    return
  }
  e.preventDefault()
  const cols = [...boardColumns.value]
  const fromIdx = cols.findIndex(c => c.id === draggedLaneId.value)
  const toIdx = cols.findIndex(c => c.id === targetLaneId)
  if (fromIdx === -1 || toIdx === -1) {
    draggedLaneId.value = null
    laneHoverInsertIdx.value = 0
    return
  }
  const [moved] = cols.splice(fromIdx, 1)
  cols.splice(toIdx, 0, moved)
  draggedLaneId.value = null
  laneHoverInsertIdx.value = 0
  await kanban.reorderColumns(activeBoardId.value, cols.map(c => c.id))
}

// ── Quick create ──────────────────────────────────────────────────────────
function openCreateForStatus(status: IssueStatus) {
  createStatus.value = status
  createTitle.value = ''
  createPriority.value = IssuePriority.NoPriority
  showCreate.value = true
}

async function submitCreate() {
  if (!createTitle.value) return
  await issueStore.createIssue(id, {
    title: createTitle.value,
    status: createStatus.value,
    priority: createPriority.value,
    type: IssueType.Issue
  })
  showCreate.value = false
}

// ── Board actions ─────────────────────────────────────────────────────────
async function submitNewBoard() {
  if (!newBoardName.value.trim()) return
  const board = await kanban.createBoard(id, newBoardName.value.trim())
  if (board) activeBoardId.value = board.id
  newBoardName.value = ''
  showNewBoard.value = false
}

// ── Lane actions ──────────────────────────────────────────────────────────
async function submitAddColumn() {
  if (!newColName.value.trim() || !activeBoardId.value) return
  const pos = boardColumns.value.length
  await kanban.addColumn(activeBoardId.value, newColName.value.trim(), pos, newColStatus.value)
  newColName.value = ''
}

async function deleteColumn(columnId: string) {
  if (!activeBoardId.value) return
  await kanban.deleteColumn(activeBoardId.value, columnId)
}

// ── Transition actions ────────────────────────────────────────────────────
async function openTransitions() {
  if (!activeBoardId.value) return
  await kanban.fetchTransitions(activeBoardId.value)
  showTransitions.value = true
}

async function submitAddTransition() {
  if (!newTransName.value.trim() || !newTransFrom.value || !newTransTo.value || !activeBoardId.value) return
  await kanban.createTransition(activeBoardId.value, {
    name: newTransName.value.trim(),
    fromColumnId: newTransFrom.value,
    toColumnId: newTransTo.value,
    isAuto: newTransIsAuto.value,
  })
  newTransName.value = ''
  newTransFrom.value = ''
  newTransTo.value = ''
  newTransIsAuto.value = false
}

async function deleteTransition(transitionId: string) {
  if (!activeBoardId.value) return
  await kanban.deleteTransition(activeBoardId.value, transitionId)
}

function columnName(columnId: string) {
  return boardColumns.value.find(c => c.id === columnId)?.name ?? columnId
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

<style scoped>
.slide-right-enter-active,
.slide-right-leave-active {
  transition: transform 0.2s ease;
}
.slide-right-enter-from,
.slide-right-leave-to {
  transform: translateX(100%);
}
</style>
