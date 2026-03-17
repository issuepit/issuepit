<template>
  <div class="relative" @mouseleave="hideTooltip">
    <div v-if="displayBars.length === 0" class="py-8 text-center text-sm text-gray-500">
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

          <!-- X-axis boundary markers -->
          <g v-for="marker in xAxisMarkers" :key="`xm-${marker.x}`">
            <line v-if="marker.isBoundary"
              :x1="marker.x" :y1="padTop" :x2="marker.x" :y2="baseline"
              stroke="#1f2937" stroke-width="1" />
            <line :x1="marker.x" :y1="baseline" :x2="marker.x" :y2="baseline + 4"
              stroke="#4b5563" stroke-width="1.5" />
            <text v-if="marker.label"
              :x="marker.x + 2" :y="baseline + 22"
              text-anchor="start" fill="#9ca3af" font-size="8.5" font-weight="600">{{ marker.label }}</text>
          </g>

          <!-- Bars -->
          <g v-for="(bar, i) in displayBars" :key="`bar-${i}`">
            <template v-if="bar !== null">
              <!-- Groups mode: stacked per artifact, colored by group failure rate -->
              <template v-if="colorMode === 'groups'">
                <rect v-for="seg in groupSegments(bar)" :key="seg.name"
                  :x="barX(i)" :y="seg.y" :width="barW" :height="seg.h"
                  :fill="seg.color" opacity="0.88" rx="1" />
              </template>
              <!-- Pass-fail stacked -->
              <template v-else-if="colorMode === 'pass-fail'">
                <template v-if="yAxis === 'count'">
                  <rect v-if="barTotalVal(bar) > 0 && bar.failedTests > 0"
                    :x="barX(i)" :y="yScale(bar.failedTests)" :width="barW"
                    :height="plotH - yScale(bar.failedTests) + padTop"
                    fill="#ef4444" opacity="0.85" rx="1" />
                  <rect v-if="barTotalVal(bar) > 0 && bar.passedTests > 0"
                    :x="barX(i)" :y="yScale(barTotalVal(bar))" :width="barW"
                    :height="yScale(bar.failedTests) - yScale(barTotalVal(bar))"
                    fill="#22c55e" opacity="0.85" rx="1" />
                </template>
                <!-- Duration Y: split proportionally by pass/fail count ratio -->
                <template v-else>
                  <template v-if="barTotalVal(bar) > 0">
                    <rect v-if="bar.failedTests > 0 && bar.totalTests > 0"
                      :x="barX(i)" :y="yScale(failedDurVal(bar))" :width="barW"
                      :height="plotH - yScale(failedDurVal(bar)) + padTop"
                      fill="#ef4444" opacity="0.85" rx="1" />
                    <rect v-if="bar.passedTests > 0 && bar.totalTests > 0"
                      :x="barX(i)" :y="yScale(barTotalVal(bar))" :width="barW"
                      :height="yScale(failedDurVal(bar)) - yScale(barTotalVal(bar))"
                      fill="#22c55e" opacity="0.85" rx="1" />
                    <rect v-if="bar.totalTests === 0"
                      :x="barX(i)" :y="yScale(barTotalVal(bar))" :width="barW"
                      :height="plotH - yScale(barTotalVal(bar)) + padTop"
                      fill="#6366f1" opacity="0.85" rx="1" />
                  </template>
                </template>
              </template>
              <!-- Failure-rate: single bar colored by failure % -->
              <template v-else>
                <rect v-if="barTotalVal(bar) > 0"
                  :x="barX(i)" :y="yScale(barTotalVal(bar))" :width="barW"
                  :height="plotH - yScale(barTotalVal(bar)) + padTop"
                  :fill="failureRateColor(bar.totalTests, bar.failedTests)"
                  opacity="0.85" rx="1" />
              </template>
            </template>

            <!-- X axis label (sparse) -->
            <text v-if="bar !== null && showBarLabel(i)"
              :x="barX(i) + barW / 2" :y="baseline + 10"
              text-anchor="middle" fill="#6b7280" font-size="7.5">{{ barLabel(i) }}</text>
          </g>

          <!-- Invisible hover rects -->
          <rect v-for="(bar, i) in displayBars" :key="`hover-${i}`"
            :x="padLeft + i * barStep" :y="padTop"
            :width="barStep" :height="plotH"
            fill="transparent"
            style="cursor:crosshair"
            @mouseenter="e => bar !== null && showTooltip(e, bar)"
            @mousemove="moveTooltip"
            @mouseleave="hideTooltip" />
        </svg>
      </div>

      <!-- Legend -->
      <div class="flex flex-wrap items-center gap-3 mt-2">
        <template v-if="colorMode === 'groups'">
          <span v-for="(g, gi) in legendGroups" :key="g.name" class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-8 h-2 rounded-sm inline-block shrink-0"
              :style="`background:linear-gradient(90deg,${groupHealthyColor(gi)},hsl(0,30%,28%))`"></span>
            {{ g.name }}
          </span>
          <span class="text-xs text-gray-600">(vivid = healthy, dark red = failing)</span>
        </template>
        <template v-else-if="colorMode === 'pass-fail'">
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 bg-green-500 rounded-sm inline-block"></span> Passed
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 bg-red-500 rounded-sm inline-block"></span> Failed
          </span>
          <span v-if="yAxis === 'duration'" class="text-xs text-gray-600">(split by pass/fail ratio)</span>
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

      <!-- Tooltip -->
      <teleport to="body">
        <div v-if="tooltipBar"
          class="fixed z-[9999] pointer-events-none bg-gray-900 border border-gray-700 rounded-lg shadow-xl text-xs p-2.5 min-w-[160px]"
          :style="`left:${tooltipPos.x + 14}px;top:${tooltipPos.y - 8}px;transform:translateY(-100%)`">
          <div class="font-medium text-gray-200 mb-1.5">{{ tooltipTitle }}</div>
          <div class="space-y-0.5">
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Total</span>
              <span class="text-gray-200">{{ tooltipBar.totalTests.toLocaleString() }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-green-400">Passed</span>
              <span class="text-gray-200">{{ tooltipBar.passedTests.toLocaleString() }}</span>
            </div>
            <div v-if="tooltipBar.failedTests > 0" class="flex justify-between gap-3">
              <span class="text-red-400">Failed</span>
              <span class="text-gray-200">{{ tooltipBar.failedTests.toLocaleString() }}</span>
            </div>
            <div v-if="tooltipBar.skippedTests > 0" class="flex justify-between gap-3">
              <span class="text-yellow-400">Skipped</span>
              <span class="text-gray-200">{{ tooltipBar.skippedTests.toLocaleString() }}</span>
            </div>
            <div class="flex justify-between gap-3">
              <span class="text-gray-400">Duration</span>
              <span class="text-gray-200">{{ formatDuration(tooltipBar.durationMs) }}</span>
            </div>
            <div v-if="isDaily(tooltipBar)" class="flex justify-between gap-3">
              <span class="text-gray-400">Runs</span>
              <span class="text-gray-200">{{ (tooltipBar as TestDailySummary).runCount }}</span>
            </div>
            <template v-if="!isDaily(tooltipBar)">
              <div v-if="(tooltipBar as TestRunSummary).branch" class="flex justify-between gap-3">
                <span class="text-gray-400">Branch</span>
                <span class="text-gray-200 truncate max-w-[100px]">{{ (tooltipBar as TestRunSummary).branch }}</span>
              </div>
              <div v-if="(tooltipBar as TestRunSummary).commitSha" class="flex justify-between gap-3">
                <span class="text-gray-400">Commit</span>
                <span class="text-gray-200 font-mono">{{ (tooltipBar as TestRunSummary).commitSha?.slice(0, 7) }}</span>
              </div>
            </template>
            <template v-if="colorMode === 'groups' && tooltipBar.groups?.length">
              <div class="border-t border-gray-700 mt-1.5 pt-1.5 space-y-0.5">
                <div v-for="g in tooltipBar.groups.filter(g => g.totalTests > 0)" :key="g.name"
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
import type { TestDailySummary, TestRunSummary } from '~/types'

type BarEntry = TestDailySummary | TestRunSummary

const props = defineProps<{
  data: TestDailySummary[]
  runsData?: TestRunSummary[]
  colorMode: string
  yAxis: string
  xMode?: 'date' | 'runs'
}>()

// ── Layout ────────────────────────────────────────────────────────────────────

const svgWidth = 600
const svgHeight = 180
const padLeft = 36
const padRight = 8
const padTop = 8
const padBottom = 30
const minWidth = 300

const MIN_LABEL_SPACING = 35
const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

function monthName(monthStr: string): string {
  const idx = parseInt(monthStr) - 1
  return MONTH_NAMES[idx >= 0 && idx < 12 ? idx : 0]
}

const plotW = svgWidth - padLeft - padRight
const plotH = svgHeight - padTop - padBottom
const baseline = padTop + plotH
const barGap = 2

// ── Mode ──────────────────────────────────────────────────────────────────────

const effectiveXMode = computed(() => props.xMode ?? 'date')

function isDaily(bar: BarEntry): boolean {
  return 'date' in bar
}

/**
 * In date mode: fill every calendar day in range, inserting null for days with no runs (gap columns).
 * In runs mode: reverse runsData so oldest run is on the left.
 */
const displayBars = computed((): (BarEntry | null)[] => {
  if (effectiveXMode.value === 'runs') {
    return (props.runsData ?? []).slice().reverse()
  }
  if (props.data.length === 0) return []
  const byDate = new Map(props.data.map(d => [d.date, d]))
  const firstMs = new Date(props.data[0].date).getTime()
  const lastMs = new Date(props.data[props.data.length - 1].date).getTime()
  const MS_PER_DAY = 86_400_000
  const result: (BarEntry | null)[] = []
  for (let ms = firstMs; ms <= lastMs; ms += MS_PER_DAY) {
    const key = new Date(ms).toISOString().slice(0, 10)
    result.push(byDate.get(key) ?? null)
  }
  return result
})

// ── Geometry ──────────────────────────────────────────────────────────────────

const barStep = computed(() => displayBars.value.length === 0 ? plotW : plotW / displayBars.value.length)
const barW = computed(() => Math.max(2, barStep.value - barGap))
function barX(i: number): number { return padLeft + i * barStep.value + barGap / 2 }

// ── Values ────────────────────────────────────────────────────────────────────

function barTotalVal(bar: BarEntry): number {
  return props.yAxis === 'duration' ? bar.durationMs / 1000 : bar.totalTests
}

/** Proportional failed-duration: total duration * (failed / total tests), clamped to [0, totalDur]. */
function failedDurVal(bar: BarEntry): number {
  if (bar.totalTests <= 0) return 0
  const ratio = Math.max(0, Math.min(1, bar.failedTests / bar.totalTests))
  return (bar.durationMs / 1000) * ratio
}

const maxVal = computed(() => {
  const vals = displayBars.value.map(b => (b === null ? 0 : barTotalVal(b)))
  return Math.max(...vals, 1)
})

const gridY = computed(() => {
  const step = Math.ceil(maxVal.value / 4)
  if (step === 0) return [0]
  return [0, step, step * 2, step * 3, step * 4].filter(v => v <= maxVal.value + step * 0.5)
})

function yScale(val: number): number { return padTop + plotH - (val / maxVal.value) * plotH }
const yAxisLabel = computed(() => props.yAxis === 'duration' ? 'sec' : 'tests')

// ── Group coloring ────────────────────────────────────────────────────────────

const groupNames = computed(() => {
  const names = new Set<string>()
  for (const bar of displayBars.value) {
    for (const g of bar?.groups ?? []) names.add(g.name)
  }
  return [...names].sort()
})

const GROUP_COLOR_HUES = [210, 160, 280, 35, 0, 60, 300, 120]
function groupBaseHue(idx: number): number { return GROUP_COLOR_HUES[idx % GROUP_COLOR_HUES.length] }
function groupHealthyColor(idx: number): string { return `hsl(${groupBaseHue(idx)},70%,48%)` }

function groupColor(name: string, totalTests: number, failedTests: number): string {
  const idx = groupNames.value.indexOf(name)
  const hue = groupBaseHue(idx)
  if (totalTests === 0) return `hsl(${hue}, 20%, 30%)`
  const failRate = Math.min(1, failedTests / totalTests)
  return `hsl(${Math.round(hue * (1 - failRate))}, ${Math.round(70 - failRate * 40)}%, ${Math.round(48 - failRate * 20)}%)`
}

const legendGroups = computed(() => {
  const totals = new Map<string, { total: number; failed: number }>()
  for (const bar of displayBars.value) {
    for (const g of bar?.groups ?? []) {
      const t = totals.get(g.name) ?? { total: 0, failed: 0 }
      t.total += g.totalTests; t.failed += g.failedTests
      totals.set(g.name, t)
    }
  }
  return groupNames.value.map(name => {
    const t = totals.get(name) ?? { total: 0, failed: 0 }
    return { name, color: groupColor(name, t.total, t.failed) }
  })
})

// ── Bar segments ──────────────────────────────────────────────────────────────

interface BarSegment { name: string; y: number; h: number; color: string }

function groupSegments(bar: BarEntry): BarSegment[] {
  const groups = (bar.groups ?? []).slice().sort((a, b) => a.name.localeCompare(b.name))
  let cum = 0
  return groups
    .filter(g => (props.yAxis === 'duration' ? g.durationMs > 0 : g.totalTests > 0))
    .map(g => {
      const val = props.yAxis === 'duration' ? g.durationMs / 1000 : g.totalTests
      const bottom = cum; cum += val
      return { name: g.name, y: yScale(cum), h: Math.max(1, yScale(bottom) - yScale(cum)), color: groupColor(g.name, g.totalTests, g.failedTests) }
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
  }
  const t = (rate - 0.5) / 0.5
  return `rgb(${Math.round(0xf5 + t * (0xef - 0xf5))},${Math.round(0x9e + t * (0x44 - 0x9e))},${Math.round(0x0b + t * (0x44 - 0x0b))})`
}

// ── X axis ────────────────────────────────────────────────────────────────────

function showBarLabel(i: number): boolean {
  const n = displayBars.value.length
  if (n <= 10) return true
  if (n <= 21) return i % 2 === 0
  if (n <= 45) return i % 7 === 0
  return i % 14 === 0
}

function barLabel(i: number): string {
  const bar = displayBars.value[i]
  if (!bar) return ''
  if (!isDaily(bar)) {
    const d = new Date((bar as TestRunSummary).startedAt)
    return `${MONTH_NAMES[d.getMonth()]} ${d.getDate()}`
  }
  const parts = (bar as TestDailySummary).date.split('-')
  return parts.length < 3 ? '' : parseInt(parts[2]).toString()
}

interface XMarker { x: number; label: string | null; isBoundary: boolean }

const xAxisMarkers = computed((): XMarker[] => {
  const bars = displayBars.value
  if (bars.length === 0) return []
  const n = bars.length
  const step = barStep.value
  const markers: XMarker[] = []

  if (effectiveXMode.value === 'runs') {
    // Runs mode: one tick per new day
    let lastDate = ''
    for (let i = 0; i < n; i++) {
      const bar = bars[i]
      if (!bar) continue
      const dateStr = new Date((bar as TestRunSummary).startedAt).toISOString().slice(0, 10)
      if (dateStr !== lastDate) {
        lastDate = dateStr
        const x = padLeft + i * step
        const parts = dateStr.split('-')
        const label = `${parseInt(parts[2])} ${monthName(parts[1])}`
        const last = markers[markers.length - 1]
        markers.push({ x, label: (!last || x - last.x > MIN_LABEL_SPACING) ? label : null, isBoundary: i > 0 })
      }
    }
    return markers
  }

  // Date mode: month starts + Monday week ticks
  let firstBoundaryX = Infinity
  for (let i = 0; i < n; i++) {
    const bar = bars[i]
    if (!bar) continue
    const parts = (bar as TestDailySummary).date.split('-')
    if (parts.length < 3) continue
    if (parseInt(parts[2]) === 1) {
      const x = padLeft + i * step
      markers.push({ x, label: monthName(parts[1]), isBoundary: true })
      if (x < firstBoundaryX) firstBoundaryX = x
    }
  }
  // Always show start month unless crowded
  const startX = padLeft
  if (firstBoundaryX - startX > MIN_LABEL_SPACING && bars[0]) {
    const parts = (bars[0] as TestDailySummary).date.split('-')
    markers.unshift({ x: startX, label: monthName(parts[1]), isBoundary: false })
  }
  // Monday week ticks (only for ranges ≤45 days)
  // Build a dayOfWeek lookup once to avoid allocating a Date per bar
  if (n <= 45) {
    const dayOfWeekCache = new Map<string, number>()
    for (let i = 0; i < n; i++) {
      const bar = bars[i]
      if (!bar) continue
      const date = (bar as TestDailySummary).date
      const parts = date.split('-')
      if (parseInt(parts[2]) === 1) continue // already a month boundary
      let dow = dayOfWeekCache.get(date)
      if (dow === undefined) {
        dow = new Date(date).getDay()
        dayOfWeekCache.set(date, dow)
      }
      // Week starts on Monday (getDay() === 1)
      if (dow === 1) {
        const x = padLeft + i * step
        if (!markers.find(m => Math.abs(m.x - x) < 4)) {
          markers.push({ x, label: null, isBoundary: true })
        }
      }
    }
  }
  return markers
})

// ── Tooltip ───────────────────────────────────────────────────────────────────

const tooltipBar = ref<BarEntry | null>(null)
const tooltipPos = ref({ x: 0, y: 0 })

const tooltipTitle = computed(() => {
  if (!tooltipBar.value) return ''
  if (isDaily(tooltipBar.value)) {
    const parts = (tooltipBar.value as TestDailySummary).date.split('-')
    return parts.length < 3 ? '' : `${parseInt(parts[2])} ${monthName(parts[1])} ${parts[0]}`
  }
  const d = new Date((tooltipBar.value as TestRunSummary).startedAt)
  const hh = d.getHours().toString().padStart(2, '0')
  const mm = d.getMinutes().toString().padStart(2, '0')
  return `${d.getDate()} ${MONTH_NAMES[d.getMonth()]} ${d.getFullYear()}, ${hh}:${mm}`
})

function showTooltip(e: MouseEvent, bar: BarEntry) {
  tooltipBar.value = bar
  tooltipPos.value = { x: e.clientX, y: e.clientY }
}
function moveTooltip(e: MouseEvent) { tooltipPos.value = { x: e.clientX, y: e.clientY } }
function hideTooltip() { tooltipBar.value = null }

function formatDuration(ms: number): string {
  const s = ms / 1000
  if (s >= 3600) return `${Math.floor(s / 3600)}h ${Math.round((s % 3600) / 60)}m`
  if (s >= 60) return `${Math.round(s / 60)}m ${Math.round(s % 60)}s`
  return `${s.toFixed(1)}s`
}

// ── Y axis label ──────────────────────────────────────────────────────────────

function formatYLabel(v: number): string {
  if (props.yAxis === 'duration') {
    if (v >= 3600) return `${Math.floor(v / 3600)}h`
    if (v >= 60) return `${Math.round(v / 60)}m`
    return `${v}s`
  }
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(v)
}

const totalTests = computed(() => displayBars.value.reduce((s, b) => s + (b?.totalTests ?? 0), 0))
const totalRuns = computed(() => {
  if (effectiveXMode.value === 'runs') return displayBars.value.filter(b => b !== null).length
  return displayBars.value.reduce((s, b) => s + ((b as TestDailySummary | null)?.runCount ?? 0), 0)
})
</script>
