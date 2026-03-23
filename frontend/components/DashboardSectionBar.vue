<template>
  <div data-no-reorder class="mb-1.5 rounded-lg bg-gray-900/60 border border-gray-800 px-2 py-1.5">
    <!-- Main row -->
    <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
      <!-- Drag handle + label -->
      <div class="flex items-center gap-1.5 text-xs text-amber-400/80 cursor-grab mr-auto">
        <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
          <circle cx="9" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/>
          <circle cx="15" cy="6" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="15" cy="18" r="1.5"/>
        </svg>
        <span class="font-semibold">{{ label }}</span>
      </div>
      <!-- Display mode pills -->
      <div v-if="displayModes?.length" class="flex items-center gap-0.5">
        <button
          v-for="mode in displayModes" :key="mode"
          @click.stop="$emit('display-mode-change', mode)"
          :class="currentDisplayMode === mode ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors capitalize">{{ mode }}</button>
      </div>
      <!-- Max items -->
      <div v-if="hasMaxItems && currentDisplayMode !== 'count'" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-600">#</span>
        <button
          v-for="n in maxItemsOptions" :key="n"
          @click.stop="$emit('max-items-change', n)"
          :class="currentMaxItems === n ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs w-5 h-5 flex items-center justify-center rounded transition-colors">{{ n }}</button>
      </div>
      <!-- Width buttons (fraction SVG icons: diagonal-slash inline fraction) -->
      <div v-if="widths.length" class="flex items-center gap-0.5">
        <button
          v-for="w in widths" :key="w.value"
          @click.stop="$emit('width-change', w.value)"
          :title="w.label"
          :class="currentWidth === w.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="px-1 py-0.5 rounded transition-colors flex items-center justify-center">
          <svg width="18" height="14" viewBox="0 0 18 14" class="shrink-0">
            <!-- Numerator top-left -->
            <text x="1" y="7" text-anchor="start" font-size="7" font-family="system-ui,sans-serif" fill="currentColor">{{ fractionParts(w.label).num }}</text>
            <!-- Diagonal slash -->
            <line x1="4" y1="13" x2="14" y2="1" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
            <!-- Denominator bottom-right -->
            <text x="17" y="13" text-anchor="end" font-size="7" font-family="system-ui,sans-serif" fill="currentColor">{{ fractionParts(w.label).den }}</text>
          </svg>
        </button>
      </div>
      <!-- Settings cog (chart or kanban settings) -->
      <button
        v-if="hasSettings"
        @click.stop="showSettings = !showSettings"
        :class="showSettings ? 'text-brand-400 bg-gray-700' : 'text-gray-500 hover:text-gray-300'"
        class="text-xs px-1 py-0.5 rounded bg-gray-800 hover:bg-gray-700 transition-colors"
        title="Section settings">
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
      </button>
      <!-- Tab with next (click or drop) -->
      <button
        v-if="canTab"
        @click.stop="$emit('tab-toggle')"
        @dragover.prevent="tabDragOver = true"
        @dragleave="tabDragOver = false"
        @drop.prevent="onTabDrop"
        class="text-xs px-1.5 py-0.5 rounded transition-colors bg-gray-800 hover:bg-gray-700"
        :class="[
          isTabbed ? 'text-brand-400' : 'text-gray-500 hover:text-gray-300',
          tabDragOver || dragHover ? 'ring-2 ring-brand-400 text-brand-300 bg-gray-700' : '',
        ]"
        :title="isTabbed ? 'Ungroup from next' : 'Combine with next as tabs (or drop a section here)'">
        {{ isTabbed ? '⊖ Ungroup' : '⊕ Tab with ↓' }}
      </button>
      <!-- Stack with next (click or drop) -->
      <button
        v-if="canStack"
        @click.stop="$emit('stack-toggle')"
        @dragover.prevent="stackDragOver = true"
        @dragleave="stackDragOver = false"
        @drop.prevent="onStackDrop"
        class="text-xs px-1.5 py-0.5 rounded transition-colors bg-gray-800 hover:bg-gray-700"
        :class="[
          isStacked ? 'text-teal-400' : 'text-gray-500 hover:text-gray-300',
          stackDragOver || dragHover ? 'ring-2 ring-teal-400 text-teal-300 bg-gray-700' : '',
        ]"
        :title="isStacked ? 'Unstack from next' : 'Stack with next section (or drop a section here)'">
        {{ isStacked ? '⊖ Unstack' : '⇕ Stack with ↓' }}
      </button>
      <!-- Hide/Show -->
      <button
        @click.stop="$emit(hidden ? 'show' : 'hide')"
        :class="hidden ? 'text-green-400' : 'text-gray-400 hover:text-red-400'"
        class="text-xs px-1.5 py-0.5 rounded bg-gray-800 hover:bg-gray-700 transition-colors">
        {{ hidden ? '+ Show' : '✕ Hide' }}
      </button>
      <!-- Remove (for dynamically added sections) -->
      <button
        v-if="canRemove"
        @click.stop="$emit('remove')"
        class="text-xs px-1.5 py-0.5 rounded bg-gray-800 hover:bg-gray-700 text-red-500 hover:text-red-300 transition-colors"
        title="Remove this section from the dashboard">
        ⊗ Remove
      </button>
    </div>

    <!-- Settings panel (chart days/height, kanban board, test history chart settings) -->
    <div v-if="showSettings && hasSettings" class="mt-1.5 pt-1.5 border-t border-gray-700/50 flex flex-wrap items-center gap-x-4 gap-y-1.5">
      <!-- Chart days (number input) -->
      <div v-if="currentChartDays !== undefined" class="flex items-center gap-1.5">
        <span class="text-xs text-gray-500">Days</span>
        <input
          type="number"
          :min="CHART_DAYS_MIN"
          :max="CHART_DAYS_MAX"
          :value="currentChartDays"
          @change.stop="onChartDaysChange"
          class="w-14 text-xs bg-gray-800 border border-gray-700 rounded px-1.5 py-0.5 text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500" />
      </div>
      <!-- Chart height buttons -->
      <div v-if="chartHeightOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">Height</span>
        <button
          v-for="h in chartHeightOptions" :key="h.value"
          @click.stop="$emit('chart-height-change', h.value)"
          :class="currentChartHeight === h.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ h.label }}</button>
      </div>
      <!-- Kanban board selector -->
      <div v-if="kanbanBoards?.length" class="flex items-center gap-1.5">
        <span class="text-xs text-gray-500">Board</span>
        <select
          :value="selectedKanbanBoardId"
          @change.stop="onBoardChange"
          class="text-xs bg-gray-800 border border-gray-700 rounded px-1.5 py-0.5 text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option v-for="b in kanbanBoards" :key="b.id" :value="b.id">{{ b.name }}</option>
        </select>
      </div>
      <!-- Test history branch filter -->
      <div v-if="testHistoryBranches !== undefined" class="flex items-center gap-1.5">
        <span class="text-xs text-gray-500">Branch</span>
        <select
          :value="currentTestHistoryBranch ?? ''"
          @change.stop="onBranchChange"
          class="text-xs bg-gray-800 border border-gray-700 rounded px-1.5 py-0.5 text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option value="">All</option>
          <option v-for="b in testHistoryBranches" :key="b" :value="b">{{ b }}</option>
        </select>
      </div>
      <!-- Test history color mode -->
      <div v-if="testHistoryColorModeOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">Color</span>
        <button
          v-for="c in testHistoryColorModeOptions" :key="c.value"
          @click.stop="$emit('test-history-color-mode-change', c.value)"
          :class="currentTestHistoryColorMode === c.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ c.label }}</button>
      </div>
      <!-- Test history Y axis -->
      <div v-if="testHistoryYAxisOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">Y</span>
        <button
          v-for="y in testHistoryYAxisOptions" :key="y.value"
          @click.stop="$emit('test-history-y-axis-change', y.value)"
          :class="currentTestHistoryYAxis === y.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ y.label }}</button>
      </div>
      <!-- Test history X axis mode -->
      <div v-if="testHistoryXModeOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">X</span>
        <button
          v-for="x in testHistoryXModeOptions" :key="x.value"
          @click.stop="$emit('test-history-x-mode-change', x.value)"
          :class="currentTestHistoryXMode === x.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ x.label }}</button>
      </div>
      <!-- Sort by -->
      <div v-if="sortByOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">Sort</span>
        <button
          v-for="s in sortByOptions" :key="s.value"
          @click.stop="$emit('sort-by-change', s.value)"
          :class="currentSortBy === s.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ s.label }}</button>
      </div>
      <!-- Project filter -->
      <div v-if="projectOptions?.length" class="flex items-center gap-1.5">
        <span class="text-xs text-gray-500">Project</span>
        <select
          :value="currentProjectFilter ?? ''"
          @change.stop="onProjectFilterChange"
          class="text-xs bg-gray-800 border border-gray-700 rounded px-1.5 py-0.5 text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option value="">All</option>
          <option v-for="p in projectOptions" :key="p.id" :value="p.id">{{ p.name }}</option>
        </select>
      </div>
      <!-- Max per project -->
      <div v-if="maxPerProjectOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">Max/proj</span>
        <button
          v-for="n in maxPerProjectOptions" :key="n"
          @click.stop="$emit('max-per-project-change', n)"
          :class="currentMaxPerProject === n ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ n === 0 ? '∞' : n }}</button>
      </div>
      <!-- Failed hours -->
      <div v-if="failedHoursOptions?.length" class="flex items-center gap-0.5">
        <span class="text-xs text-gray-500 mr-1">Failed in</span>
        <button
          v-for="h in failedHoursOptions" :key="h"
          @click.stop="$emit('failed-hours-change', h)"
          :class="currentFailedHours === h ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
          class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ formatFailedHours(h) }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  label: string
  displayModes?: string[]
  currentDisplayMode?: string
  hasMaxItems?: boolean
  maxItemsOptions: number[]
  currentMaxItems?: number
  widths: { value: string; label: string }[]
  currentWidth: string
  currentChartDays?: number
  chartHeightOptions?: { value: string; label: string }[]
  currentChartHeight?: string
  kanbanBoards?: { id: string; name: string }[]
  selectedKanbanBoardId?: string
  testHistoryBranches?: string[]
  currentTestHistoryBranch?: string | null
  testHistoryColorModeOptions?: { value: string; label: string }[]
  currentTestHistoryColorMode?: string
  testHistoryYAxisOptions?: { value: string; label: string }[]
  currentTestHistoryYAxis?: string
  testHistoryXModeOptions?: { value: string; label: string }[]
  currentTestHistoryXMode?: string
  sortByOptions?: { value: string; label: string }[]
  currentSortBy?: string
  projectOptions?: { id: string; name: string }[]
  currentProjectFilter?: string | null
  maxPerProjectOptions?: number[]
  currentMaxPerProject?: number
  failedHoursOptions?: number[]
  currentFailedHours?: number
  canTab?: boolean
  isTabbed?: boolean
  canStack?: boolean
  isStacked?: boolean
  hidden?: boolean
  dragHover?: boolean
  canRemove?: boolean
}>()

