<template>
  <div>
    <div v-if="data.length === 0" class="py-8 text-center text-sm text-gray-500">
      No test results in this period
    </div>
    <template v-else>
      <div class="overflow-x-auto">
        <svg :viewBox="`0 0 ${svgWidth} ${svgHeight}`" class="w-full" :style="`min-width:${minWidth}px`">
          <!-- Y-axis grid lines and labels -->
          <line v-for="yv in gridY" :key="`grid-${yv}`"
            :x1="padLeft" :y1="yScale(yv)" :x2="svgWidth - padRight" :y2="yScale(yv)"
            stroke="#374151" stroke-width="1" />
          <text v-for="yv in gridY" :key="`yl-${yv}`"
            :x="padLeft - 4" :y="yScale(yv) + 4" text-anchor="end" fill="#6b7280" font-size="9">{{ formatYLabel(yv) }}</text>

          <!-- Bars -->
          <g v-for="(day, i) in data" :key="day.date">
            <!-- Pass-fail mode: stacked bars -->
            <template v-if="colorMode === 'pass-fail'">
              <!-- Pass-fail stacked mode: only available for count Y axis -->
              <template v-if="yAxis === 'count'">
                <!-- Failed portion (bottom, red) -->
                <rect v-if="barValue(day) > 0 && day.failedTests > 0"
                  :x="barX(i)"
                  :y="yScale(failedValue(day))"
                  :width="barW"
                  :height="plotH - yScale(failedValue(day)) + padTop"
                  fill="#ef4444"
                  opacity="0.85"
                  rx="1" />
                <!-- Passed portion (on top, green) -->
                <rect v-if="barValue(day) > 0 && day.passedTests > 0"
                  :x="barX(i)"
                  :y="yScale(barValue(day))"
                  :width="barW"
                  :height="yScale(failedValue(day)) - yScale(barValue(day))"
                  fill="#22c55e"
                  opacity="0.85"
                  rx="1" />
              </template>
              <!-- Duration mode with pass-fail: show single bar (can't split duration by outcome) -->
              <template v-else>
                <rect v-if="barValue(day) > 0"
                  :x="barX(i)"
                  :y="yScale(barValue(day))"
                  :width="barW"
                  :height="plotH - yScale(barValue(day)) + padTop"
                  fill="#6366f1"
                  opacity="0.85"
                  rx="1" />
              </template>
            </template>
            <!-- Failure-rate mode: single bar colored by failure % -->
            <template v-else>
              <rect v-if="barValue(day) > 0"
                :x="barX(i)"
                :y="yScale(barValue(day))"
                :width="barW"
                :height="plotH - yScale(barValue(day)) + padTop"
                :fill="failureRateColor(day)"
                opacity="0.85"
                rx="1" />
            </template>
            <!-- X-axis label (date) -->
            <text
              :x="barX(i) + barW / 2"
              :y="svgHeight - 2"
              text-anchor="middle" fill="#6b7280" font-size="8">{{ shortDate(day.date) }}</text>
          </g>
        </svg>
      </div>

      <!-- Legend -->
      <div class="flex flex-wrap items-center gap-4 mt-2">
        <template v-if="colorMode === 'pass-fail'">
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 bg-green-500 rounded-sm inline-block"></span> Passed
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 bg-red-500 rounded-sm inline-block"></span> Failed
          </span>
        </template>
        <template v-else>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-2 rounded-sm inline-block" style="background:linear-gradient(90deg,#22c55e,#ef4444)"></span> Low → High failure %
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
const svgHeight = 160
const padLeft = 32
const padRight = 8
const padTop = 8
const padBottom = 20
const minWidth = 300

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

function failedValue(day: TestDailySummary): number {
  if (props.yAxis === 'duration') return 0
  return day.failedTests
}

const maxVal = computed(() => {
  if (props.data.length === 0) return 1
  return Math.max(...props.data.map(d => barValue(d)), 1)
})

const gridY = computed(() => {
  const step = Math.ceil(maxVal.value / 3)
  if (step === 0) return [0]
  return [0, step, step * 2, step * 3].filter(v => v <= maxVal.value + step)
})

function yScale(val: number): number {
  return padTop + plotH - (val / maxVal.value) * plotH
}

function failureRateColor(day: TestDailySummary): string {
  if (day.totalTests === 0) return '#4b5563'
  const rate = day.failedTests / day.totalTests
  // Interpolate green (#22c55e) → yellow (#f59e0b) → red (#ef4444)
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

function shortDate(d: string): string {
  // d = "2024-01-15" → "1/15"
  const parts = d.split('-')
  if (parts.length < 3) return d
  return `${parseInt(parts[1])}/${parseInt(parts[2])}`
}

function formatYLabel(v: number): string {
  if (props.yAxis === 'duration') return `${v}s`
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(v)
}

const totalTests = computed(() => props.data.reduce((s, d) => s + d.totalTests, 0))
const totalRuns = computed(() => props.data.reduce((s, d) => s + d.runCount, 0))
</script>
