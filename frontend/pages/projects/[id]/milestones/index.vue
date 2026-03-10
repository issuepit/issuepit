<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-3">
        <PageBreadcrumb :items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
          { label: 'Milestones', to: `/projects/${id}/milestones`, icon: 'M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9' },
        ]" />
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full">
          {{ store.milestones.length }}
        </span>
      </div>
      <div class="flex items-center gap-3">
        <!-- View toggle: List | Both | Gantt -->
        <div class="flex items-center bg-gray-800 rounded-lg p-0.5">
          <button @click="viewMode = 'list'"
            :class="['flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm transition-colors', viewMode === 'list' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16" />
            </svg>
            List
          </button>
          <button @click="viewMode = 'split'"
            :class="['flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm transition-colors', viewMode === 'split' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
            </svg>
            Both
          </button>
          <button @click="viewMode = 'gantt'" data-testid="gantt-view-button"
            :class="['flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm transition-colors', viewMode === 'gantt' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            Gantt
          </button>
        </div>
        <button @click="showCreate = true"
          class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          New Milestone
        </button>
      </div>
    </div>

    <!-- Filter tabs (list or split mode only) -->
    <div v-if="viewMode !== 'gantt'" class="flex gap-2 mb-5">
      <button v-for="tab in tabs" :key="tab.value" @click="activeTab = tab.value"
        :class="[
          'text-sm px-3 py-1.5 rounded-lg transition-colors',
          activeTab === tab.value ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200 hover:bg-gray-800/60'
        ]">
        {{ tab.label }}
        <span class="ml-1 text-xs text-gray-500">{{ tab.count }}</span>
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else>
      <!-- List Section (shown in 'list' and 'split') -->
      <div v-if="viewMode !== 'gantt'" :class="viewMode === 'split' ? 'mb-8' : ''" class="space-y-3">
        <div v-if="filteredMilestones.length === 0" class="py-16 text-center bg-gray-900 border border-gray-800 rounded-xl">
          <p class="text-gray-400">No milestones found</p>
          <button @click="showCreate = true" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
            Create the first milestone →
          </button>
        </div>

        <div v-for="milestone in filteredMilestones" :key="milestone.id"
          data-testid="milestone-row"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 transition-colors cursor-pointer"
          @click="navigateTo(`/projects/${id}/milestones/${milestone.id}`)">
          <div class="flex items-start justify-between gap-4">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1">
                <span :class="milestone.status === 'open' ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
                  class="text-xs px-2 py-0.5 rounded-full font-medium">
                  {{ milestone.status === 'open' ? 'Open' : 'Closed' }}
                </span>
                <span class="milestone-row-title text-base font-semibold text-white hover:text-brand-300 transition-colors">
                  {{ milestone.title }}
                </span>
              </div>
              <p v-if="milestone.description" class="text-sm text-gray-400 mt-1 line-clamp-2">
                {{ milestone.description }}
              </p>
              <div class="flex items-center gap-4 mt-2 text-xs text-gray-500">
                <span v-if="milestone.startDate">
                  Start {{ formatDate(milestone.startDate) }}
                </span>
                <span v-if="milestone.dueDate">
                  Due {{ formatDate(milestone.dueDate) }}
                </span>
                <span>Created {{ formatDate(milestone.createdAt) }}</span>
              </div>
            </div>
            <div class="flex items-center gap-2 shrink-0">
              <button @click.stop="openEdit(milestone)"
                class="text-gray-500 hover:text-gray-300 transition-colors p-1.5 rounded hover:bg-gray-800">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                </svg>
              </button>
              <button @click.stop="confirmDelete(milestone.id)"
                class="text-gray-500 hover:text-red-400 transition-colors p-1.5 rounded hover:bg-red-900/20">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Split-mode divider -->
      <div v-if="viewMode === 'split'" class="flex items-center gap-3 mb-5">
        <div class="flex-1 border-t border-gray-800"></div>
        <span class="text-xs text-gray-600 uppercase tracking-wider font-medium">Gantt</span>
        <div class="flex-1 border-t border-gray-800"></div>
      </div>

      <!-- Gantt Section (shown in 'gantt' and 'split') -->
      <div v-if="viewMode !== 'list'" class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div v-if="store.milestones.length === 0" class="py-16 text-center">
          <p class="text-gray-400">No milestones yet</p>
          <button @click="showCreate = true" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
            Create the first milestone →
          </button>
        </div>

        <div v-else class="overflow-x-auto">
          <!-- Month header -->
          <div class="flex border-b border-gray-800" style="min-width: 800px">
            <div class="w-48 shrink-0 px-2 py-2 text-xs text-gray-500 font-medium border-r border-gray-800">Milestone</div>
            <div class="flex-1 flex h-8">
              <div v-for="month in ganttMonths" :key="month.label"
                :style="{ width: month.widthPct + '%' }"
                class="border-r border-gray-800 flex items-center px-1.5 text-xs text-gray-500 font-medium shrink-0">
                {{ month.label }}
              </div>
            </div>
          </div>

          <!-- Milestone rows -->
          <div style="min-width: 800px">
              <div v-for="milestone in store.milestones" :key="milestone.id"
              class="flex border-b border-gray-800/50 hover:bg-gray-800/20 transition-colors"
              style="height: 48px">
              <!-- Label column -->
              <div class="w-48 shrink-0 flex items-center px-2 border-r border-gray-800 gap-1.5">
                <span :class="milestone.status === 'open' ? 'bg-green-500' : 'bg-gray-600'"
                  class="w-2 h-2 rounded-full shrink-0"></span>
                <button class="text-xs text-gray-300 hover:text-white transition-colors truncate text-left"
                  data-testid="gantt-label-btn"
                  @click="navigateTo(`/projects/${id}/milestones/${milestone.id}`)">
                  {{ milestone.title }}
                </button>
              </div>
              <!-- Bar area -->
              <div class="bar-area-container flex-1 relative overflow-hidden">
                <!-- Today line -->
                <div class="absolute top-0 bottom-0 w-px bg-brand-500/60 z-10 pointer-events-none"
                  :style="{ left: todayPct + '%' }"></div>
                <!-- Grid lines for months -->
                <div v-for="month in ganttMonths" :key="month.label"
                  class="absolute top-0 bottom-0 w-px bg-gray-800/80 pointer-events-none"
                  :style="{ left: month.leftPct + '%' }"></div>
                <!-- Milestone bar (draggable) -->
                <div v-if="ganttBarDisplay(milestone)"
                  class="absolute top-[8px] h-8 rounded flex items-stretch select-none"
                  :class="[
                    milestone.status === 'open' ? 'bg-indigo-600' : 'bg-gray-600',
                    dragging?.milestoneId === milestone.id ? 'opacity-80 shadow-lg z-20' : 'hover:opacity-90 z-10',
                  ]"
                  :style="{
                    left: ganttBarDisplay(milestone)!.left + '%',
                    width: Math.max(0.5, ganttBarDisplay(milestone)!.width) + '%',
                    minWidth: '4px',
                  }"
                  :title="`${milestone.title}${milestone.dueDate ? ' · Due ' + formatDate(milestone.dueDate) : ''}`"
                  @mousedown="startBarDrag($event, milestone, 'move')">
                  <!-- Left resize handle -->
                  <div
                    class="w-2 shrink-0 cursor-ew-resize rounded-l hover:bg-black/30 z-10"
                    @mousedown.stop="startBarDrag($event, milestone, 'resize-left')">
                  </div>
                  <!-- Label (click to navigate when not dragging) -->
                  <span v-if="ganttBarDisplay(milestone)!.width > 6"
                    class="flex-1 flex items-center px-2 text-xs text-white font-medium truncate cursor-move"
                    @mousedown.stop="startBarDrag($event, milestone, 'move')">
                    {{ milestone.title }}
                  </span>
                  <!-- Right resize handle -->
                  <div
                    class="w-2 shrink-0 cursor-ew-resize rounded-r hover:bg-black/30 z-10"
                    @mousedown.stop="startBarDrag($event, milestone, 'resize-right')">
                  </div>
                </div>
                <!-- No-date indicator -->
                <div v-else class="absolute flex items-center text-xs text-gray-600 italic"
                  style="top: 8px"
                  :style="{ left: todayPct + '%', transform: 'translateX(4px)' }">
                  no dates
                </div>
              </div>
            </div>
          </div>

          <!-- Legend -->
          <div class="flex items-center gap-6 px-4 py-3 border-t border-gray-800">
            <div class="flex items-center gap-1.5 text-xs text-gray-500">
              <span class="w-3 h-3 rounded bg-indigo-600 block"></span> Open
            </div>
            <div class="flex items-center gap-1.5 text-xs text-gray-500">
              <span class="w-3 h-3 rounded bg-gray-600 block"></span> Closed
            </div>
            <div class="flex items-center gap-1.5 text-xs text-gray-500">
              <span class="w-px h-3 bg-brand-500/60 block"></span> Today
            </div>
            <div class="ml-auto text-xs text-gray-600 italic">Drag bars to adjust dates · Click label to open</div>
          </div>
        </div>
      </div>
    </template>

    <!-- Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">New Milestone</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="form.title" type="text" placeholder="Milestone title"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="form.description" rows="3" placeholder="Describe this milestone..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Start Date</label>
              <input v-model="form.startDate" type="date"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Due Date</label>
              <input v-model="form.dueDate" type="date"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Milestone
          </button>
          <button @click="showCreate = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Edit Modal -->
    <div v-if="editMilestone" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Edit Milestone</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="editForm.title" type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="editForm.description" rows="3"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Start Date</label>
              <input v-model="editForm.startDate" type="date"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Due Date</label>
              <input v-model="editForm.dueDate" type="date"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Status</label>
            <select v-model="editForm.status"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option value="open">Open</option>
              <option value="closed">Closed</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitEdit"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Save Changes
          </button>
          <button @click="editMilestone = null"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Milestone } from '~/types'