const emit = defineEmits<{
  'display-mode-change': [mode: string]
  'max-items-change': [n: number]
  'width-change': [value: string]
  'chart-days-change': [days: number]
  'chart-height-change': [key: string]
  'kanban-board-change': [boardId: string]
  'test-history-branch-change': [branch: string | null]
  'test-history-color-mode-change': [mode: string]
  'test-history-y-axis-change': [axis: string]
  'test-history-x-mode-change': [mode: string]
  'sort-by-change': [value: string]
  'project-filter-change': [projectId: string | null]
  'max-per-project-change': [n: number]
  'failed-hours-change': [hours: number]
  'tab-toggle': []
  'tab-drop': [droppedSid: string]
  'stack-toggle': []
  'stack-drop': [droppedSid: string]
  hide: []
  show: []
  remove: []
}>()

const showSettings = ref(false)
const tabDragOver = ref(false)
const stackDragOver = ref(false)

/** Convert a failedHours number to a short display label. 0 means "all time". */
function formatFailedHours(h: number): string {
  if (h === 0) return '∞'
  if (h === 168) return '7d'
  return `${h}h`
}

/** Split a fraction label like "1/12" into { num, den }. "Full" maps to { num:"1", den:"1" }. */
function fractionParts(label: string): { num: string; den: string } {
  if (label === 'Full') return { num: '1', den: '1' }
  const slash = label.indexOf('/')
  if (slash === -1) return { num: '1', den: '1' }
  return { num: label.slice(0, slash), den: label.slice(slash + 1) }
}

