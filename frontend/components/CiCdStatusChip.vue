<template>
  <div class="relative inline-flex" @mouseenter="onChipEnter" @mouseleave="scheduleClose">
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
        @mouseenter="keepOpen"
        @mouseleave="scheduleClose">
        <p class="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wide">CI/CD Runs</p>
        <div class="space-y-1.5">
          <button
            v-for="run in runs"
            :key="run.id"
            class="flex items-center gap-2 w-full text-left rounded-lg px-2 py-1.5 transition-colors hover:bg-gray-800 cursor-pointer"
            @click.stop="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)"
            @mouseenter="onRunItemEnter(run, $event)"
            @mouseleave="scheduleClose">
            <span :class="runDotClass(run.status)" class="w-2 h-2 rounded-full shrink-0" />
            <div class="flex-1 min-w-0">
              <p class="text-xs text-gray-200 truncate font-medium"
                :aria-label="`${run.workflow || run.branch || 'Run'}${run.externalSource ? ` from ${run.externalSource}` : ''}`">
                {{ run.workflow || run.branch || 'Run' }}
                <span v-if="run.externalSource" class="ml-1 text-gray-600 font-normal">({{ run.externalSource }})</span>
              </p>
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
        class="fixed z-[60] bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/60"
        @mouseenter="keepOpen"
        @mouseleave="scheduleClose">
        <p class="text-xs text-gray-500 px-3 pt-3 pb-1 font-medium uppercase tracking-wide">Job Graph</p>
        <div class="px-3 pb-3">
          <MiniJobGraph :run-id="hoveredRunId" />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { CiCdRunStatus, type CiCdRun } from '~/types'

const props = withDefaults(defineProps<{
  runs: CiCdRun[]
  /** If true, runs in the tooltip are clickable links to run details. Default: true (kept for backward compat, tooltip items are always clickable) */
  runLink?: boolean
}>(), {
  runLink: true,
})

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

const RUN_ITEM_HEIGHT = 60
const TOOLTIP_HEADER_HEIGHT = 48
const MAX_TOOLTIP_HEIGHT = 320

function onChipEnter(e: MouseEvent) {
  keepOpen()
  positionTooltip(e)
  tooltipVisible.value = true
}

function positionTooltip(e: MouseEvent) {
  const target = e.currentTarget as HTMLElement
  const rect = target.getBoundingClientRect()
  const vpW = window.innerWidth
  const vpH = window.innerHeight
  const tooltipW = 280
  const tooltipH = Math.min(props.runs.length * RUN_ITEM_HEIGHT + TOOLTIP_HEADER_HEIGHT, MAX_TOOLTIP_HEIGHT)

  // Prefer right side of chip so moving cursor up/down won't land in the tooltip
  let left = rect.right + 12
  let top = rect.top

  // Flip to left if it would overflow right
  if (left + tooltipW > vpW - 8) left = rect.left - tooltipW - 12
  if (left < 8) left = 8

  // Ensure doesn't overflow bottom
  if (top + tooltipH > vpH - 8) top = vpH - tooltipH - 8
  if (top < 8) top = 8

  tooltipStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
  }
}

// ── Sub-tooltip: mini cicd job graph ──────────────────────────────────────────

const hoveredRunId = ref<string | null>(null)
const subTooltipVisible = ref(false)
const subTooltipStyle = ref<Record<string, string>>({})

function onRunItemEnter(run: CiCdRun, e: MouseEvent) {
  keepOpen()
  hoveredRunId.value = run.id
  positionSubTooltip(e)
  subTooltipVisible.value = true
}

function positionSubTooltip(e: MouseEvent) {
  const item = e.currentTarget as HTMLElement
  const rect = item.getBoundingClientRect()
  const vpW = window.innerWidth
  const vpH = window.innerHeight
  const subW = 380
  const subH = 200

  // Prefer right of the main tooltip – use actual rendered width to avoid a gap
  const mainLeft = parseFloat(tooltipStyle.value.left ?? '0')
  const mainW = tooltipEl.value?.offsetWidth ?? 280
  let left = mainLeft + mainW

  // If it would overflow right, try left of main tooltip
  if (left + subW > vpW - 8) left = mainLeft - subW
  if (left < 8) left = 8

  let top = rect.top
  if (top + subH > vpH - 8) top = vpH - subH - 8
  if (top < 8) top = 8

  subTooltipStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
  }
}

// ── Shared hover management ───────────────────────────────────────────────────
// A single shared timer closes both tooltips. Any tooltip element entering resets the timer,
// ensuring tooltips stay open as the cursor moves between chip → main tooltip → sub-tooltip.

let hideTimer: ReturnType<typeof setTimeout> | null = null

function keepOpen() {
  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
}

function scheduleClose() {
  if (hideTimer) clearTimeout(hideTimer)
  hideTimer = setTimeout(() => {
    tooltipVisible.value = false
    subTooltipVisible.value = false
    hoveredRunId.value = null
  }, 200)
}

onUnmounted(() => {
  if (hideTimer) clearTimeout(hideTimer)
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
