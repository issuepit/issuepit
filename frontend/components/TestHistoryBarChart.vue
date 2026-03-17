<template>
  <div>
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

          <!-- Month / week boundary markers on X axis -->
          <g v-for="marker in xAxisMarkers" :key="`xm-${marker.x}`">
            <line :x1="marker.x" y1="0" :x2="marker.x" :y2="svgHeight - padBottom"
              stroke="#1f2937" stroke-width="1" />
            <text v-if="marker.label"
              :x="marker.x + 2" :y="svgHeight - padBottom + 10"
              text-anchor="start" fill="#9ca3af" font-size="8.5" font-weight="600">{{ marker.label }}</text>
          </g>

          <!-- Bars -->
          <g v-for="(day, i) in data" :key="day.date">
            <!-- Groups mode: stacked per artifact, colored by group failure rate -->
            <template v-if="colorMode === 'groups'">
              <template v-if="yAxis === 'count'">
                <rect v-for="seg in groupSegments(day, i)" :key="seg.name"
                  :x="barX(i)" :y="seg.y" :width="barW" :height="seg.h"
                  :fill="seg.color" opacity="0.88" rx="1" />
              </template>
              <!-- Duration Y: single bar per group (stacked) colored by failure rate -->
              <template v-else>
                <rect v-for="seg in groupDurationSegments(day, i)" :key="seg.name"
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

            <!-- X axis day label (sparse) -->
            <text v-if="showDayLabel(i)"
              :x="barX(i) + barW / 2"
              :y="svgHeight - padBottom + 9"
              text-anchor="middle" fill="#6b7280" font-size="7.5">{{ shortDay(day.date) }}</text>
          </g>
        </svg>
      </div>

      <!-- Legend -->
      <div class="flex flex-wrap items-center gap-3 mt-2">
        <template v-if="colorMode === 'groups'">
          <span v-for="g in legendGroups" :key="g.name" class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 rounded-sm inline-block shrink-0" :style="`background:${g.color}`"></span>
            {{ g.name }}
          </span>
        </template>
        <template v-else-if="colorMode === 'pass-fail'">
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 bg-green-500 rounded-sm inline-block"></span> Passed
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 bg-red-500 rounded-sm inline-block"></span> Failed
          </span>
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
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { TestDailySummary } from '~/types'

const props = defineProps<{
  data: TestDailySummary[]
  colorMode: string
  yAxis: string
}>()

const svgWidth = 600
const svgHeight = 170
const padLeft = 36
const padRight = 8
const padTop = 8
// Extra space for X axis labels + month markers
const padBottom = 22
const minWidth = 300

const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const DAYS_PER_WEEK = 7

const plotW = svgWidth - padLeft - padRight
const plotH = svgHeight - padTop - padBottom

const barGap = 2

const barW = computed(() => {
  if (props.data.length === 0) return 10
  return Math.max(2, (plotW / props.data.length) - barGap)
})

