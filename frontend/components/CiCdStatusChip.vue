<template>
  <div class="relative inline-flex" @mouseenter="onChipEnter" @mouseleave="onChipLeave">
    <!-- Status chip -->
    <span :class="chipClass" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium cursor-default select-none">
      <span :class="dotClass" class="w-1.5 h-1.5 rounded-full" />
      {{ label }}
    </span>

    <!-- Tooltip -->
    <Teleport to="body">
      <div
        v-if="tooltipVisible && runs.length"
        ref="tooltipEl"
        :style="tooltipStyle"
        class="fixed z-50 bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/60 p-3 min-w-[260px] max-w-xs"
        @mouseenter="onTooltipEnter"
        @mouseleave="onTooltipLeave">
        <p class="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wide">CI/CD Runs</p>
        <div class="space-y-1.5">
          <button
            v-for="run in runs"
            :key="run.id"
            class="flex items-center gap-2 w-full text-left rounded-lg px-2 py-1.5 transition-colors hover:bg-gray-800 cursor-pointer"
            @click.stop="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)"
            @mouseenter="onRunItemEnter(run, $event)"
            @mouseleave="onRunItemLeave">
            <span :class="runDotClass(run.status)" class="w-2 h-2 rounded-full shrink-0" />
            <div class="flex-1 min-w-0">
              <p class="text-xs text-gray-200 truncate font-medium">{{ run.workflow || run.branch || 'Run' }}</p>
              <p class="text-xs text-gray-500 truncate font-mono">
                {{ run.commitSha?.slice(0, 7) || '—' }}
                <span v-if="run.branch"> · {{ run.branch }}</span>
                <span v-if="run.eventName" class="ml-1 text-gray-600">({{ run.eventName }})</span>
              </p>
            </div>
            <div class="flex flex-col items-end shrink-0 gap-0.5">
              <span :class="runChipClass(run.status)" class="text-xs px-1.5 py-0.5 rounded-full font-medium">{{ run.statusName }}</span>
              <span class="text-xs text-gray-600">{{ relativeTime(run.startedAt) }}</span>
            </div>
          </button>
        </div>
      </div>

      <!-- Sub-tooltip: mini job graph shown when hovering a run item -->
      <div
        v-if="subTooltipVisible && hoveredRunId"
        :style="subTooltipStyle"
        class="fixed z-[60] bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/60 p-3"
        @mouseenter="onSubTooltipEnter"
        @mouseleave="onSubTooltipLeave">
        <p class="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wide">Job Graph</p>
        <div v-if="graphLoading" class="text-xs text-gray-500 py-2 text-center">Loading…</div>
        <div v-else-if="!miniGraphColumns.length" class="text-xs text-gray-500 py-2 text-center italic">No graph data</div>
        <div v-else class="flex items-start gap-1.5 overflow-x-auto max-w-[340px]">
          <template v-for="(col, colIdx) in miniGraphColumns" :key="colIdx">
            <div v-if="colIdx > 0" class="flex items-center shrink-0 self-center px-0.5">
              <svg class="w-3 h-2.5 text-gray-600" viewBox="0 0 12 10" fill="none" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M0 5h8M5 1.5l4 3.5-4 3.5" />
              </svg>
            </div>
            <div class="flex flex-col gap-1 shrink-0">
              <div
                v-for="job in col"
                :key="job.id"
                class="text-[10px] text-gray-300 bg-gray-800 border border-gray-700 rounded px-1.5 py-0.5 max-w-[96px] truncate leading-tight"
                :title="job.name">
                {{ job.name }}
              </div>
            </div>
          </template>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { CiCdRunStatus, type CiCdRun, type WorkflowGraph, type WorkflowJobNode } from '~/types'

const props = withDefaults(defineProps<{
  runs: CiCdRun[]
  /** If true, runs in the tooltip are clickable links to run details. Default: true (kept for backward compat, tooltip items are always clickable) */
  runLink?: boolean
}>(), {
  runLink: true,
})

const api = useApi()

// ── Derived status from all runs ──────────────────────────────────────────────

