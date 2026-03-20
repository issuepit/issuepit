<template>
  <div class="relative inline-flex" @mouseenter="onChipEnter" @mouseleave="scheduleClose">
    <!-- Status chip -->
    <span :class="chipClass" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium cursor-default select-none">
      <span :class="dotClass" class="w-1.5 h-1.5 rounded-full" />
      {{ session.statusName }}
    </span>

    <!-- Tooltip -->
    <Teleport to="body">
      <div
        v-if="tooltipVisible"
        ref="tooltipEl"
        :style="tooltipStyle"
        class="fixed z-50 bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/60 p-3 min-w-[240px] max-w-xs"
        @mouseenter="keepOpen"
        @mouseleave="scheduleClose">
        <p class="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wide">Agent Session</p>
        <!-- Session info - clickable if projectId available -->
        <component
          :is="session.projectId ? 'button' : 'div'"
          class="flex items-start gap-2 w-full text-left rounded-lg px-2 py-1.5 transition-colors"
          :class="session.projectId ? 'hover:bg-gray-800 cursor-pointer' : ''"
          @click.stop="session.projectId && navigateTo(`/projects/${session.projectId}/runs/agent-sessions/${session.id}`)">
          <span :class="dotClass" class="w-2 h-2 rounded-full shrink-0 mt-1" />
          <div class="flex-1 min-w-0">
            <p v-if="session.issueTitle" class="text-xs text-gray-200 font-medium truncate">{{ session.issueTitle }}</p>
            <p class="text-xs text-gray-400 truncate">{{ session.agentName }}</p>
            <p v-if="session.gitBranch" class="text-xs text-gray-500 font-mono truncate">{{ session.gitBranch }}</p>
            <p v-if="session.commitSha" class="text-xs text-gray-600 font-mono">{{ session.commitSha.slice(0, 7) }}</p>
          </div>
          <div class="flex flex-col items-end shrink-0 gap-0.5">
            <span :class="chipClass" class="text-xs px-1.5 py-0.5 rounded-full font-medium">{{ session.statusName }}</span>
            <span class="text-xs text-gray-600">{{ relativeTime(session.startedAt) }}</span>
          </div>
        </component>
        <!-- Linked CI/CD runs -->
        <template v-if="session.cicdRuns && session.cicdRuns.length">
          <p class="text-xs text-gray-600 mt-2 mb-1 font-medium uppercase tracking-wide">CI/CD Runs</p>
          <div class="space-y-1">
            <button
              v-for="run in session.cicdRuns"
              :key="run.id"
              class="flex items-center gap-2 w-full text-left rounded-lg px-2 py-1 transition-colors hover:bg-gray-800 cursor-pointer"
              @click.stop="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)"
              @mouseenter="onRunItemEnter(run, $event)"
              @mouseleave="scheduleClose">
              <span :class="runDotClass(run.status)" class="w-1.5 h-1.5 rounded-full shrink-0" />
              <div class="flex-1 min-w-0">
                <p class="text-xs text-gray-300 truncate">{{ run.workflow || run.branch || 'Run' }}</p>
              </div>
              <span :class="runChipClass(run.status)" class="text-xs px-1.5 py-0.5 rounded-full font-medium shrink-0">{{ run.statusName }}</span>
            </button>
          </div>
        </template>
      </div>

      <!-- Sub-tooltip: mini job graph shown when hovering a CI/CD run item -->
      <div
        v-if="subTooltipVisible && hoveredCiCdRunId"
        :style="subTooltipStyle"
        class="fixed z-[60] bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/60"
        @mouseenter="keepOpen"
        @mouseleave="scheduleClose">
        <p class="text-xs text-gray-500 px-3 pt-3 pb-1 font-medium uppercase tracking-wide">Job Graph</p>
        <div class="px-3 pb-3">
          <MiniJobGraph :run-id="hoveredCiCdRunId" @job-click="onMiniGraphJobClick(hoveredCiCdRunId!, $event)" />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { AgentSessionStatus, CiCdRunStatus, type CiCdRun } from '~/types'

