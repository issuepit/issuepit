<template>
  <div class="relative inline-flex" @mouseenter="onChipEnter" @mouseleave="onChipLeave">
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
        class="fixed z-50 bg-gray-900 border border-gray-700 rounded-xl shadow-2xl shadow-black/60 p-3 min-w-[220px] max-w-xs"
        @mouseenter="onTooltipEnter"
        @mouseleave="onTooltipLeave">
        <p class="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wide">Agent Session</p>
        <div class="flex items-start gap-2">
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
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { AgentSessionStatus } from '~/types'

interface SessionProp {
  status: AgentSessionStatus
  statusName: string
  agentName: string
  startedAt: string
  endedAt?: string
  gitBranch?: string
  commitSha?: string
  issueTitle?: string
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

// ── Tooltip positioning & visibility ─────────────────────────────────────────

const tooltipVisible = ref(false)
const tooltipEl = ref<HTMLElement | null>(null)
const tooltipStyle = ref<Record<string, string>>({})
let hideTimer: ReturnType<typeof setTimeout> | null = null

const TOOLTIP_HEIGHT = 110

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
  const tooltipW = 260

  let left = rect.left
  let top = rect.bottom + 6

  // Flip left if it would overflow right
  if (left + tooltipW > vpW - 8) left = vpW - tooltipW - 8
  if (left < 8) left = 8

  // Flip above if it would overflow bottom
  if (top + TOOLTIP_HEIGHT > vpH - 8) top = rect.top - TOOLTIP_HEIGHT - 6

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