function barX(i: number): number {
  const step = plotW / props.data.length
  return padLeft + i * step + barGap / 2
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

const yAxisLabel = computed(() => {
  if (props.yAxis === 'duration') return 'sec'
  return 'tests'
})

/** Stable ordered list of unique group names across all days. */
const groupNames = computed(() => {
  const names = new Set<string>()
  for (const day of props.data) {
    for (const g of day.groups ?? []) names.add(g.name)
  }
  return [...names].sort()
})

/** Assign a stable base hue per group index. */
const GROUP_COLOR_HUES = [210, 160, 280, 35, 0, 60, 300, 120]
function groupBaseHue(idx: number): number {
  return GROUP_COLOR_HUES[idx % GROUP_COLOR_HUES.length]
}

/** Color a group segment by its failure rate: hue stays fixed, saturation/lightness shifts red on failure. */
function groupColor(name: string, totalTests: number, failedTests: number): string {
  const idx = groupNames.value.indexOf(name)
  const hue = groupBaseHue(idx)
  if (totalTests === 0) return `hsl(${hue}, 20%, 30%)`
  const failRate = Math.min(1, failedTests / totalTests)
  // Low failure → full saturation vivid color; high failure → deep red tint
  const sat = Math.round(70 - failRate * 40)
  const lit = Math.round(48 - failRate * 20)
  // Blend towards red (hue 0) as failure rate climbs
  const blendedHue = Math.round(hue * (1 - failRate) + 0 * failRate)
  return `hsl(${blendedHue}, ${sat}%, ${lit}%)`
}

/** Legend entries for groups mode. */
const legendGroups = computed(() => {
  // Compute aggregate failure rate per group across all days
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

interface BarSegment { name: string; y: number; h: number; color: string }

/** Build stacked count segments for a day in groups mode. */
function groupSegments(day: TestDailySummary, _i: number): BarSegment[] {
  const groups = (day.groups ?? []).slice().sort((a, b) => a.name.localeCompare(b.name))
  let cumulative = 0
  return groups
    .filter(g => g.totalTests > 0)
    .map(g => {
      const bottom = cumulative
      cumulative += g.totalTests
      const yTop = yScale(cumulative)
      const yBot = yScale(bottom)
      return {
        name: g.name,
        y: yTop,
        h: Math.max(1, yBot - yTop),
        color: groupColor(g.name, g.totalTests, g.failedTests),
      }
    })
}

/** Build stacked duration segments for a day in groups mode (duration Y). */
function groupDurationSegments(day: TestDailySummary, _i: number): BarSegment[] {
  const groups = (day.groups ?? []).slice().sort((a, b) => a.name.localeCompare(b.name))
  let cumulative = 0
  return groups
    .filter(g => g.durationMs > 0)
    .map(g => {
      const bottom = cumulative
      cumulative += g.durationMs / 1000
      const yTop = yScale(cumulative)
      const yBot = yScale(bottom)
      return {
        name: g.name,
        y: yTop,
        h: Math.max(1, yBot - yTop),
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
    const r = Math.round(0x22 + t * (0xf5 - 0x22))
    const g = Math.round(0xc5 + t * (0x9e - 0xc5))
    const b = Math.round(0x5e + t * (0x0b - 0x5e))
    return `rgb(${r},${g},${b})`
  } else {
    const t = (rate - 0.5) / 0.5
    const r = Math.round(0xf5 + t * (0xef - 0xf5))
    const g = Math.round(0x9e + t * (0x44 - 0x9e))
    const b = Math.round(0x0b + t * (0x44 - 0x0b))
    return `rgb(${r},${g},${b})`
  }
}

/** Compute which day indices should show a day label.
 *  - ≤ 10 days: every day
 *  - ≤ 21 days: every 2nd day
 *  - ≤ 45 days: every 7th day (weekly)
 *  - > 45 days: every 14th day (biweekly)
 *  Additionally we suppress any label that would collide with a month marker.
 */
function showDayLabel(i: number): boolean {
  const n = props.data.length
  if (n <= 10) return true
  if (n <= 21) return i % 2 === 0
  if (n <= 45) return i % 7 === 0
  return i % 14 === 0
}

/** Short day label: "15" (day of month only) to avoid duplication with month marker. */
function shortDay(d: string): string {
  const parts = d.split('-')
  if (parts.length < 3) return d
  return parseInt(parts[2]).toString()
}

interface XMarker { x: number; label: string | null }

/** Build month and week boundary markers for the X axis. */
const xAxisMarkers = computed((): XMarker[] => {
  if (props.data.length === 0) return []
  const n = props.data.length
  const step = plotW / n
  const markers: XMarker[] = []

  for (let i = 0; i < n; i++) {
    const d = props.data[i].date
    const parts = d.split('-')
    if (parts.length < 3) continue
    const day = parseInt(parts[2])
    const month = parseInt(parts[1]) - 1
    const x = padLeft + i * step

    // Month boundary: first day of month
    if (day === 1) {
      markers.push({ x, label: MONTH_NAMES[month] })
    } else if (n <= 14 && i > 0) {
      // For short ranges: add week boundary marker every 7 days
      if (i % DAYS_PER_WEEK === 0) markers.push({ x, label: null })
    }
  }
  return markers
})

function formatYLabel(v: number): string {
  if (props.yAxis === 'duration') {
    if (v >= 3600) return `${(v / 3600).toFixed(1)}h`
    if (v >= 60) return `${Math.round(v / 60)}m`
    return `${v}s`
  }
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(v)
}

const totalTests = computed(() => props.data.reduce((s, d) => s + d.totalTests, 0))
const totalRuns = computed(() => props.data.reduce((s, d) => s + d.runCount, 0))
</script>