import { useMilestonesStore } from '~/stores/milestones'
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const id = route.params.id as string
const store = useMilestonesStore()
const projectsStore = useProjectsStore()

const showCreate = ref(false)
const activeTab = ref<'all' | 'open' | 'closed'>('open')
const viewMode = ref<'list' | 'split' | 'gantt'>('list')
const editMilestone = ref<Milestone | null>(null)

const form = reactive({ title: '', description: '', startDate: '', dueDate: '' })
const editForm = reactive({ title: '', description: '', startDate: '', dueDate: '', status: 'open' as 'open' | 'closed' })

const openCount = computed(() => store.milestones.filter(m => m.status === 'open').length)
const closedCount = computed(() => store.milestones.filter(m => m.status === 'closed').length)

const tabs = computed(() => [
  { value: 'open' as const, label: 'Open', count: openCount.value },
  { value: 'closed' as const, label: 'Closed', count: closedCount.value },
  { value: 'all' as const, label: 'All', count: store.milestones.length },
])

const filteredMilestones = computed(() => {
  if (activeTab.value === 'open') return store.milestones.filter(m => m.status === 'open')
  if (activeTab.value === 'closed') return store.milestones.filter(m => m.status === 'closed')
  return store.milestones
})

// ── Gantt chart helpers ──────────────────────────────────────────

