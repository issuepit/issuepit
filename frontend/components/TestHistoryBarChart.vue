<template>
  <div class="relative" @mouseleave="hideTooltip">
    <div v-if="data.length === 0" class="py-8 text-center text-sm text-gray-500">
      No test results in this period
    </div>
    <template v-else>
      <div class="overflow-x-auto">
        <svg :viewBox="`0 0 ${svgWidth} ${svgHeight}`" class="w-full" :style="`min-width:${minWidth}px`">
          <!-- Y-axis grid lines -->
          <line v-for="yv in gridY" :key="`grid-${yv}`"
            :x1="padLeft" :y1="yScale(yv)" :x2="svgWidth - padRight" :y2="yScale(yv)"
            stroke="#374151" stroke-width="1" />
          <!-- Y-axis value labels -->
          <text v-for="yv in gridY" :key="`yl-${yv}`"
            :x="padLeft - 4" :y="yScale(yv) + 4" text-anchor="end" fill="#6b7280" font-size="9">{{ formatYLabel(yv) }}</text>
          <!-- Y-axis title -->
          <text
            :x="8" :y="padTop + plotH / 2" text-anchor="middle" fill="#6b7280" font-size="8"
            :transform="`rotate(-90, 8, ${padTop + plotH / 2})`">{{ yAxisLabel }}</text>

          <!-- Month / week boundary markers: boundary line + tick + label -->
          <g v-for="marker in xAxisMarkers" :key="`xm-${marker.x}`">
            <!-- Vertical guide line (only for true month/week boundaries, not the start label) -->
            <line v-if="marker.isBoundary"
              :x1="marker.x" :y1="padTop" :x2="marker.x" :y2="baseline"
              stroke="#1f2937" stroke-width="1" />
            <!-- Tick mark below baseline -->
            <line :x1="marker.x" :y1="baseline" :x2="marker.x" :y2="baseline + 4"
              stroke="#4b5563" stroke-width="1.5" />
            <!-- Month label below tick -->
            <text v-if="marker.label"
              :x="marker.x + 2"
              :y="baseline + 22"
              text-anchor="start" fill="#9ca3af" font-size="8.5" font-weight="600">{{ marker.label }}</text>
          </g>

          <!-- Bars -->
          <g v-for="(day, i) in data" :key="day.date">
            <!-- Groups mode: stacked per artifact, colored by group failure rate -->
            <template v-if="colorMode === 'groups'">
              <template v-if="yAxis === 'count'">
                <rect v-for="seg in groupSegments(day)" :key="seg.name"
                  :x="barX(i)" :y="seg.y" :width="barW" :height="seg.h"
                  :fill="seg.color" opacity="0.88" rx="1" />
              </template>
              <template v-else>
                <rect v-for="seg in groupDurationSegments(day)" :key="seg.name"
                  :x="barX(i)" :y="seg.y" :width="barW" :height="seg.h"
                  :fill="seg.color" opacity="0.88" rx="1" />
              </template>
            </template>
            <!-- Pass-fail stacked (count only) -->
            <template v-else-if="colorMode === 'pass-fail'">
              <template v-if="yAxis === 'count'">
                <rect v-if="barValue(day) > 0 && day.failedTests > 0"
                  :x="barX(i)" :y="yScale(day.failedTests)" :width="barW"
                  :height="plotH - yScale(day.failedTests) + padTop"
                  fill="#ef4444" opacity="0.85" rx="1" />
                <rect v-if="barValue(day) > 0 && day.passedTests > 0"
                  :x="barX(i)" :y="yScale(barValue(day))" :width="barW"
                  :height="yScale(day.failedTests) - yScale(barValue(day))"
                  fill="#22c55e" opacity="0.85" rx="1" />
              </template>
              <!-- Duration Y with pass/fail mode: single bar (can't split duration by outcome) -->
              <template v-else>
                <rect v-if="barValue(day) > 0"
                  :x="barX(i)" :y="yScale(barValue(day))" :width="barW"
                  :height="plotH - yScale(barValue(day)) + padTop"
                  fill="#6366f1" opacity="0.85" rx="1" />
              </template>
            </template>
            <!-- Failure-rate: single bar colored by failure % -->
            <template v-else>
              <rect v-if="barValue(day) > 0"
                :x="barX(i)" :y="yScale(barValue(day))" :width="barW"
                :height="plotH - yScale(barValue(day)) + padTop"
                :fill="failureRateColor(day.totalTests, day.failedTests)"
                opacity="0.85" rx="1" />
            </template>

            <!-- X axis day label (sparse, above month label row) -->
            <text v-if="showDayLabel(i)"
              :x="barX(i) + barW / 2"
              :y="baseline + 10"
              text-anchor="middle" fill="#6b7280" font-size="7.5">{{ shortDay(day.date) }}</text>
          </g>

          <!-- Invisible hover rects (on top of all bars, full column width) -->
          <rect v-for="(day, i) in data" :key="`hover-${i}`"
            :x="padLeft + i * barStep" :y="padTop"
            :width="barStep" :height="plotH"
            fill="transparent"
            style="cursor:crosshair"
            @mouseenter="e => showTooltip(e, day)"
            @mousemove="moveTooltip" />
        </svg>
      </div>

      <!-- Legend -->
      <div class="flex flex-wrap items-center gap-3 mt-2">
        <template v-if="colorMode === 'groups'">
          <span v-for="g in legendGroups" :key="g.name" class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 rounded-sm inline-block shrink-0" :style="`background:${g.color}`"></span>
            {{ g.name }}
          </span>
          <!-- Color-health hint -->
          <span class="flex items-center gap-1.5 text-xs text-gray-600">
            <span class="w-8 h-2 rounded-sm inline-block shrink-0" style="background:linear-gradient(90deg,hsl(210,70%,48%),hsl(0,30%,30%))"></span>
            healthy → failing
          </span>
        </template>
        <template v-else-if="colorMode === 'pass-fail'">
          <template v-if="yAxis === 'count'">
            <span class="flex items-center gap-1.5 text-xs text-gray-400">
              <span class="w-3 h-2 bg-green-500 rounded-sm inline-block"></span> Passed
            </span>
            <span class="flex items-center gap-1.5 text-xs text-gray-400">
              <span class="w-3 h-2 bg-red-500 rounded-sm inline-block"></span> Failed
            </span>
          </template>
          <template v-else>
            <span class="flex items-center gap-1.5 text-xs text-gray-400">
              <span class="w-3 h-2 bg-indigo-500 rounded-sm inline-block"></span> Total duration
            </span>
            <span class="text-xs text-gray-600">(per-outcome split unavailable)</span>
          </template>
        </template>
        <template v-else>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 rounded-sm inline-block" style="background:linear-gradient(90deg,#22c55e,#ef4444)"></span>
            Low → High failure %
          </span>
        </template>
        <span class="text-xs text-gray-600 ml-auto">
          {{ totalTests.toLocaleString() }} tests · {{ totalRuns }} runs
        </span>
      </div>

      <!-- Tooltip (teleported to body for correct z-ordering) -->
      <teleport to="body">
        <div v-if="tooltipDay"
          class="fixed z-[9999] pointer-events-none bg-gray-900 border border-gray-700 rounded-lg shadow-xl text-xs p-2.5 min-w-[150px]"
          :style="`left:${tooltipPos.x + 14}px;top:${tooltipPos.y - 8}px;transform:translateY(-100%)`">
          <div class="font-medium text-gray-200 mb-1.5">{{ formatTooltipDate(tooltipDay.date) }}</div>
          <div class="space-y-0.5">
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Total</span>
              <span class="text-gray-200">{{ tooltipDay.totalTests.toLocaleString() }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-green-400">Passed</span>
              <span class="text-gray-200">{{ tooltipDay.passedTests.toLocaleString() }}</span>
            </div>
            <div v-if="tooltipDay.failedTests > 0" class="flex justify-between gap-3">
              <span class="text-red-400">Failed</span>
              <span class="text-gray-200">{{ tooltipDay.failedTests.toLocaleString() }}</span>
            </div>
            <div v-if="tooltipDay.skippedTests > 0" class="flex justify-between gap-3">
              <span class="text-yellow-400">Skipped</span>
              <span class="text-gray-200">{{ tooltipDay.skippedTests.toLocaleString() }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Duration</span>
              <span class="text-gray-200">{{ formatDuration(tooltipDay.durationMs) }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Runs</span>
              <span class="text-gray-200">{{ tooltipDay.runCount }}</span>
            </div>
            <!-- Per-group breakdown in groups mode -->
            <template v-if="colorMode === 'groups' && tooltipDay.groups?.length">
              <div class="border-t border-gray-700 mt-1.5 pt-1.5 space-y-0.5">
                <div v-for="g in tooltipDay.groups.filter(g => g.totalTests > 0)" :key="g.name"
                  class="flex items-center justify-between gap-2">
                  <span class="flex items-center gap-1">
                    <span class="w-2 h-2 rounded-sm inline-block shrink-0"
                      :style="`background:${groupColor(g.name, g.totalTests, g.failedTests)}`"></span>
                    <span class="text-gray-400">{{ g.name }}</span>
                  </span>
                  <span class="text-gray-300">
                    {{ g.totalTests }}
                    <span v-if="g.failedTests > 0" class="text-red-400 ml-0.5">{{ g.failedTests }} fail</span>
                  </span>
                </div>
              </div>
            </template>
          </div>
        </div>
      </teleport>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { TestDailySummary } from '~/types'

const props = defineProps<{
  data: TestDailySummary[]
  colorMode: string
  yAxis: string
}>()

const svgWidth = 600
// Slightly taller to accommodate tick + month label row below day labels
const svgHeight = 180
const padLeft = 36
const padRight = 8
const padTop = 8
// tick (4) + day label (10) + month label (12) + margin (4) = 30
const padBottom = 30
const minWidth = 300

/** Minimum horizontal distance (SVG units) between the start-month label and the first
 *  month-boundary label, below which we suppress the start label to avoid text crowding. */
const MIN_LABEL_SPACING = 35
const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const DAYS_PER_WEEK = 7

/** Safe month name lookup (1-based month index). */
function monthName(monthStr: string): string {
  const idx = parseInt(monthStr) - 1
  return MONTH_NAMES[idx >= 0 && idx < 12 ? idx : 0]
}
const plotH = svgHeight - padTop - padBottom
/** Y coordinate of the X axis baseline. */
const baseline = padTop + plotH

const barGap = 2

const barStep = computed(() => {
  if (props.data.length === 0) return plotW
  return plotW / props.data.length
})

const barW = computed(() => Math.max(2, barStep.value - barGap))

function barX(i: number): number {
  return padLeft + i * barStep.value + barGap / 2
}

function barValue(day: TestDailySummary): number {
  if (props.yAxis === 'duration') return day.durationMs / 1000
  return day.totalTests
}

const maxVal = computed(() => {
  if (props.data.length === 0) return 1
  return Math.max(...props.data.map(d => barValue(d)), 1)
})

const gridY = computed(() => {
  const step = Math.ceil(maxVal.value / 4)
  if (step === 0) return [0]
  return [0, step, step * 2, step * 3, step * 4].filter(v => v <= maxVal.value + step * 0.5)
})

function yScale(val: number): number {
  return padTop + plotH - (val / maxVal.value) * plotH
}

const yAxisLabel = computed(() => (props.yAxis === 'duration' ? 'sec' : 'tests'))

// ── Group coloring ─────────────────────────────────────────────────────────

/** Stable ordered list of unique group names across all days. */
const groupNames = computed(() => {
  const names = new Set<string>()
  for (const day of props.data) {
    for (const g of day.groups ?? []) names.add(g.name)
  }
  return [...names].sort()
})

const GROUP_COLOR_HUES = [210, 160, 280, 35, 0, 60, 300, 120]
function groupBaseHue(idx: number): number {
  return GROUP_COLOR_HUES[idx % GROUP_COLOR_HUES.length]
}

/** Color a group by its failure rate: base hue at healthy, shifts to dark red at 100% failure. */
function groupColor(name: string, totalTests: number, failedTests: number): string {
  const idx = groupNames.value.indexOf(name)
  const hue = groupBaseHue(idx)
  if (totalTests === 0) return `hsl(${hue}, 20%, 30%)`
  const failRate = Math.min(1, failedTests / totalTests)
  const sat = Math.round(70 - failRate * 40)
  const lit = Math.round(48 - failRate * 20)
  const blendedHue = Math.round(hue * (1 - failRate))
  return `hsl(${blendedHue}, ${sat}%, ${lit}%)`
}

/** Legend entries for groups mode (aggregate failure rate per group). */
const legendGroups = computed(() => {
  const totals = new Map<string, { total: number; failed: number }>()
  for (const day of props.data) {
    for (const g of day.groups ?? []) {
      const t = totals.get(g.name) ?? { total: 0, failed: 0 }
      t.total += g.totalTests
      t.failed += g.failedTests
      totals.set(g.name, t)
    }
  }
  return groupNames.value.map(name => {
    const t = totals.get(name) ?? { total: 0, failed: 0 }
    return { name, color: groupColor(name, t.total, t.failed) }
  })
})

// ── Bar segments ────────────────────────────────────────────────────────────

interface BarSegment { name: string; y: number; h: number; color: string }

function groupSegments(day: TestDailySummary): BarSegment[] {
  const groups = (day.groups ?? []).slice().sort((a, b) => a.name.localeCompare(b.name))
  let cumulative = 0
  return groups
    .filter(g => g.totalTests > 0)
    .map(g => {
      const bottom = cumulative
      cumulative += g.totalTests
      return {
        name: g.name,
        y: yScale(cumulative),
        h: Math.max(1, yScale(bottom) - yScale(cumulative)),
        color: groupColor(g.name, g.totalTests, g.failedTests),
      }
    })
}

function groupDurationSegments(day: TestDailySummary): BarSegment[] {
  const groups = (day.groups ?? []).slice().sort((a, b) => a.name.localeCompare(b.name))
  let cumulative = 0
  return groups
    .filter(g => g.durationMs > 0)
    .map(g => {
      const bottom = cumulative
      cumulative += g.durationMs / 1000
      return {
        name: g.name,
        y: yScale(cumulative),
        h: Math.max(1, yScale(bottom) - yScale(cumulative)),
        color: groupColor(g.name, g.totalTests, g.failedTests),
      }
    })
}

function failureRateColor(totalTests: number, failedTests: number): string {
  if (totalTests === 0) return '#4b5563'
  const rate = failedTests / totalTests
  if (rate <= 0) return '#22c55e'
  if (rate >= 1) return '#ef4444'
  if (rate < 0.5) {
    const t = rate / 0.5
    return `rgb(${Math.round(0x22 + t * (0xf5 - 0x22))},${Math.round(0xc5 + t * (0x9e - 0xc5))},${Math.round(0x5e + t * (0x0b - 0x5e))})`
  } else {
    const t = (rate - 0.5) / 0.5
    return `rgb(${Math.round(0xf5 + t * (0xef - 0xf5))},${Math.round(0x9e + t * (0x44 - 0x9e))},${Math.round(0x0b + t * (0x44 - 0x0b))})`
  }
}

// ── X axis ──────────────────────────────────────────────────────────────────

/** Whether a sparse day label should appear above the baseline for bar index i. */
function showDayLabel(i: number): boolean {
  const n = props.data.length
  if (n <= 10) return true
  if (n <= 21) return i % 2 === 0
  if (n <= 45) return i % 7 === 0
  return i % 14 === 0
}

function shortDay(d: string): string {
  const parts = d.split('-')
  return parts.length < 3 ? d : parseInt(parts[2]).toString()
}

interface XMarker { x: number; label: string | null; isBoundary: boolean }

/**
 * Build X-axis markers:
 * - Always shows the month name of the first data point at the chart left edge.
 * - Adds a boundary marker (vertical line + tick + label) for every month start (day=1).
 * - For short ranges (≤14 days) also adds unlabeled week tick marks every 7 days.
 * The start label is suppressed if a month boundary falls within 35 SVG units of the left edge
 * (to avoid text crowding).
 */
const xAxisMarkers = computed((): XMarker[] => {
  if (props.data.length === 0) return []
  const n = props.data.length
  const step = barStep.value
  const markers: XMarker[] = []

  // Collect all month boundary positions (day === 1 within the data)
  let firstBoundaryX = Infinity
  for (let i = 0; i < n; i++) {
    const parts = props.data[i].date.split('-')
    if (parts.length < 3) continue
    if (parseInt(parts[2]) === 1) {
      const x = padLeft + i * step
      markers.push({ x, label: monthName(parts[1]), isBoundary: true })
      if (x < firstBoundaryX) firstBoundaryX = x
    }
  }

  // Always show starting month unless a month boundary is very close to the left edge
  const startX = padLeft
  if (firstBoundaryX - startX > MIN_LABEL_SPACING) {
    const parts = props.data[0].date.split('-')
    // Insert at the front (no boundary line, just tick + label)
    markers.unshift({ x: startX, label: monthName(parts[1]), isBoundary: false })
  }

  // Week tick marks for short ranges (no label)
  if (n <= 14) {
    for (let i = DAYS_PER_WEEK; i < n; i += DAYS_PER_WEEK) {
      markers.push({ x: padLeft + i * step, label: null, isBoundary: true })
    }
  }

  return markers
})

// ── Tooltip ─────────────────────────────────────────────────────────────────

const tooltipDay = ref<TestDailySummary | null>(null)
const tooltipPos = ref({ x: 0, y: 0 })

function showTooltip(e: MouseEvent, day: TestDailySummary) {
  tooltipDay.value = day
  tooltipPos.value = { x: e.clientX, y: e.clientY }
}

function moveTooltip(e: MouseEvent) {
  tooltipPos.value = { x: e.clientX, y: e.clientY }
}

function hideTooltip() {
  tooltipDay.value = null
}

function formatTooltipDate(d: string): string {
  const parts = d.split('-')
  if (parts.length < 3) return d
  return `${parseInt(parts[2])} ${monthName(parts[1])} ${parts[0]}`
}

function formatDuration(ms: number): string {
  const s = ms / 1000
  if (s >= 3600) return `${Math.floor(s / 3600)}h ${Math.round((s % 3600) / 60)}m`
  if (s >= 60) return `${Math.round(s / 60)}m ${Math.round(s % 60)}s`
  return `${s.toFixed(1)}s`
}

// ── Y axis ───────────────────────────────────────────────────────────────────

function formatYLabel(v: number): string {
  if (props.yAxis === 'duration') {
    if (v >= 3600) return `${Math.floor(v / 3600)}h`
    if (v >= 60) return `${Math.round(v / 60)}m`
    return `${v}s`
  }
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(v)
}

const totalTests = computed(() => props.data.reduce((s, d) => s + d.totalTests, 0))
const totalRuns = computed(() => props.data.reduce((s, d) => s + d.runCount, 0))
</script>
