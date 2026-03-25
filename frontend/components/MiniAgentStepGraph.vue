<template>
  <div>
    <div v-if="loading" class="text-xs text-gray-500 py-2 text-center">Loading…</div>
    <div v-else-if="!steps.length" class="text-xs text-gray-500 py-2 text-center italic">No step data</div>
    <div v-else class="overflow-x-auto">
      <svg
        :width="svgWidth"
        :height="svgHeight"
        class="block overflow-visible"
        style="max-width: 360px">
        <!-- Connector lines between step boxes -->
        <line
          v-for="(step, i) in layoutSteps.slice(0, -1)"
          :key="`c${i}`"
          :x1="step.x + STEP_BOX_W"
          :y1="step.y + STEP_BOX_H / 2"
          :x2="layoutSteps[i + 1].x"
          :y2="layoutSteps[i + 1].y + STEP_BOX_H / 2"
          :stroke="connectorStroke(layoutSteps[i + 1].hasError)"
          stroke-width="1.2"
          opacity="0.75" />
        <!-- Step boxes -->
        <g
          v-for="step in layoutSteps"
          :key="step.key"
          @mouseenter="onStepEnter(step, $event)"
          @mouseleave="onStepLeave">
          <rect
            :x="step.x"
            :y="step.y"
            :width="STEP_BOX_W"
            :height="STEP_BOX_H"
            rx="3"
            :fill="stepFill(step)"
            :stroke="hoveredKey === step.key ? stepHoverStroke(step) : stepStroke(step)"
            :stroke-width="hoveredKey === step.key ? 2 : 1"
            :opacity="hoveredKey !== null && hoveredKey !== step.key ? 0.65 : 1" />
          <!-- Status dot -->
          <circle
            :cx="step.x + 7"
            :cy="step.y + STEP_BOX_H / 2"
            r="2.5"
            :fill="stepDotFill(step)" />
          <!-- Step name text -->
          <text
            :x="step.x + 15"
            :y="step.y + STEP_BOX_H / 2 + 3.5"
            font-size="9"
            :fill="stepTextFill(step)"
            class="select-none pointer-events-none">
            {{ step.label.length > MAX_LABEL_CHARS ? step.label.slice(0, MAX_LABEL_CHARS - 1) + '…' : step.label }}
          </text>
        </g>
      </svg>
    </div>

    <!-- Step hover tooltip -->
    <Teleport to="body">
      <div
        v-if="hoveredKey && tooltipData"
        :style="tooltipStyle"
        class="fixed z-[80] bg-gray-800 border border-gray-600 rounded-lg shadow-xl px-2.5 py-1.5 pointer-events-none min-w-[120px]">
        <p class="text-xs text-gray-200 font-medium leading-tight">{{ tooltipData.label }}</p>
        <p class="text-xs mt-0.5" :class="tooltipData.hasError ? 'text-red-400' : 'text-green-400'">
          {{ tooltipData.hasError ? 'Failed' : 'Succeeded' }}
        </p>
        <p v-if="tooltipData.duration" class="text-xs text-gray-500 mt-0.5">{{ tooltipData.duration }}</p>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  sessionId: string
}>()

const api = useApi()

// ── Constants ─────────────────────────────────────────────────────────────────

const STEP_BOX_W = 96
const STEP_BOX_H = 22
const COL_GAP = 28
const PADDING = 6
const MAX_LABEL_CHARS = 13
const TOOLTIP_W = 150

// ── Types ─────────────────────────────────────────────────────────────────────

interface AgentStep {
  section: string
  sectionIndex: number
  label: string
  hasError: boolean
  startedAt: string
  endedAt: string
}

interface LayoutStep extends AgentStep {
  key: string
  x: number
  y: number
}

// ── Data fetching ─────────────────────────────────────────────────────────────

const MAX_CACHE_SIZE = 50