const CHART_DAYS_MIN = 7
const CHART_DAYS_MAX = 60

const hasSettings = computed(() =>
  props.currentChartDays !== undefined ||
  (props.chartHeightOptions?.length ?? 0) > 0 ||
  props.kanbanBoards !== undefined ||
  props.testHistoryBranches !== undefined ||
  (props.testHistoryColorModeOptions?.length ?? 0) > 0 ||
  (props.testHistoryXModeOptions?.length ?? 0) > 0 ||
  (props.sortByOptions?.length ?? 0) > 0 ||
  (props.projectOptions?.length ?? 0) > 0 ||
  (props.maxPerProjectOptions?.length ?? 0) > 0 ||
  (props.failedHoursOptions?.length ?? 0) > 0,
)

function onChartDaysChange(e: Event) {
  const v = parseInt((e.target as HTMLInputElement).value)
  if (!isNaN(v)) emit('chart-days-change', Math.min(CHART_DAYS_MAX, Math.max(CHART_DAYS_MIN, v)))
}

function onBoardChange(e: Event) {
  const v = (e.target as HTMLSelectElement).value
  if (v) emit('kanban-board-change', v)
}

function onBranchChange(e: Event) {
  const v = (e.target as HTMLSelectElement).value
  emit('test-history-branch-change', v || null)
}

function onProjectFilterChange(e: Event) {
  const v = (e.target as HTMLSelectElement).value
  emit('project-filter-change', v || null)
}

function onTabDrop(e: DragEvent) {
  tabDragOver.value = false
  const sid = e.dataTransfer?.getData('text/plain')
  if (sid) emit('tab-drop', sid)
}

function onStackDrop(e: DragEvent) {
  stackDragOver.value = false
  const sid = e.dataTransfer?.getData('text/plain')
  if (sid) emit('stack-drop', sid)
}
</script>