const summaryStatus = computed((): CiCdRunStatus => {
  if (!props.runs.length) return CiCdRunStatus.Pending
  // Use the status of the most recent run (sorted by startedAt descending)
  const lastRun = [...props.runs].sort(
    (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime(),
  )[0]
  return lastRun.status
})

const label = computed(() => {
  const s = summaryStatus.value
  const labels: Record<CiCdRunStatus, string> = {
    [CiCdRunStatus.Pending]: 'Pending',
    [CiCdRunStatus.Running]: 'Running',
    [CiCdRunStatus.Succeeded]: 'Succeeded',
    [CiCdRunStatus.Failed]: 'Failed',
    [CiCdRunStatus.Cancelled]: 'Cancelled',
    [CiCdRunStatus.WaitingForApproval]: 'Waiting for Approval',
    [CiCdRunStatus.SucceededWithWarnings]: 'Succeeded with Warnings',
  }
  return labels[s] ?? 'Unknown'
})

const chipClass = computed(() => {
  switch (summaryStatus.value) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    case CiCdRunStatus.WaitingForApproval: return 'bg-purple-900/30 text-purple-400'
    case CiCdRunStatus.SucceededWithWarnings: return 'bg-yellow-900/30 text-yellow-400'
    default: return 'bg-gray-800 text-gray-400'
  }
})

const dotClass = computed(() => {
  switch (summaryStatus.value) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    case CiCdRunStatus.WaitingForApproval: return 'bg-purple-400'
    case CiCdRunStatus.SucceededWithWarnings: return 'bg-yellow-400'
    default: return 'bg-gray-500'
  }
})

function runChipClass(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    case CiCdRunStatus.WaitingForApproval: return 'bg-purple-900/30 text-purple-400'
    case CiCdRunStatus.SucceededWithWarnings: return 'bg-yellow-900/30 text-yellow-400'
    default: return 'bg-gray-800 text-gray-400'
  }
}

function runDotClass(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    case CiCdRunStatus.WaitingForApproval: return 'bg-purple-400'
    case CiCdRunStatus.SucceededWithWarnings: return 'bg-yellow-400'
    default: return 'bg-gray-500'
  }
}

// ── Main tooltip positioning & visibility ─────────────────────────────────────

const tooltipVisible = ref(false)
const tooltipEl = ref<HTMLElement | null>(null)
const tooltipStyle = ref<Record<string, string>>({})
let hideTimer: ReturnType<typeof setTimeout> | null = null

const RUN_ITEM_HEIGHT = 60
const TOOLTIP_HEADER_HEIGHT = 48
const MAX_TOOLTIP_HEIGHT = 320

function onChipEnter(e: MouseEvent) {
  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
  positionTooltip(e)
  tooltipVisible.value = true
}

function onChipLeave() {
  hideTimer = setTimeout(() => { tooltipVisible.value = false }, 150)
}

function onTooltipEnter() {
  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
}

function onTooltipLeave() {
  hideTimer = setTimeout(() => {
    tooltipVisible.value = false
    subTooltipVisible.value = false
  }, 150)
}

onUnmounted(() => {
  if (hideTimer) clearTimeout(hideTimer)
  if (subHideTimer) clearTimeout(subHideTimer)
})

function positionTooltip(e: MouseEvent) {
  const target = e.currentTarget as HTMLElement
  const rect = target.getBoundingClientRect()
  const vpW = window.innerWidth
  const vpH = window.innerHeight
  const tooltipW = 280
  const tooltipH = Math.min(props.runs.length * RUN_ITEM_HEIGHT + TOOLTIP_HEADER_HEIGHT, MAX_TOOLTIP_HEIGHT)

  let left = rect.left
  let top = rect.bottom + 6

  // Flip left if it would overflow right
  if (left + tooltipW > vpW - 8) left = vpW - tooltipW - 8
  if (left < 8) left = 8

  // Flip above if it would overflow bottom
  if (top + tooltipH > vpH - 8) top = rect.top - tooltipH - 6

  tooltipStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
  }
}

// ── Sub-tooltip: mini cicd job graph ──────────────────────────────────────────

const hoveredRunId = ref<string | null>(null)
const subTooltipVisible = ref(false)
const subTooltipStyle = ref<Record<string, string>>({})
let subHideTimer: ReturnType<typeof setTimeout> | null = null

