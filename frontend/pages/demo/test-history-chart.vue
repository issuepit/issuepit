<template>
  <div class="p-6 max-w-5xl">
    <div class="flex items-center gap-3 mb-6">
      <NuxtLink to="/demo" class="text-gray-500 hover:text-gray-300 text-sm">← Demo</NuxtLink>
      <span class="text-gray-700">/</span>
      <h1 class="text-xl font-bold text-white">Test History Chart</h1>
    </div>

    <!-- Controls -->
    <div class="flex flex-wrap items-center gap-4 mb-6 bg-gray-900 border border-gray-800 rounded-xl p-4">
      <div class="flex items-center gap-2">
        <span class="text-xs text-gray-400">Color mode</span>
        <button v-for="m in COLOR_MODES" :key="m.value"
          @click="colorMode = m.value"
          :class="colorMode === m.value ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-300 hover:bg-gray-700'"
          class="text-xs px-2 py-1 rounded transition-colors">{{ m.label }}</button>
      </div>
      <div class="flex items-center gap-2">
        <span class="text-xs text-gray-400">Y axis</span>
        <button v-for="y in Y_AXES" :key="y.value"
          @click="yAxis = y.value"
          :class="yAxis === y.value ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-300 hover:bg-gray-700'"
          class="text-xs px-2 py-1 rounded transition-colors">{{ y.label }}</button>
      </div>
      <div class="flex items-center gap-2">
        <span class="text-xs text-gray-400">X mode</span>
        <button v-for="x in X_MODES" :key="x.value"
          @click="xMode = x.value"
          :class="xMode === x.value ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-300 hover:bg-gray-700'"
          class="text-xs px-2 py-1 rounded transition-colors">{{ x.label }}</button>
      </div>
      <div class="flex items-center gap-2">
        <span class="text-xs text-gray-400">Range</span>
        <button v-for="r in RANGES" :key="r.label"
          @click="range = r.days"
          :class="range === r.days ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-300 hover:bg-gray-700'"
          class="text-xs px-2 py-1 rounded transition-colors">{{ r.label }}</button>
      </div>
    </div>

    <!-- Chart preview -->
    <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-8">
      <div class="flex items-center justify-between mb-3">
        <h2 class="font-semibold text-white flex items-center gap-2 text-sm">
          <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          Test History
          <span class="text-xs font-normal text-gray-500">({{ range }}d · mock data)</span>
        </h2>
        <span class="text-xs text-gray-500 bg-gray-800 px-2 py-0.5 rounded">{{ colorModeLabel }} · {{ yAxisLabel }} · {{ xModeLabel }}</span>
      </div>
      <TestHistoryBarChart
        :data="filteredData"
        :runs-data="mockRunsData"
        :color-mode="colorMode"
        :y-axis="yAxis"
        :x-mode="xMode"
      />
    </div>

    <!-- All variants grid -->
    <h2 class="text-lg font-semibold text-white mb-4">All Variants (static snapshot — 30 days, with gaps)</h2>
    <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
      <div v-for="v in variants" :key="v.title"
        class="bg-gray-900 border border-gray-800 rounded-xl p-4">
        <div class="text-xs font-medium text-gray-400 mb-3">{{ v.title }}</div>
        <TestHistoryBarChart
          :data="filteredData"
          :runs-data="mockRunsData"
          :color-mode="v.colorMode"
          :y-axis="v.yAxis"
          :x-mode="v.xMode ?? 'date'"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { TestDailySummary, TestRunSummary } from '~/types'

// ── Constants ──────────────────────────────────────────────────────────────────

const COLOR_MODES = [
  { value: 'failure-rate', label: 'Fail %' },
  { value: 'pass-fail', label: 'Pass/Fail' },
  { value: 'groups', label: 'Groups' },
]
const Y_AXES = [
  { value: 'count', label: 'Count' },
  { value: 'duration', label: 'Time' },
]
const X_MODES = [
  { value: 'date', label: 'Date' },
  { value: 'runs', label: 'Runs' },
]
const RANGES = [
  { label: '14d', days: 14 },
  { label: '30d', days: 30 },
  { label: '60d', days: 60 },
]

// ── Controls ───────────────────────────────────────────────────────────────────

const colorMode = ref('failure-rate')
const yAxis = ref('count')
const xMode = ref<'date' | 'runs'>('date')
const range = ref(30)

const colorModeLabel = computed(() => COLOR_MODES.find(m => m.value === colorMode.value)?.label ?? '')
const yAxisLabel = computed(() => Y_AXES.find(y => y.value === yAxis.value)?.label ?? '')
const xModeLabel = computed(() => X_MODES.find(x => x.value === xMode.value)?.label ?? '')

// ── Mock data ──────────────────────────────────────────────────────────────────

/** Generate a date string YYYY-MM-DD for `daysAgo` days before today. */
function daysAgo(n: number): string {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return d.toISOString().slice(0, 10)
}

/**
 * Deterministic pseudo-random number in [0,1) seeded by n.
 * Uses a hash-based GLSL-style formula: sin(n * prime + offset) * large_prime.
 * The specific constants (127.1, 311.7, 43758.5453) are a common WebGL noise recipe.
 */
function prng(n: number): number {
  return ((Math.sin(n * 127.1 + 311.7) * 43758.5453) % 1 + 1) % 1
}

