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
          <component
            :is="runLink ? 'button' : 'div'"
            v-for="run in runs"
            :key="run.id"
            class="flex items-center gap-2 w-full text-left rounded-lg px-2 py-1.5 transition-colors"
            :class="runLink ? 'hover:bg-gray-800 cursor-pointer' : ''"
            @click="runLink && navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)">
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
          </component>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { CiCdRunStatus, type CiCdRun } from '~/types'

const props = withDefaults(defineProps<{
  runs: CiCdRun[]
  /** If true, runs in the tooltip are clickable links to run details. Default: true */
  runLink?: boolean
}>(), {
  runLink: true,
})

// ── Derived status from all runs ──────────────────────────────────────────────

const summaryStatus = computed((): CiCdRunStatus => {
  if (!props.runs.length) return CiCdRunStatus.Pending
  if (props.runs.some(r => r.status === CiCdRunStatus.Failed)) return CiCdRunStatus.Failed
  if (props.runs.some(r => r.status === CiCdRunStatus.Cancelled)) return CiCdRunStatus.Cancelled
  if (props.runs.some(r => r.status === CiCdRunStatus.Running)) return CiCdRunStatus.Running
  if (props.runs.some(r => r.status === CiCdRunStatus.Pending)) return CiCdRunStatus.Pending
  return CiCdRunStatus.Succeeded
})

const label = computed(() => {
  const s = summaryStatus.value
  const labels: Record<CiCdRunStatus, string> = {
    [CiCdRunStatus.Pending]: 'Pending',
    [CiCdRunStatus.Running]: 'Running',
    [CiCdRunStatus.Succeeded]: 'Succeeded',
    [CiCdRunStatus.Failed]: 'Failed',
    [CiCdRunStatus.Cancelled]: 'Cancelled',
  }
  return labels[s]
})

const chipClass = computed(() => {
  switch (summaryStatus.value) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
})

const dotClass = computed(() => {
  switch (summaryStatus.value) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
})

function runChipClass(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function runDotClass(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}

// ── Tooltip positioning & visibility ─────────────────────────────────────────

const tooltipVisible = ref(false)
const tooltipEl = ref<HTMLElement | null>(null)
const tooltipStyle = ref<Record<string, string>>({})
let hideTimer: ReturnType<typeof setTimeout> | null = null

const RUN_ITEM_HEIGHT = 60
const TOOLTIP_HEADER_HEIGHT = 48
const MAX_TOOLTIP_HEIGHT = 320

function onChipEnter(e: MouseEvent) {
  if (hideTimer) { clearTimeout(hideTimer); hideTimer = null }
  positionTooltip(e)
  tooltipVisible.value = true
}

function onChipLeave() {
  hideTimer = setTimeout(() => { tooltipVisible.value = false }, 150)
}

function onTooltipEnter() {
  if (hideTimer) { clearTimeout(hideTimer); hideTimer = null }
}

function onTooltipLeave() {
  hideTimer = setTimeout(() => { tooltipVisible.value = false }, 150)
}

onUnmounted(() => {
  if (hideTimer) clearTimeout(hideTimer)
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