interface SessionProp {
  id: string
  status: AgentSessionStatus
  statusName: string
  agentName: string
  startedAt: string
  endedAt?: string
  gitBranch?: string
  commitSha?: string
  issueTitle?: string
  projectId?: string
  cicdRuns?: CiCdRun[]
}

const props = defineProps<{
  session: SessionProp
}>()

const chipClass = computed(() => {
  switch (props.session.status) {
    case AgentSessionStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case AgentSessionStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case AgentSessionStatus.Failed: return 'bg-red-900/30 text-red-400'
    case AgentSessionStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
})

const dotClass = computed(() => {
  switch (props.session.status) {
    case AgentSessionStatus.Succeeded: return 'bg-green-400'
    case AgentSessionStatus.Running: return 'bg-blue-400 animate-pulse'
    case AgentSessionStatus.Failed: return 'bg-red-400'
    case AgentSessionStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
})

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

// ── Tooltip positioning & visibility ─────────────────────────────────────────

const tooltipVisible = ref(false)
const tooltipEl = ref<HTMLElement | null>(null)
const tooltipStyle = ref<Record<string, string>>({})
/** True when the main tooltip was flipped to the LEFT side of the chip (near right edge of viewport). */
const tooltipPlacedLeft = ref(false)

const TOOLTIP_BASE_HEIGHT = 110
const CICD_RUN_ITEM_HEIGHT = 32

function tooltipHeight() {
  const runsH = (props.session.cicdRuns?.length ?? 0) * CICD_RUN_ITEM_HEIGHT
  return TOOLTIP_BASE_HEIGHT + (runsH > 0 ? runsH + 24 : 0)
}

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
  const tooltipW = 260
  const ttH = tooltipHeight()

  // Prefer right side of chip so moving cursor up/down won't land in the tooltip
  let left = rect.right + 12
  let top = rect.top
  tooltipPlacedLeft.value = false

  // Flip to left if it would overflow right
  if (left + tooltipW > vpW - 8) {
    left = rect.left - tooltipW - 12
    tooltipPlacedLeft.value = true
  }
  if (left < 8) left = 8

  // Ensure doesn't overflow bottom
  if (top + ttH > vpH - 8) top = vpH - ttH - 8
  if (top < 8) top = 8

  tooltipStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
  }
}

// ── Sub-tooltip: mini cicd job graph ──────────────────────────────────────────

const hoveredCiCdRunId = ref<string | null>(null)
const subTooltipVisible = ref(false)
const subTooltipStyle = ref<Record<string, string>>({})

function onRunItemEnter(run: CiCdRun, e: MouseEvent) {
  keepOpen()
  hoveredCiCdRunId.value = run.id
  positionSubTooltip(e)
  subTooltipVisible.value = true
}

function onMiniGraphJobClick(runId: string, jobId: string) {
  const run = props.session.cicdRuns?.find(r => r.id === runId)
  if (!run) return
  tooltipVisible.value = false
  subTooltipVisible.value = false
  hoveredCiCdRunId.value = null
  navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}?tab=logs&job=${encodeURIComponent(jobId)}`)
}

function positionSubTooltip(e: MouseEvent) {
  const item = e.currentTarget as HTMLElement
  const rect = item.getBoundingClientRect()
  const vpW = window.innerWidth
  const vpH = window.innerHeight
  const subW = 380
  const subH = 200

  const mainLeft = parseFloat(tooltipStyle.value.left ?? '0')
  const mainW = tooltipEl.value?.offsetWidth ?? 260
  let left: number

  if (tooltipPlacedLeft.value) {
    // Left mode: main tooltip is to the LEFT of the chip — prefer sub-tooltip to the LEFT of main tooltip
    left = mainLeft - subW
    if (left < 8) left = mainLeft + mainW  // fallback: right of main if no room on left
  }
  else {
    // Right mode: main tooltip is to the RIGHT of the chip — prefer sub-tooltip to the RIGHT of main tooltip
    left = mainLeft + mainW
    if (left + subW > vpW - 8) left = mainLeft - subW  // fallback: left of main if no room on right
  }
  // Final viewport clamp
  if (left + subW > vpW - 8) left = vpW - subW - 8
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
    hoveredCiCdRunId.value = null
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