const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000
const GANTT_PADDING_RATIO = 0.15

/** Returns the effective start timestamp for a milestone (startDate → createdAt fallback). */
function milestoneStartTime(m: Milestone): number {
  return new Date(m.startDate ?? m.createdAt).getTime()
}

/** Returns the effective end timestamp for a milestone (dueDate → start + 7 days fallback). */
function milestoneEndTime(m: Milestone): number {
  if (m.dueDate) return new Date(m.dueDate).getTime()
  return milestoneStartTime(m) + SEVEN_DAYS_MS
}

const ganttRange = computed(() => {
  if (!store.milestones.length) return { min: Date.now(), max: Date.now() }
  const starts = store.milestones.map(milestoneStartTime)
  const ends = store.milestones.map(milestoneEndTime)
  const minTs = Math.min(...starts)
  const maxTs = Math.max(...ends)
  const pad = (maxTs - minTs) * GANTT_PADDING_RATIO || SEVEN_DAYS_MS
  return { min: minTs - pad, max: maxTs + pad }
})

const todayPct = computed(() => {
  const { min, max } = ganttRange.value
  return Math.max(0, Math.min(100, ((Date.now() - min) / (max - min)) * 100))
})

const ganttMonths = computed(() => {
  const { min, max } = ganttRange.value
  const months: { label: string; leftPct: number; widthPct: number }[] = []
  const cur = new Date(min)
  cur.setDate(1)
  cur.setHours(0, 0, 0, 0)
  const total = max - min
  while (cur.getTime() < max) {
    const nextMonth = new Date(cur.getFullYear(), cur.getMonth() + 1, 1)
    const left = Math.max(0, (cur.getTime() - min) / total * 100)
    const right = Math.min(100, (nextMonth.getTime() - min) / total * 100)
    months.push({
      label: cur.toLocaleDateString('en-US', { month: 'short', year: '2-digit' }),
      leftPct: left,
      widthPct: right - left,
    })
    cur.setMonth(cur.getMonth() + 1)
  }
  return months
})