/** Generate a deterministic 40-character hex SHA from a numeric seed. */
function generateMockSha(seed: number): string {
  return Array.from({ length: 40 }, (_, k) => '0123456789abcdef'[Math.floor(prng(seed * 17 + k) * 16)]).join('')
}

/** Build a realistic-looking 60-day daily summary with ~15% missing days (gaps). */
const allMockData: TestDailySummary[] = (() => {
  const data: TestDailySummary[] = []
  const groups = ['unit', 'integration', 'e2e']
  for (let i = 59; i >= 0; i--) {
    // Skip ~15% of days to create realistic gaps
    if (prng(i * 3 + 1) < 0.15) continue
    const baseUnit = Math.round(80 + prng(i) * 20)
    const baseInteg = Math.round(60 + prng(i + 100) * 15)
    const baseE2e = Math.round(40 + prng(i + 200) * 10)
    const unitFail = Math.round(baseUnit * prng(i + 300) * 0.12)
    const integFail = Math.round(baseInteg * prng(i + 400) * 0.08)
    const e2eFail = Math.round(baseE2e * prng(i + 500) * 0.20)
    const total = baseUnit + baseInteg + baseE2e
    const failed = unitFail + integFail + e2eFail
    const passed = total - failed - Math.round(total * 0.01)
    const runCount = 1 + Math.round(prng(i + 600) * 2)
    data.push({
      date: daysAgo(i),
      totalTests: total,
      passedTests: passed,
      failedTests: failed,
      skippedTests: total - passed - failed,
      durationMs: Math.round((total * 150 + prng(i + 700) * 5000) * runCount),
      runCount,
      groups: [
        { name: groups[0], totalTests: baseUnit, passedTests: baseUnit - unitFail, failedTests: unitFail, skippedTests: 0, durationMs: Math.round(baseUnit * 80) },
        { name: groups[1], totalTests: baseInteg, passedTests: baseInteg - integFail, failedTests: integFail, skippedTests: 0, durationMs: Math.round(baseInteg * 200) },
        { name: groups[2], totalTests: baseE2e, passedTests: baseE2e - e2eFail, failedTests: e2eFail, skippedTests: 0, durationMs: Math.round(baseE2e * 500) },
      ],
    })
  }
  return data
})()

/** Build per-run mock data (newest first from API, chart reverses to oldest-first). */
const mockRunsData: TestRunSummary[] = (() => {
  const runs: TestRunSummary[] = []
  const branches = ['main', 'main', 'main', 'feat/auth', 'main', 'fix/perf', 'main']
  const groups = ['unit', 'integration', 'e2e']
  for (let i = 0; i < 30; i++) {
    const dayOffset = Math.round(i * 1.5)
    const base = 160 + Math.round(prng(i * 7) * 20)
    const failed = Math.round(base * prng(i * 13) * 0.10)
    const passed = base - failed
    const grpUnit = Math.round(base * 0.45)
    const grpInteg = Math.round(base * 0.35)
    const grpE2e = base - grpUnit - grpInteg
    runs.push({
      runId: `run-${i}`,
      commitSha: generateMockSha(i),
      branch: branches[i % branches.length],
      startedAt: new Date(Date.now() - dayOffset * 86_400_000 - Math.round(prng(i * 11) * 3_600_000)).toISOString(),
      statusName: failed > 0 ? 'Failed' : 'Succeeded',
      totalTests: base,
      passedTests: passed,
      failedTests: failed,
      skippedTests: 0,
      durationMs: Math.round(base * 150 + prng(i * 19) * 5000),
      suiteCount: 3,
      groups: [
        { name: groups[0], totalTests: grpUnit, passedTests: grpUnit - Math.round(failed * 0.4), failedTests: Math.round(failed * 0.4), skippedTests: 0, durationMs: Math.round(grpUnit * 80) },
        { name: groups[1], totalTests: grpInteg, passedTests: grpInteg - Math.round(failed * 0.3), failedTests: Math.round(failed * 0.3), skippedTests: 0, durationMs: Math.round(grpInteg * 200) },
        { name: groups[2], totalTests: grpE2e, passedTests: grpE2e - Math.round(failed * 0.3), failedTests: Math.round(failed * 0.3), skippedTests: 0, durationMs: Math.round(grpE2e * 500) },
      ],
    })
  }
  return runs
})()

const filteredData = computed((): TestDailySummary[] => {
  const cutoff = daysAgo(range.value)
  return allMockData.filter(d => d.date >= cutoff)
})

// ── Variants grid ─────────────────────────────────────────────────────────────

const variants = [
  { title: 'Failure Rate (count)', colorMode: 'failure-rate', yAxis: 'count' },
  { title: 'Failure Rate (duration)', colorMode: 'failure-rate', yAxis: 'duration' },
  { title: 'Pass / Fail (count)', colorMode: 'pass-fail', yAxis: 'count' },
  { title: 'Pass / Fail (duration — split by ratio)', colorMode: 'pass-fail', yAxis: 'duration' },
  { title: 'Groups (count)', colorMode: 'groups', yAxis: 'count' },
  { title: 'Groups (duration)', colorMode: 'groups', yAxis: 'duration' },
  { title: 'Runs mode — Pass / Fail', colorMode: 'pass-fail', yAxis: 'count', xMode: 'runs' },
  { title: 'Runs mode — Groups', colorMode: 'groups', yAxis: 'count', xMode: 'runs' },
]
</script>