/** LRU-style cache: stores steps keyed by sessionId, capped at MAX_CACHE_SIZE entries. */
const stepsCache = new Map<string, AgentStep[]>()
const steps = ref<AgentStep[]>([])
const loading = ref(false)

async function loadSteps() {
  if (stepsCache.has(props.sessionId)) {
    steps.value = stepsCache.get(props.sessionId)!
    return
  }
  loading.value = true
  steps.value = []
  try {
    const data = await api.get<AgentStep[]>(`/api/agent-sessions/${props.sessionId}/steps`)
    // Evict oldest entry when cache is full.
    if (stepsCache.size >= MAX_CACHE_SIZE) {
      const firstKey = stepsCache.keys().next().value
      if (firstKey !== undefined) stepsCache.delete(firstKey)
    }
    stepsCache.set(props.sessionId, data)
    steps.value = data
  }
  catch {
    stepsCache.set(props.sessionId, [])
    steps.value = []
  }
  finally {
    loading.value = false
  }
}

watch(() => props.sessionId, loadSteps, { immediate: true })

// ── Layout ────────────────────────────────────────────────────────────────────

const layoutSteps = computed<LayoutStep[]>(() => {
  const result: LayoutStep[] = []
  for (let i = 0; i < steps.value.length; i++) {
    const step = steps.value[i]
    const x = PADDING + i * (STEP_BOX_W + COL_GAP)
    const y = PADDING
    result.push({
      ...step,
      key: `${step.section}:${step.sectionIndex}`,
      x,
      y,
    })
  }
  return result
})

const svgWidth = computed(() => PADDING * 2 + steps.value.length * STEP_BOX_W + Math.max(steps.value.length - 1, 0) * COL_GAP)
const svgHeight = computed(() => PADDING * 2 + STEP_BOX_H)

// ── Color helpers ─────────────────────────────────────────────────────────────

function stepFill(step: LayoutStep) {
  if (step.hasError) return '#450a0a'
  return '#14532d'
}

function stepStroke(step: LayoutStep) {
  if (step.hasError) return '#ef4444'
  return '#22c55e'
}

function stepHoverStroke(step: LayoutStep) {
  if (step.hasError) return '#f87171'
  return '#4ade80'
}

function stepTextFill(step: LayoutStep) {
  if (step.hasError) return '#fecaca'
  return '#bbf7d0'
}

function stepDotFill(step: LayoutStep) {
  if (step.hasError) return '#ef4444'
  return '#22c55e'
}

function connectorStroke(hasError: boolean) {
  return hasError ? '#ef4444' : '#22c55e'
}

// ── Hover tooltip ─────────────────────────────────────────────────────────────

const hoveredKey = ref<string | null>(null)
const tooltipStyle = ref<Record<string, string>>({})

interface TooltipData {
  label: string
  hasError: boolean
  duration?: string
}

const tooltipData = computed((): TooltipData | null => {
  if (!hoveredKey.value) return null
  const step = layoutSteps.value.find(s => s.key === hoveredKey.value)
  if (!step) return null
  return {
    label: step.label,
    hasError: step.hasError,
    duration: formatDuration(step.startedAt, step.endedAt),
  }
})

function formatDuration(startedAt: string, endedAt: string): string | undefined {
  const start = new Date(startedAt).getTime()
  const end = new Date(endedAt).getTime()
  const ms = end - start
  if (ms < 0) return undefined
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ${s % 60}s`
  return `${Math.floor(m / 60)}h ${m % 60}m`
}

function onStepEnter(step: LayoutStep, e: MouseEvent) {
  hoveredKey.value = step.key
  const target = e.currentTarget as SVGGElement
  const rect = target.getBoundingClientRect()
  const vpW = window.innerWidth
  let left = rect.right + 6
  if (left + TOOLTIP_W > vpW - 8) left = rect.left - TOOLTIP_W - 6
  tooltipStyle.value = {
    left: `${left}px`,
    top: `${rect.top}px`,
  }
}

function onStepLeave() {
  hoveredKey.value = null
}
</script>