/** Compute bar position from a milestone's stored dates. */
function ganttBar(m: Milestone) {
  const { min, max } = ganttRange.value
  const total = max - min
  if (total <= 0) return null
  const left = ((milestoneStartTime(m) - min) / total) * 100
  const width = ((milestoneEndTime(m) - milestoneStartTime(m)) / total) * 100
  return { left, width }
}

// ── Drag-and-drop ────────────────────────────────────────────────

interface DragState {
  milestoneId: string
  type: 'move' | 'resize-left' | 'resize-right'
  startX: number
  originalBarLeft: number
  originalBarWidth: number
  containerWidth: number
}

const dragging = ref<DragState | null>(null)
/** True once the pointer has moved enough to count as a drag (vs a click). */
const wasDragged = ref(false)
/** Per-milestone bar overrides while dragging; keyed by milestone id. */
const dragPreview = reactive<Record<string, { left: number; width: number } | undefined>>({})

/** Bar position to render — overridden during drag. */
function ganttBarDisplay(m: Milestone) {
  return dragPreview[m.id] ?? ganttBar(m)
}

function startBarDrag(event: MouseEvent, m: Milestone, type: DragState['type']) {
  event.preventDefault()
  const containerEl = (event.currentTarget as HTMLElement).closest<HTMLElement>('.bar-area-container')
  if (!containerEl) return
  const bar = ganttBar(m)
  if (!bar) return

  wasDragged.value = false
  dragging.value = {
    milestoneId: m.id,
    type,
    startX: event.clientX,
    originalBarLeft: bar.left,
    originalBarWidth: bar.width,
    containerWidth: containerEl.getBoundingClientRect().width,
  }

  document.addEventListener('mousemove', onDragMove)
  document.addEventListener('mouseup', onDragEnd)
}