/** Cache of fetched graphs keyed by run ID. `null` means fetch failed / no graph. */
const graphCache = new Map<string, WorkflowGraph | null>()
const currentGraph = ref<WorkflowGraph | null>(null)
const graphLoading = ref(false)

async function loadGraph(runId: string) {
  if (graphCache.has(runId)) {
    currentGraph.value = graphCache.get(runId) ?? null
    return
  }
  graphLoading.value = true
  currentGraph.value = null
  try {
    const graph = await api.get<WorkflowGraph>(`/api/cicd-runs/${runId}/graph`)
    graphCache.set(runId, graph)
    currentGraph.value = graph
  }
  catch {
    graphCache.set(runId, null)
    currentGraph.value = null
  }
  finally {
    graphLoading.value = false
  }
}

function onRunItemEnter(run: CiCdRun, e: MouseEvent) {
  if (subHideTimer) {
    clearTimeout(subHideTimer)
    subHideTimer = null
  }
  hoveredRunId.value = run.id
  positionSubTooltip(e)
  subTooltipVisible.value = true
  loadGraph(run.id)
}

function onRunItemLeave() {
  subHideTimer = setTimeout(() => {
    subTooltipVisible.value = false
    hoveredRunId.value = null
  }, 200)
}

function onSubTooltipEnter() {
  if (subHideTimer) {
    clearTimeout(subHideTimer)
    subHideTimer = null
  }
}

function onSubTooltipLeave() {
  subHideTimer = setTimeout(() => {
    subTooltipVisible.value = false
    hoveredRunId.value = null
  }, 200)
}

function positionSubTooltip(e: MouseEvent) {
  const item = e.currentTarget as HTMLElement
  const rect = item.getBoundingClientRect()
  const vpW = window.innerWidth
  const vpH = window.innerHeight
  const subW = 360
  const subH = 180

  // Prefer right of the main tooltip
  const mainLeft = parseFloat(tooltipStyle.value.left ?? '0')
  const mainW = 280
  let left = mainLeft + mainW + 6

  // If it would overflow right, try left of main tooltip
  if (left + subW > vpW - 8) left = mainLeft - subW - 6
  if (left < 8) left = 8

  let top = rect.top
  if (top + subH > vpH - 8) top = vpH - subH - 8
  if (top < 8) top = 8

  subTooltipStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
  }
}

/** Mini graph columns: BFS column assignment for compact display. */
const miniGraphColumns = computed((): WorkflowJobNode[][] => {
  const graph = currentGraph.value
  if (!graph || !graph.jobs.length) return []

  const jobs = graph.jobs
  const edges = graph.edges

  // BFS column assignment
  const colMap = new Map<string, number>()
  const inDegree = new Map<string, number>()
  for (const job of jobs) inDegree.set(job.id, 0)
  for (const edge of edges) {
    inDegree.set(edge.to, (inDegree.get(edge.to) ?? 0) + 1)
  }

  const queue: string[] = []
  for (const [id, deg] of inDegree) {
    if (deg === 0) queue.push(id)
  }
  while (queue.length) {
    const id = queue.shift()!
    const col = colMap.get(id) ?? 0
    for (const edge of edges) {
      if (edge.from === id) {
        const nextCol = Math.max(colMap.get(edge.to) ?? 0, col + 1)
        colMap.set(edge.to, nextCol)
        const newDeg = (inDegree.get(edge.to) ?? 1) - 1
        inDegree.set(edge.to, newDeg)
        if (newDeg === 0) queue.push(edge.to)
      }
    }
  }

  const byCol = new Map<number, WorkflowJobNode[]>()
  for (const job of jobs) {
    const col = colMap.get(job.id) ?? 0
    if (!byCol.has(col)) byCol.set(col, [])
    byCol.get(col)!.push(job)
  }

  const maxCol = Math.max(...Array.from(byCol.keys()), 0)
  return Array.from({ length: maxCol + 1 }, (_, i) => byCol.get(i) ?? [])
})

// ── Helpers ───────────────────────────────────────────────────────────────────

function relativeTime(dateStr: string) {
  if (!dateStr) return '—'
  const ms = Date.now() - new Date(dateStr).getTime()
  const s = Math.floor(ms / 1000)
  if (s < 60) return 'just now'
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}
</script>