function onDragMove(event: MouseEvent) {
  if (!dragging.value) return
  const d = dragging.value
  const deltaX = event.clientX - d.startX
  if (Math.abs(deltaX) > 3) wasDragged.value = true

  const deltaPct = (deltaX / d.containerWidth) * 100

  if (d.type === 'move') {
    dragPreview[d.milestoneId] = {
      left: d.originalBarLeft + deltaPct,
      width: d.originalBarWidth,
    }
  } else if (d.type === 'resize-right') {
    dragPreview[d.milestoneId] = {
      left: d.originalBarLeft,
      width: Math.max(0.5, d.originalBarWidth + deltaPct), // 0.5% minimum width to keep bar visible
    }
  } else {
    // resize-left: allow dragging outside the visible range, same as resize-right; only prevent collapsing below min width
    const minBarWidth = 0.5
    const newLeft = Math.min(d.originalBarLeft + d.originalBarWidth - minBarWidth, d.originalBarLeft + deltaPct)
    dragPreview[d.milestoneId] = {
      left: newLeft,
      width: d.originalBarLeft + d.originalBarWidth - newLeft,
    }
  }
}

async function onDragEnd(_event: MouseEvent) {
  document.removeEventListener('mousemove', onDragMove)
  document.removeEventListener('mouseup', onDragEnd)

  if (!dragging.value) return
  const d = dragging.value

  if (!wasDragged.value) {
    // Plain click on bar → navigate to detail
    dragging.value = null
    navigateTo(`/projects/${id}/milestones/${d.milestoneId}`)
    return
  }

  const preview = dragPreview[d.milestoneId]
  const milestone = store.milestones.find(m => m.id === d.milestoneId)

  if (preview && milestone) {
    const { min, max } = ganttRange.value
    const totalMs = max - min
    const pctToDate = (pct: number) =>
      new Date(min + (pct / 100) * totalMs).toISOString().split('T')[0]

    let newStartDate = milestone.startDate
    let newDueDate = milestone.dueDate

    if (d.type === 'move') {
      newStartDate = pctToDate(preview.left)
      newDueDate = pctToDate(preview.left + preview.width)
    } else if (d.type === 'resize-left') {
      newStartDate = pctToDate(preview.left)
    } else {
      newDueDate = pctToDate(preview.left + preview.width)
    }

    await store.updateMilestone(id, milestone.id, {
      title: milestone.title,
      description: milestone.description,
      startDate: newStartDate,
      dueDate: newDueDate,
      status: milestone.status,
    })
  }

  dragPreview[d.milestoneId] = undefined
  dragging.value = null
  wasDragged.value = false
}

// ── Lifecycle ────────────────────────────────────────────────────

onMounted(() => {
  // Default to split view on large screens (height ≥ 900px)
  if (window.innerHeight >= 900) viewMode.value = 'split'

  projectsStore.fetchProject(id)
  store.fetchMilestones(id)
})

onBeforeUnmount(() => {
  document.removeEventListener('mousemove', onDragMove)
  document.removeEventListener('mouseup', onDragEnd)
})

// ── CRUD ─────────────────────────────────────────────────────────

async function submitCreate() {
  if (!form.title) return
  await store.createMilestone(id, {
    title: form.title,
    description: form.description || undefined,
    startDate: form.startDate || undefined,
    dueDate: form.dueDate || undefined,
  })
  showCreate.value = false
  Object.assign(form, { title: '', description: '', startDate: '', dueDate: '' })
}

function openEdit(milestone: Milestone) {
  editMilestone.value = milestone
  editForm.title = milestone.title
  editForm.description = milestone.description ?? ''
  editForm.startDate = milestone.startDate ? milestone.startDate.split('T')[0] : ''
  editForm.dueDate = milestone.dueDate ? milestone.dueDate.split('T')[0] : ''
  editForm.status = milestone.status
}

async function submitEdit() {
  if (!editMilestone.value || !editForm.title) return
  await store.updateMilestone(id, editMilestone.value.id, {
    title: editForm.title,
    description: editForm.description || undefined,
    startDate: editForm.startDate || undefined,
    dueDate: editForm.dueDate || undefined,
    status: editForm.status,
  })
  editMilestone.value = null
}

async function confirmDelete(milestoneId: string) {
  if (!confirm('Delete this milestone?')) return
  await store.deleteMilestone(id, milestoneId)
}

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>
