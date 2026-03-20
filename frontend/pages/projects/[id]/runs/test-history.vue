<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-2 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${projectId}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Runs', to: `/projects/${projectId}/runs`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
        { label: 'Test History', to: `/projects/${projectId}/runs/test-history`, icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2' },
      ]" />
    </div>

    <!-- Toolbar -->
    <div class="flex flex-wrap items-center gap-3 mb-6">
      <MultiSelect
        v-model="branchFilters"
        :options="branchOptions"
        placeholder="All Branches"
      />

      <div class="flex items-center gap-2 bg-gray-900 border border-gray-800 rounded-lg px-3 py-1.5 flex-1 min-w-48">
        <svg class="w-3.5 h-3.5 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Search tests…"
          class="bg-transparent text-xs text-gray-300 placeholder-gray-600 focus:outline-none flex-1"
          @input="onSearchInput" />
        <button v-if="searchQuery" class="text-gray-600 hover:text-gray-400 transition-colors" @click="searchQuery = ''">✕</button>
      </div>

      <div class="ml-auto flex items-center gap-2">
        <button
          class="flex items-center gap-1.5 text-xs text-gray-400 hover:text-gray-200 bg-gray-900 border border-gray-800 px-3 py-1.5 rounded-lg transition-colors"
          @click="showImportModal = true">
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
          </svg>
          Import TRX
        </button>
      </div>
    </div>

    <!-- Tabs -->
    <div class="flex gap-1 mb-6 border-b border-gray-800">
      <button v-for="tab in tabs" :key="tab"
        :class="[
          'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
          activeTab === tab
            ? 'text-white border-brand-500'
            : 'text-gray-500 border-transparent hover:text-gray-300',
        ]"
        @click="activeTab = tab">
        {{ tab }}
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else>
      <!-- Overview Tab -->
      <template v-if="activeTab === 'Overview'">
        <!-- Summary cards -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <p class="text-xs text-gray-500 mb-1">Total Tests</p>
            <p class="text-2xl font-semibold text-white">{{ latestRunSummary?.totalTests ?? '—' }}</p>
          </div>
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <p class="text-xs text-gray-500 mb-1">Passing</p>
            <p class="text-2xl font-semibold text-green-400">{{ latestRunSummary?.passedTests ?? '—' }}</p>
          </div>
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <p class="text-xs text-gray-500 mb-1">Failing</p>
            <p class="text-2xl font-semibold text-red-400">{{ latestRunSummary?.failedTests ?? '—' }}</p>
          </div>
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <p class="text-xs text-gray-500 mb-1">Flaky Tests</p>
            <p class="text-2xl font-semibold text-yellow-400">{{ flakyTests.length }}</p>
          </div>
        </div>

        <!-- Chart: test counts over runs -->
        <div v-if="runSummaries.length" class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
          <h3 class="text-sm font-medium text-gray-300 mb-4">Test History (last {{ runSummaries.length }} runs)</h3>
          <div class="relative h-40 flex items-end gap-px overflow-x-auto">
            <div
              v-for="run in [...runSummaries].reverse()"
              :key="run.runId"
              class="flex-1 min-w-[12px] max-w-[32px] flex flex-col-reverse gap-px cursor-pointer group"
              :title="`${formatCommit(run.commitSha)} · ${run.passedTests} passed, ${run.failedTests} failed`"
              @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}?tab=tests`)">
              <div
                class="w-full rounded-sm transition-opacity group-hover:opacity-80"
                :style="{ height: barHeight(run.passedTests, run.totalTests) + 'px', background: '#22c55e' }" />
              <div
                v-if="run.failedTests"
                class="w-full rounded-sm transition-opacity group-hover:opacity-80"
                :style="{ height: barHeight(run.failedTests, run.totalTests) + 'px', background: '#ef4444' }" />
              <div
                v-if="run.skippedTests"
                class="w-full rounded-sm transition-opacity group-hover:opacity-80"
                :style="{ height: barHeight(run.skippedTests, run.totalTests) + 'px', background: '#eab308' }" />
            </div>
          </div>
          <div class="flex justify-between mt-1 text-xs text-gray-600">
            <span>oldest</span>
            <span class="flex items-center gap-4">
              <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-green-500" />passed</span>
              <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-red-500" />failed</span>
              <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-yellow-500" />skipped</span>
            </span>
            <span>latest</span>
          </div>
        </div>

        <!-- Run list -->
        <div v-if="runSummaries.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Run</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Passed</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Failed</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Skipped</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Total</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Duration</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="run in runSummaries" :key="run.runId"
                class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}?tab=tests`)">
                <td class="px-4 py-3 text-gray-300 font-mono text-xs">
                  {{ formatCommit(run.commitSha) }}
                </td>
                <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ run.branch || '—' }}</td>
                <td class="px-4 py-3 text-right text-green-400 font-medium">{{ run.passedTests }}</td>
                <td class="px-4 py-3 text-right" :class="run.failedTests ? 'text-red-400 font-medium' : 'text-gray-600'">{{ run.failedTests }}</td>
                <td class="px-4 py-3 text-right text-yellow-500/70">{{ run.skippedTests || '—' }}</td>
                <td class="px-4 py-3 text-right text-gray-400">{{ run.totalTests }}</td>
                <td class="px-4 py-3 text-right text-gray-400 text-xs">{{ formatDuration(run.durationMs) }}</td>
                <td class="px-4 py-3 text-gray-500 text-xs"><DateDisplay :date="run.startedAt" mode="auto" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">No test history yet</p>
          <p class="text-gray-600 text-sm mt-1">Test results are collected automatically from <code>.trx</code> artifact files after CI/CD runs complete.</p>
        </div>
      </template>

      <!-- Tests Tab -->
      <template v-else-if="activeTab === 'Tests'">
        <div v-if="filteredTests.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Test</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Runs</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Passed</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Failed</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Fail %</th>
                <th class="text-right px-4 py-3 text-gray-400 font-medium">Avg Duration</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Last Result</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Last Run</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="test in filteredTests" :key="test.fullName"
                class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                @click="openTestDetail(test)">
                <td class="px-4 py-3 max-w-sm">
                  <div class="flex items-center gap-2">
                    <span v-if="test.lastOutcomeName === 'Passed'" class="text-green-400 shrink-0 text-xs">✓</span>
                    <span v-else-if="test.lastOutcomeName === 'Failed'" class="text-red-400 shrink-0 text-xs">✗</span>
                    <span v-else class="text-yellow-500 shrink-0 text-xs">–</span>
                    <div class="min-w-0">
                      <p class="text-xs text-gray-300 font-mono truncate" :title="test.fullName">{{ test.methodName || test.fullName }}</p>
                      <p v-if="test.className" class="text-xs text-gray-600 font-mono truncate">{{ test.className }}</p>
                    </div>
                    <span v-if="isFlaky(test)" class="shrink-0 text-xs bg-yellow-900/50 text-yellow-400 border border-yellow-700/50 px-1.5 py-0.5 rounded">flaky</span>
                  </div>
                </td>
                <td class="px-4 py-3 text-right text-gray-400">{{ test.totalRuns }}</td>
                <td class="px-4 py-3 text-right text-green-400">{{ test.passedRuns }}</td>
                <td class="px-4 py-3 text-right" :class="test.failedRuns ? 'text-red-400 font-medium' : 'text-gray-600'">{{ test.failedRuns }}</td>
                <td class="px-4 py-3 text-right">
                  <span v-if="test.totalRuns" :class="failRateClass(test.failedRuns / test.totalRuns)">
                    {{ Math.round((test.failedRuns / test.totalRuns) * 100) }}%
                  </span>
                  <span v-else class="text-gray-600">—</span>
                </td>
                <td class="px-4 py-3 text-right text-gray-400 text-xs">{{ formatDuration(test.avgDurationMs) }}</td>
                <td class="px-4 py-3">
                  <span :class="outcomeClass(test.lastOutcomeName)" class="text-xs">{{ test.lastOutcomeName }}</span>
                </td>
                <td class="px-4 py-3 text-gray-500 text-xs"><DateDisplay :date="test.lastRunAt" mode="auto" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">No tests found</p>
          <p v-if="searchQuery" class="text-gray-600 text-sm mt-1">No tests match "{{ searchQuery }}"</p>
          <p v-else class="text-gray-600 text-sm mt-1">Tests appear once CI/CD runs produce .trx artifacts</p>
        </div>
      </template>

      <!-- Flaky Tab -->
      <template v-else-if="activeTab === 'Flaky'">
        <div v-if="flakyTests.length">
          <p class="text-xs text-gray-500 mb-4">Tests that have both passed and failed across recorded runs. They may pass on retry but indicate unstable behaviour.</p>
          <div class="rounded-xl border border-gray-800 overflow-hidden">
            <table class="w-full text-sm">
              <thead class="bg-gray-900">
                <tr>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">Test</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Runs</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Failed</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Fail Rate</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Avg Duration</th>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">Last Run</th>
                  <th class="px-4 py-3" />
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-800">
                <tr v-for="test in flakyTests" :key="test.fullName"
                  class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                  @click="openTestDetail(test)">
                  <td class="px-4 py-3 max-w-xs">
                    <p class="text-xs text-gray-300 font-mono truncate" :title="test.fullName">{{ test.methodName || test.fullName }}</p>
                    <p v-if="test.className" class="text-xs text-gray-600 font-mono truncate">{{ test.className }}</p>
                  </td>
                  <td class="px-4 py-3 text-right text-gray-400">{{ test.totalRuns }}</td>
                  <td class="px-4 py-3 text-right text-red-400 font-medium">{{ test.failedRuns }}</td>
                  <td class="px-4 py-3 text-right">
                    <span :class="failRateClass(test.failedRuns / test.totalRuns)" class="font-medium">
                      {{ Math.round((test.failedRuns / test.totalRuns) * 100) }}%
                    </span>
                  </td>
                  <td class="px-4 py-3 text-right text-gray-400 text-xs">{{ formatDuration(test.avgDurationMs) }}</td>
                  <td class="px-4 py-3 text-gray-500 text-xs"><DateDisplay :date="test.lastRunAt" mode="auto" /></td>
                  <td class="px-4 py-3 text-right">
                    <button
                      class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
                      @click.stop="createIssueForTest(test)">
                      Create Issue
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <div class="w-12 h-12 bg-green-900/30 rounded-full flex items-center justify-center mb-3">
            <svg class="w-6 h-6 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <p class="text-gray-400 font-medium">No flaky tests detected</p>
          <p class="text-gray-600 text-sm mt-1">All tests consistently pass or consistently fail across recorded runs.</p>
        </div>
      </template>

      <!-- Coverage Tab -->
      <template v-else-if="activeTab === 'Coverage'">
        <!-- Summary cards for latest coverage -->
        <template v-if="coverageRuns.length">
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p class="text-xs text-gray-500 mb-1">Line Coverage</p>
              <p class="text-2xl font-semibold" :class="coverageRateClass(coverageRuns[0].lineRate)">
                {{ formatCoverageRate(coverageRuns[0].lineRate) }}
              </p>
              <p class="text-xs text-gray-600 mt-0.5">{{ coverageRuns[0].linesCovered }} / {{ coverageRuns[0].linesValid }} lines</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p class="text-xs text-gray-500 mb-1">Branch Coverage</p>
              <p class="text-2xl font-semibold" :class="coverageRateClass(coverageRuns[0].branchRate)">
                {{ formatCoverageRate(coverageRuns[0].branchRate) }}
              </p>
              <p class="text-xs text-gray-600 mt-0.5">{{ coverageRuns[0].branchesCovered }} / {{ coverageRuns[0].branchesValid }} branches</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p class="text-xs text-gray-500 mb-1">Latest Commit</p>
              <p class="text-lg font-semibold text-gray-300 font-mono">{{ formatCommit(coverageRuns[0].commitSha) }}</p>
              <p class="text-xs text-gray-600 mt-0.5">{{ coverageRuns[0].branch || 'no branch' }}</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p class="text-xs text-gray-500 mb-1">Reports</p>
              <p class="text-2xl font-semibold text-white">{{ coverageRuns[0].reportCount }}</p>
              <p class="text-xs text-gray-600 mt-0.5">artifact(s)</p>
            </div>
          </div>

          <!-- Coverage trend chart -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
            <h3 class="text-sm font-medium text-gray-300 mb-4">Coverage Trend (last {{ coverageRuns.length }} runs)</h3>
            <div class="relative h-32 flex items-end gap-1 overflow-x-auto">
              <div
                v-for="run in [...coverageRuns].reverse()"
                :key="run.runId"
                class="flex-1 min-w-[12px] max-w-[32px] flex flex-col-reverse gap-0.5 cursor-pointer group"
                :title="`${formatCommit(run.commitSha)} · Line: ${formatCoverageRate(run.lineRate)}, Branch: ${formatCoverageRate(run.branchRate)}`"
                @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}`)">
                <!-- Line coverage bar -->
                <div
                  class="w-full rounded-sm transition-opacity group-hover:opacity-80"
                  :style="{ height: Math.max(2, Math.round(run.lineRate * 128)) + 'px', background: coverageBgColor(run.lineRate) }" />
              </div>
            </div>
            <div class="flex justify-between mt-2 text-xs text-gray-600">
              <span>oldest</span>
              <span class="flex items-center gap-4">
                <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-green-500" />≥80%</span>
                <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-yellow-500" />60–79%</span>
                <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-red-500" />&lt;60%</span>
              </span>
              <span>latest</span>
            </div>
          </div>

          <!-- Coverage run history table -->
          <div class="rounded-xl border border-gray-800 overflow-hidden">
            <table class="w-full text-sm">
              <thead class="bg-gray-900">
                <tr>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">Run</th>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">Branch</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Line Coverage</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Branch Coverage</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Lines</th>
                  <th class="text-right px-4 py-3 text-gray-400 font-medium">Branches</th>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-800">
                <tr v-for="run in coverageRuns" :key="run.runId"
                  class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                  @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}`)">
                  <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ formatCommit(run.commitSha) }}</td>
                  <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ run.branch || '—' }}</td>
                  <td class="px-4 py-3 text-right font-medium" :class="coverageRateClass(run.lineRate)">
                    {{ formatCoverageRate(run.lineRate) }}
                  </td>
                  <td class="px-4 py-3 text-right font-medium" :class="coverageRateClass(run.branchRate)">
                    {{ formatCoverageRate(run.branchRate) }}
                  </td>
                  <td class="px-4 py-3 text-right text-gray-400 text-xs">
                    {{ run.linesValid > 0 ? `${run.linesCovered} / ${run.linesValid}` : '—' }}
                  </td>
                  <td class="px-4 py-3 text-right text-gray-400 text-xs">
                    {{ run.branchesValid > 0 ? `${run.branchesCovered} / ${run.branchesValid}` : '—' }}
                  </td>
                  <td class="px-4 py-3 text-gray-500 text-xs"><DateDisplay :date="run.startedAt" mode="auto" /></td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>

        <!-- Empty state -->
        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <svg class="w-12 h-12 text-gray-700 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <p class="text-gray-400 font-medium">No coverage data yet</p>
          <p class="text-gray-600 text-sm mt-1">Coverage reports are collected automatically from Cobertura XML artifacts (<code>coverage.cobertura.xml</code>, <code>coverage.xml</code>) after CI/CD runs complete.</p>
        </div>
      </template>

      <!-- Compare Tab -->
      <template v-else-if="activeTab === 'Compare'">
        <!-- Run pickers -->
        <div class="grid grid-cols-2 gap-4 mb-6">
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <p class="text-xs text-gray-500 mb-2">Run A <span class="text-gray-600">(baseline)</span></p>
            <select
              v-model="compareRunAId"
              class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 focus:outline-none focus:border-brand-500 font-mono">
              <option value="">— select a run —</option>
              <option v-for="run in runSummaries" :key="run.runId" :value="run.runId">
                {{ formatCommit(run.commitSha) }} · {{ run.branch || 'no branch' }} · {{ formatDate(run.startedAt) }} ({{ run.totalTests }} tests)
              </option>
            </select>
          </div>
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <p class="text-xs text-gray-500 mb-2">Run B <span class="text-gray-600">(comparison)</span></p>
            <select
              v-model="compareRunBId"
              class="w-full bg-gray-800 border border-gray-700 rounded-md text-xs text-gray-300 px-2.5 py-1.5 focus:outline-none focus:border-brand-500 font-mono">
              <option value="">— select a run —</option>
              <option v-for="run in runSummaries" :key="run.runId" :value="run.runId">
                {{ formatCommit(run.commitSha) }} · {{ run.branch || 'no branch' }} · {{ formatDate(run.startedAt) }} ({{ run.totalTests }} tests)
              </option>
            </select>
          </div>
        </div>
        <div class="flex justify-center mb-6">
          <button
            :disabled="!compareRunAId || !compareRunBId || compareRunAId === compareRunBId || compareLoading"
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white text-sm px-5 py-2 rounded-lg transition-colors"
            @click="runCompare">
            <svg v-if="compareLoading" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
            </svg>
            <svg v-else class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
            {{ compareLoading ? 'Comparing…' : 'Compare Runs' }}
          </button>
        </div>

        <!-- Compare results -->
        <template v-if="compareResult">
          <!-- Summary cards -->
          <div class="flex items-center gap-3 mb-5 text-xs text-gray-500">
            <span class="font-mono text-gray-300 bg-gray-800 px-2 py-1 rounded">A: {{ formatCommit(compareResult.runA.commitSha) }}</span>
            <svg class="w-4 h-4 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3" />
            </svg>
            <span class="font-mono text-gray-300 bg-gray-800 px-2 py-1 rounded">B: {{ formatCommit(compareResult.runB.commitSha) }}</span>
            <span class="text-gray-600">{{ compareResult.runA.testCount }} → {{ compareResult.runB.testCount }} tests</span>
          </div>
          <div class="grid grid-cols-2 md:grid-cols-5 gap-3 mb-6">
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-3 text-center">
              <p class="text-xs text-gray-500 mb-1">New Tests</p>
              <p class="text-xl font-semibold text-blue-400">+{{ compareResult.summary.addedCount }}</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-3 text-center">
              <p class="text-xs text-gray-500 mb-1">Removed</p>
              <p class="text-xl font-semibold text-gray-400">-{{ compareResult.summary.removedCount }}</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-3 text-center">
              <p class="text-xs text-gray-500 mb-1">Fixed</p>
              <p class="text-xl font-semibold text-green-400">{{ compareResult.summary.fixedCount }}</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-3 text-center">
              <p class="text-xs text-gray-500 mb-1">Regressed</p>
              <p class="text-xl font-semibold text-red-400">{{ compareResult.summary.regressedCount }}</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-3 text-center">
              <p class="text-xs text-gray-500 mb-1">Slower</p>
              <p class="text-xl font-semibold text-yellow-400">{{ compareResult.summary.slowedDownCount }}</p>
            </div>
          </div>

          <!-- Regressed section -->
          <template v-if="compareResult.regressed.length">
            <h4 class="text-sm font-medium text-red-400 mb-2 flex items-center gap-2">
              <span class="text-red-400">✗</span> Regressed (passed in A → failed in B)
            </h4>
            <div class="rounded-xl border border-red-900/50 overflow-hidden mb-5">
              <table class="w-full text-xs">
                <thead class="bg-red-950/40">
                  <tr>
                    <th class="text-left px-4 py-2 text-red-400/70 font-medium">Test</th>
                    <th class="text-right px-4 py-2 text-red-400/70 font-medium">Duration A</th>
                    <th class="text-right px-4 py-2 text-red-400/70 font-medium">Duration B</th>
                    <th class="text-left px-4 py-2 text-red-400/70 font-medium">Error</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-red-900/30">
                  <tr v-for="t in compareResult.regressed" :key="t.fullName" class="hover:bg-red-950/20">
                    <td class="px-4 py-2 text-gray-300 font-mono truncate max-w-xs" :title="t.fullName">{{ t.fullName.split('.').pop() }}</td>
                    <td class="px-4 py-2 text-right text-gray-400">{{ formatDuration(t.durationMsA) }}</td>
                    <td class="px-4 py-2 text-right text-red-400">{{ formatDuration(t.durationMsB) }}</td>
                    <td class="px-4 py-2 text-red-400/80 truncate max-w-xs font-mono text-xs" :title="t.errorMessage ?? ''">{{ t.errorMessage || '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <!-- Fixed section -->
          <template v-if="compareResult.fixed_.length">
            <h4 class="text-sm font-medium text-green-400 mb-2 flex items-center gap-2">
              <span>✓</span> Fixed (failed in A → passed in B)
            </h4>
            <div class="rounded-xl border border-green-900/50 overflow-hidden mb-5">
              <table class="w-full text-xs">
                <thead class="bg-green-950/40">
                  <tr>
                    <th class="text-left px-4 py-2 text-green-400/70 font-medium">Test</th>
                    <th class="text-right px-4 py-2 text-green-400/70 font-medium">Duration A</th>
                    <th class="text-right px-4 py-2 text-green-400/70 font-medium">Duration B</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-green-900/30">
                  <tr v-for="t in compareResult.fixed_" :key="t.fullName" class="hover:bg-green-950/20">
                    <td class="px-4 py-2 text-gray-300 font-mono truncate max-w-xs" :title="t.fullName">{{ t.fullName.split('.').pop() }}</td>
                    <td class="px-4 py-2 text-right text-gray-400">{{ formatDuration(t.durationMsA) }}</td>
                    <td class="px-4 py-2 text-right text-green-400">{{ formatDuration(t.durationMsB) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <!-- Added section -->
          <template v-if="compareResult.added.length">
            <h4 class="text-sm font-medium text-blue-400 mb-2 flex items-center gap-2">
              <span>+</span> New Tests (only in B)
            </h4>
            <div class="rounded-xl border border-blue-900/50 overflow-hidden mb-5">
              <table class="w-full text-xs">
                <thead class="bg-blue-950/40">
                  <tr>
                    <th class="text-left px-4 py-2 text-blue-400/70 font-medium">Test</th>
                    <th class="text-left px-4 py-2 text-blue-400/70 font-medium">Outcome</th>
                    <th class="text-right px-4 py-2 text-blue-400/70 font-medium">Duration</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-blue-900/30">
                  <tr v-for="t in compareResult.added" :key="t.fullName" class="hover:bg-blue-950/20">
                    <td class="px-4 py-2 text-gray-300 font-mono truncate max-w-xs" :title="t.fullName">{{ t.fullName.split('.').pop() }}</td>
                    <td class="px-4 py-2" :class="outcomeClass(t.outcomeName)">{{ t.outcomeName }}</td>
                    <td class="px-4 py-2 text-right text-gray-400">{{ formatDuration(t.durationMs) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <!-- Removed section -->
          <template v-if="compareResult.removed.length">
            <h4 class="text-sm font-medium text-gray-400 mb-2 flex items-center gap-2">
              <span>−</span> Removed Tests (only in A)
            </h4>
            <div class="rounded-xl border border-gray-800 overflow-hidden mb-5">
              <table class="w-full text-xs">
                <thead class="bg-gray-900">
                  <tr>
                    <th class="text-left px-4 py-2 text-gray-500 font-medium">Test</th>
                    <th class="text-left px-4 py-2 text-gray-500 font-medium">Last Outcome</th>
                    <th class="text-right px-4 py-2 text-gray-500 font-medium">Duration</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-800">
                  <tr v-for="t in compareResult.removed" :key="t.fullName" class="hover:bg-gray-900/50">
                    <td class="px-4 py-2 text-gray-400 font-mono truncate max-w-xs line-through" :title="t.fullName">{{ t.fullName.split('.').pop() }}</td>
                    <td class="px-4 py-2 text-gray-500">{{ t.outcomeName }}</td>
                    <td class="px-4 py-2 text-right text-gray-500">{{ formatDuration(t.durationMs) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <!-- Slowed down section -->
          <template v-if="compareResult.slowedDown.length">
            <h4 class="text-sm font-medium text-yellow-400 mb-2 flex items-center gap-2">
              <span>⏱</span> Significantly Slower (≥1.5× slower than baseline)
            </h4>
            <div class="rounded-xl border border-yellow-900/50 overflow-hidden mb-5">
              <table class="w-full text-xs">
                <thead class="bg-yellow-950/30">
                  <tr>
                    <th class="text-left px-4 py-2 text-yellow-400/70 font-medium">Test</th>
                    <th class="text-right px-4 py-2 text-yellow-400/70 font-medium">Duration A</th>
                    <th class="text-right px-4 py-2 text-yellow-400/70 font-medium">Duration B</th>
                    <th class="text-right px-4 py-2 text-yellow-400/70 font-medium">Δ</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-yellow-900/30">
                  <tr v-for="t in compareResult.slowedDown" :key="t.fullName" class="hover:bg-yellow-950/20">
                    <td class="px-4 py-2 text-gray-300 font-mono truncate max-w-xs" :title="t.fullName">{{ t.fullName.split('.').pop() }}</td>
                    <td class="px-4 py-2 text-right text-gray-400">{{ formatDuration(t.durationMsA) }}</td>
                    <td class="px-4 py-2 text-right text-yellow-400">{{ formatDuration(t.durationMsB) }}</td>
                    <td class="px-4 py-2 text-right text-yellow-300 font-medium">+{{ formatDuration(t.deltaMs) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <div
            v-if="!compareResult.regressed.length && !compareResult.fixed_.length && !compareResult.added.length && !compareResult.removed.length && !compareResult.slowedDown.length"
            class="flex flex-col items-center justify-center py-16 text-center">
            <div class="w-12 h-12 bg-green-900/30 rounded-full flex items-center justify-center mb-3">
              <svg class="w-6 h-6 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <p class="text-gray-400 font-medium">No differences found</p>
            <p class="text-gray-600 text-sm mt-1">Both runs have identical test names and outcomes.</p>
          </div>
        </template>
        <div v-else-if="!compareLoading" class="flex flex-col items-center justify-center py-16 text-center text-gray-500">
          <svg class="w-10 h-10 text-gray-700 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
          </svg>
          <p class="text-sm">Select two runs above and click <strong class="text-gray-400">Compare Runs</strong></p>
        </div>
      </template>

      <!-- Analytics Tab -->
      <template v-else-if="activeTab === 'Analytics'">
        <template v-if="runSummaries.length || allTests.length">

          <!-- Duration Analytics ──────────────────────────────────────── -->
          <div class="mb-8">
            <h3 class="text-sm font-semibold text-gray-200 mb-1">Duration Analytics</h3>
            <p class="text-xs text-gray-500 mb-4">Slowest tests by average duration — potential CI bottlenecks. Tests that have become significantly faster than their historic baseline may indicate broken or bypassed test conditions.</p>

            <!-- Slowest tests table -->
            <div v-if="slowestTests.length" class="rounded-xl border border-gray-800 overflow-hidden mb-6">
              <table class="w-full text-sm">
                <thead class="bg-gray-900">
                  <tr>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium">#</th>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium">Test</th>
                    <th class="text-right px-4 py-3 text-gray-400 font-medium">Avg Duration</th>
                    <th class="text-right px-4 py-3 text-gray-400 font-medium">Runs</th>
                    <th class="text-right px-4 py-3 text-gray-400 font-medium">Fail Rate</th>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium">Last Result</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-800">
                  <tr v-for="(test, idx) in slowestTests" :key="test.fullName"
                    class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                    @click="openTestDetail(test)">
                    <td class="px-4 py-3 text-gray-600 text-xs">{{ idx + 1 }}</td>
                    <td class="px-4 py-3 max-w-xs">
                      <p class="text-xs text-gray-300 font-mono truncate" :title="test.fullName">{{ test.methodName || test.fullName }}</p>
                      <p v-if="test.className" class="text-xs text-gray-600 font-mono truncate">{{ test.className }}</p>
                    </td>
                    <td class="px-4 py-3 text-right">
                      <span class="text-yellow-400 font-medium text-xs">{{ formatDuration(test.avgDurationMs) }}</span>
                    </td>
                    <td class="px-4 py-3 text-right text-gray-400 text-xs">{{ test.totalRuns }}</td>
                    <td class="px-4 py-3 text-right text-xs">
                      <span v-if="test.totalRuns" :class="failRateClass(test.failedRuns / test.totalRuns)">
                        {{ Math.round((test.failedRuns / test.totalRuns) * 100) }}%
                      </span>
                    </td>
                    <td class="px-4 py-3 text-xs">
                      <span :class="outcomeClass(test.lastOutcomeName)">{{ test.lastOutcomeName }}</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Suite duration trend chart -->
            <div v-if="runSummaries.length > 1" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
              <h4 class="text-xs font-medium text-gray-400 mb-3">Total Suite Duration per Run (oldest → latest)</h4>
              <div class="relative flex items-end gap-px overflow-x-auto" :style="{ height: SUITE_CHART_HEIGHT + 16 + 'px' }">
                <div
                  v-for="run in [...runSummaries].reverse()"
                  :key="run.runId"
                  class="flex-1 min-w-[10px] max-w-[28px] flex flex-col-reverse cursor-pointer group"
                  :title="`${formatCommit(run.commitSha)} · ${formatDuration(run.durationMs)}`"
                  @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}?tab=tests`)">
                  <div
                    class="w-full rounded-sm bg-brand-500/70 transition-opacity group-hover:opacity-90"
                    :style="{ height: suiteDurationBarHeight(run.durationMs) + 'px' }" />
                </div>
              </div>
              <div class="flex justify-between mt-1 text-xs text-gray-600">
                <span>oldest</span>
                <span>latest</span>
              </div>
            </div>
          </div>

          <!-- Test Delta & Volatility ──────────────────────────────────── -->
          <div class="mb-8">
            <h3 class="text-sm font-semibold text-gray-200 mb-1">Test Delta & Volatility</h3>
            <p class="text-xs text-gray-500 mb-4">Track total test count and skipped tests across runs. A sudden drop in total tests may indicate tests were silently removed. Use the <button class="text-brand-400 hover:text-brand-300 underline underline-offset-2" @click="activeTab = 'Compare'">Compare tab</button> to see exact added/removed test names between any two runs.</p>

            <!-- Test count drops warning banner -->
            <div
              v-if="testCountTrend.some(r => r.hasDropWarning)"
              class="flex items-start gap-3 bg-amber-900/20 border border-amber-700/40 rounded-xl px-4 py-3 mb-4 text-xs text-amber-300">
              <svg class="w-4 h-4 shrink-0 mt-0.5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              </svg>
              <span>
                <strong class="text-amber-200">Test count dropped</strong> between some consecutive runs.
                This may indicate tests were silently removed. Use the Compare tab to identify which test names are missing.
              </span>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">
              <!-- Test count trend chart -->
              <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                <h4 class="text-xs font-medium text-gray-400 mb-3">Total Test Count per Run</h4>
                <div class="flex items-end gap-px h-24 overflow-x-auto">
                  <div
                    v-for="run in [...testCountTrend].reverse()"
                    :key="run.runId"
                    class="flex-1 min-w-[10px] max-w-[28px] flex flex-col-reverse cursor-pointer group"
                    :title="`${formatCommit(run.commitSha)} · ${run.totalTests} tests${run.delta !== null ? (run.delta >= 0 ? ` (+${run.delta})` : ` (${run.delta})`) : ''}`"
                    @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}?tab=tests`)">
                    <div
                      class="w-full rounded-sm transition-opacity group-hover:opacity-90"
                      :class="run.hasDropWarning ? 'bg-amber-500' : 'bg-blue-500/70'"
                      :style="{ height: Math.max(2, Math.round((run.totalTests / Math.max(...testCountTrend.map(r => r.totalTests), 1)) * 96)) + 'px' }" />
                  </div>
                </div>
                <div class="flex justify-between mt-1 text-xs text-gray-600">
                  <span>oldest</span>
                  <span class="flex items-center gap-3">
                    <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-blue-500/70" />normal</span>
                    <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-amber-500" />drop (&gt;5)</span>
                  </span>
                  <span>latest</span>
                </div>
              </div>

              <!-- Skipped tests trend chart -->
              <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                <h4 class="text-xs font-medium text-gray-400 mb-3">Skipped Tests per Run</h4>
                <div class="flex items-end gap-px h-24 overflow-x-auto">
                  <div
                    v-for="run in [...runSummaries].reverse()"
                    :key="run.runId"
                    class="flex-1 min-w-[10px] max-w-[28px] flex flex-col-reverse cursor-pointer group"
                    :title="`${formatCommit(run.commitSha)} · ${run.skippedTests} skipped`"
                    @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}?tab=tests`)">
                    <div
                      class="w-full rounded-sm transition-opacity group-hover:opacity-90"
                      :class="run.skippedTests > 0 ? 'bg-yellow-500/70' : 'bg-gray-700'"
                      :style="{ height: Math.max(2, skipBarHeight(run.skippedTests)) + 'px' }" />
                  </div>
                </div>
                <div class="flex justify-between mt-1 text-xs text-gray-600">
                  <span>oldest</span>
                  <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-yellow-500/70" />skipped</span>
                  <span>latest</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Reliability & Pass Rate ──────────────────────────────────── -->
          <div class="mb-8">
            <h3 class="text-sm font-semibold text-gray-200 mb-1">Reliability & Pass Rate</h3>
            <p class="text-xs text-gray-500 mb-4">Pass rates broken down by branch and day of week. Low pass rates on specific branches or days can reveal scheduling issues or branch-specific fragility.</p>

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">
              <!-- Pass rate by branch -->
              <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
                <div class="px-4 py-3 border-b border-gray-800">
                  <h4 class="text-xs font-medium text-gray-400">Pass Rate by Branch</h4>
                </div>
                <table class="w-full text-xs">
                  <thead>
                    <tr class="border-b border-gray-800">
                      <th class="text-left px-4 py-2 text-gray-500 font-medium">Branch</th>
                      <th class="text-right px-4 py-2 text-gray-500 font-medium">Runs</th>
                      <th class="text-right px-4 py-2 text-gray-500 font-medium">Pass Rate</th>
                      <th class="text-right px-4 py-2 text-gray-500 font-medium">Failed</th>
                      <th class="text-right px-4 py-2 text-gray-500 font-medium">Skipped</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-800">
                    <tr v-for="row in passRateByBranch" :key="row.branch" class="hover:bg-gray-800/40">
                      <td class="px-4 py-2.5 text-gray-300 font-mono truncate max-w-[140px]" :title="row.branch">{{ row.branch }}</td>
                      <td class="px-4 py-2.5 text-right text-gray-500">{{ row.runs }}</td>
                      <td class="px-4 py-2.5 text-right font-medium" :class="failRateClass(1 - row.passRate)">
                        {{ Math.round(row.passRate * 100) }}%
                      </td>
                      <td class="px-4 py-2.5 text-right" :class="row.failed ? 'text-red-400' : 'text-gray-600'">{{ row.failed }}</td>
                      <td class="px-4 py-2.5 text-right text-yellow-500/70">{{ row.skipped || '—' }}</td>
                    </tr>
                  </tbody>
                </table>
                <div v-if="!passRateByBranch.length" class="py-6 text-center text-xs text-gray-600">No branch data</div>
              </div>

              <!-- Pass rate heatmap by day of week -->
              <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                <h4 class="text-xs font-medium text-gray-400 mb-3">Pass Rate Heatmap — Day of Week</h4>
                <div class="grid grid-cols-7 gap-1.5">
                  <div
                    v-for="day in passRateByDayOfWeek"
                    :key="day.dayIndex"
                    :title="day.passRate !== null ? `${day.dayName}: ${Math.round(day.passRate * 100)}% pass rate over ${day.runs} run(s)` : `${day.dayName}: no data`"
                    :class="['rounded-lg p-2 text-center transition-opacity', heatmapBg(day.passRate)]">
                    <p class="text-xs font-medium" :class="day.passRate !== null ? 'text-white/90' : 'text-gray-600'">{{ day.dayName }}</p>
                    <p class="text-xs mt-0.5" :class="day.passRate !== null ? 'text-white/70' : 'text-gray-700'">
                      {{ day.passRate !== null ? Math.round(day.passRate * 100) + '%' : '—' }}
                    </p>
                    <p v-if="day.runs > 0" class="text-xs mt-0.5 text-white/50">{{ day.runs }}r</p>
                  </div>
                </div>
                <div class="mt-3 flex items-center gap-3 text-xs text-gray-600 justify-center">
                  <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded bg-green-600" />≥95%</span>
                  <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded bg-green-700/70" />≥80%</span>
                  <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded bg-yellow-600/70" />≥60%</span>
                  <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded bg-orange-600/70" />≥40%</span>
                  <span class="flex items-center gap-1"><span class="inline-block w-3 h-3 rounded bg-red-600/70" />&lt;40%</span>
                </div>
              </div>
            </div>
          </div>

        </template>
        <!-- Empty state -->
        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <svg class="w-12 h-12 text-gray-700 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <p class="text-gray-400 font-medium">No analytics data yet</p>
          <p class="text-gray-600 text-sm mt-1">Analytics are computed from test runs. Import a TRX file or run CI/CD pipelines to start seeing insights.</p>
        </div>
      </template>
    </template>

    <!-- Test detail side panel -->
    <Teleport to="body">
      <div v-if="selectedTest" class="fixed inset-0 z-50 flex justify-end bg-black/50" @mousedown.self="selectedTest = null">
        <div class="bg-gray-950 border-l border-gray-800 w-full max-w-2xl flex flex-col overflow-hidden">
          <div class="flex items-center justify-between p-4 border-b border-gray-800">
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-gray-200 truncate">{{ selectedTest.methodName || selectedTest.fullName }}</p>
              <p v-if="selectedTest.className" class="text-xs text-gray-500 font-mono truncate">{{ selectedTest.className }}</p>
            </div>
            <button class="ml-3 text-gray-500 hover:text-gray-300 transition-colors" @click="selectedTest = null">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <!-- Stats bar -->
          <div class="flex items-center gap-6 px-4 py-3 border-b border-gray-800 bg-gray-900/60">
            <div>
              <p class="text-xs text-gray-500">Total Runs</p>
              <p class="text-lg font-semibold text-white">{{ selectedTest.totalRuns }}</p>
            </div>
            <div>
              <p class="text-xs text-gray-500">Passed</p>
              <p class="text-lg font-semibold text-green-400">{{ selectedTest.passedRuns }}</p>
            </div>
            <div>
              <p class="text-xs text-gray-500">Failed</p>
              <p class="text-lg font-semibold text-red-400">{{ selectedTest.failedRuns }}</p>
            </div>
            <div>
              <p class="text-xs text-gray-500">Fail Rate</p>
              <p class="text-lg font-semibold" :class="failRateClass(selectedTest.failedRuns / selectedTest.totalRuns)">
                {{ Math.round((selectedTest.failedRuns / selectedTest.totalRuns) * 100) }}%
              </p>
            </div>
            <div>
              <p class="text-xs text-gray-500">Avg Duration</p>
              <p class="text-lg font-semibold text-gray-300">{{ formatDuration(selectedTest.avgDurationMs) }}</p>
            </div>
          </div>

          <!-- History -->
          <div class="flex-1 overflow-y-auto p-4 space-y-2">
            <div v-if="testHistoryLoading" class="flex items-center justify-center py-10">
              <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
            <template v-else-if="testHistory.length">
              <!-- Mini chart: outcome + duration per run -->
              <div class="bg-gray-900 border border-gray-800 rounded-lg p-3 mb-2">
                <p class="text-xs text-gray-500 mb-2">Run history (oldest → newest)</p>
                <div class="flex items-end gap-1 h-16 overflow-x-auto">
                  <div
                    v-for="entry in [...testHistory].reverse()"
                    :key="entry.id"
                    class="flex flex-col items-center gap-0.5 min-w-[10px] flex-1 group cursor-default"
                    :title="`${formatDate(entry.runAt)} · ${formatDuration(entry.durationMs)} · ${entry.outcomeName}`">
                    <div
                      class="w-full rounded-sm transition-opacity group-hover:opacity-75"
                      :style="{ height: testBarHeight(entry.durationMs) + 'px' }"
                      :class="entry.outcomeName === 'Passed' ? 'bg-green-500' : entry.outcomeName === 'Failed' ? 'bg-red-500' : 'bg-yellow-500'" />
                  </div>
                </div>
                <div class="flex justify-between mt-1 text-xs text-gray-600">
                  <span>oldest</span>
                  <span class="flex items-center gap-3">
                    <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-green-500" />passed</span>
                    <span class="flex items-center gap-1"><span class="inline-block w-2 h-2 rounded-sm bg-red-500" />failed</span>
                  </span>
                  <span>latest</span>
                </div>
              </div>
              <div
                v-for="entry in testHistory"
                :key="entry.id"
                class="rounded-lg border px-3 py-2.5"
                :class="entry.outcomeName === 'Passed' ? 'border-green-800/60 bg-green-900/10' : entry.outcomeName === 'Failed' ? 'border-red-800/60 bg-red-900/10' : 'border-gray-800 bg-gray-900/30'">
                <div class="flex items-center gap-3">
                  <span v-if="entry.outcomeName === 'Passed'" class="text-green-400 text-sm font-bold">✓</span>
                  <span v-else-if="entry.outcomeName === 'Failed'" class="text-red-400 text-sm font-bold">✗</span>
                  <span v-else class="text-yellow-500 text-sm font-bold">–</span>
                  <span class="text-xs text-gray-500"><DateDisplay :date="entry.runAt" mode="auto" /></span>
                  <NuxtLink
                    v-if="entry.commitSha"
                    :to="commitUrl(entry.commitSha)!"
                    class="text-xs text-brand-400 hover:text-brand-300 font-mono transition-colors"
                    @click.stop>
                    {{ formatCommit(entry.commitSha) }}
                  </NuxtLink>
                  <span v-if="entry.branch" class="text-xs text-gray-600 font-mono">{{ entry.branch }}</span>
                  <span class="ml-auto text-xs text-gray-500">{{ formatDuration(entry.durationMs) }}</span>
                </div>
                <div v-if="entry.errorMessage" class="mt-2 text-xs text-red-400 font-mono whitespace-pre-wrap break-all bg-red-950/30 rounded p-2">{{ entry.errorMessage }}</div>
                <details v-if="entry.stackTrace" class="mt-1">
                  <summary class="text-xs text-gray-600 cursor-pointer hover:text-gray-400 select-none">Stack trace</summary>
                  <pre class="mt-1 text-xs text-gray-500 font-mono whitespace-pre-wrap break-all">{{ entry.stackTrace }}</pre>
                </details>
              </div>
            </template>
            <div v-else class="py-10 text-center text-sm text-gray-500">No history available</div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Import TRX modal -->
    <Teleport to="body">
      <div v-if="showImportModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="showImportModal = false">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-md">
          <h3 class="text-base font-semibold text-white mb-4">Import TRX File</h3>
          <p class="text-xs text-gray-500 mb-4">Import a Visual Studio test results file (<code>.trx</code>) directly. Optionally provide the commit SHA and branch so the results integrate with your history.</p>

          <div class="space-y-3 mb-4">
            <div>
              <label class="block text-xs text-gray-400 mb-1">TRX File</label>
              <input
                ref="fileInputRef"
                type="file"
                accept=".trx"
                class="w-full text-xs text-gray-300 bg-gray-800 border border-gray-700 rounded-md px-2 py-1.5 file:mr-3 file:py-1 file:px-2 file:rounded file:border-0 file:text-xs file:bg-brand-600 file:text-white hover:file:bg-brand-700 file:cursor-pointer"
                @change="onFileChange" />
            </div>
            <div>
              <label class="block text-xs text-gray-400 mb-1">Commit SHA <span class="text-gray-600">(optional)</span></label>
              <input
                v-model="importForm.commitSha"
                type="text"
                placeholder="e.g. abc1234"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-white px-2.5 py-1.5 focus:outline-none focus:border-brand-500 font-mono" />
            </div>
            <div>
              <label class="block text-xs text-gray-400 mb-1">Branch <span class="text-gray-600">(optional)</span></label>
              <input
                v-model="importForm.branch"
                type="text"
                placeholder="e.g. main"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-white px-2.5 py-1.5 focus:outline-none focus:border-brand-500 font-mono" />
            </div>
            <div>
              <label class="block text-xs text-gray-400 mb-1">Artifact Name <span class="text-gray-600">(optional)</span></label>
              <input
                v-model="importForm.artifactName"
                type="text"
                placeholder="e.g. e2e-test-results"
                class="w-full bg-gray-800 border border-gray-700 rounded-md text-sm text-white px-2.5 py-1.5 focus:outline-none focus:border-brand-500" />
            </div>
          </div>

          <div v-if="importError" class="mb-3 text-xs text-red-400 bg-red-900/20 border border-red-800/50 rounded px-3 py-2">{{ importError }}</div>
          <div v-if="importSuccess" class="mb-3 text-xs text-green-400 bg-green-900/20 border border-green-800/50 rounded px-3 py-2">{{ importSuccess }}</div>

          <div class="flex justify-end gap-2">
            <button class="text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 transition-colors" @click="showImportModal = false">Cancel</button>
            <button
              :disabled="!importForm.file || importing"
              class="text-sm bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white px-4 py-1.5 rounded-lg transition-colors"
              @click="importTrx">
              {{ importing ? 'Importing…' : 'Import' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import type { TestRunSummary, TestStats, TestCaseHistoryEntry, TestRunCompareResult, CoverageRunSummary } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import type { MultiSelectOption } from '~/components/MultiSelect.vue'

const route = useRoute()
const router = useRouter()
const projectId = route.params.id as string
const projectsStore = useProjectsStore()

const api = useApi()

const tabs = ['Overview', 'Tests', 'Flaky', 'Coverage', 'Compare', 'Analytics'] as const
type Tab = typeof tabs[number]

function tabFromQuery(q: unknown): Tab {
  const candidate = String(q ?? '')
  return (tabs as readonly string[]).includes(candidate) ? (candidate as Tab) : 'Overview'
}

const activeTab = ref<Tab>(tabFromQuery(route.query.tab))

watch(activeTab, (tab) => {
  router.replace({ query: { ...route.query, tab } })
})

const branchFilters = ref<string[]>(
  Array.isArray(route.query.branch)
    ? (route.query.branch as string[]).filter(Boolean)
    : route.query.branch
      ? [route.query.branch as string]
      : [],
)
const availableBranches = ref<string[]>([])

const branchOptions = computed<MultiSelectOption[]>(() =>
  availableBranches.value.map(b => ({ value: b, label: b })),
)

watch(branchFilters, (branches) => {
  router.replace({ query: { ...route.query, branch: branches.length ? branches : undefined } })
  reload()
}, { deep: true })
const searchQuery = ref('')

// Start as true so the loading spinner is present on initial render.
// This allows E2E tests (and users) to reliably wait for the spinner to
// disappear before interacting with tab content.
const loading = ref(true)
const runSummaries = ref<TestRunSummary[]>([])
const allTests = ref<TestStats[]>([])
const coverageRuns = ref<CoverageRunSummary[]>([])

const selectedTest = ref<TestStats | null>(null)
const testHistoryLoading = ref(false)
const testHistory = ref<TestCaseHistoryEntry[]>([])

const showImportModal = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)
const importing = ref(false)
const importError = ref('')
const importSuccess = ref('')
const importForm = reactive({
  file: null as File | null,
  commitSha: '',
  branch: '',
  artifactName: '',
})

const latestRunSummary = computed(() => runSummaries.value[0] ?? null)

const flakyTests = computed(() =>
  allTests.value.filter(t => t.failedRuns > 0 && t.passedRuns > 0),
)

// ── Analytics tab computed ──────────────────────────────────────────────────

const slowestTests = computed(() =>
  [...allTests.value]
    .sort((a, b) => b.avgDurationMs - a.avgDurationMs)
    .slice(0, 20),
)

const maxSuiteDurationMs = computed(() =>
  Math.max(...runSummaries.value.map(r => r.durationMs), 1),
)

const SUITE_CHART_HEIGHT = 96

function suiteDurationBarHeight(ms: number): number {
  if (!ms || !maxSuiteDurationMs.value) return 2
  return Math.max(2, Math.round((ms / maxSuiteDurationMs.value) * SUITE_CHART_HEIGHT))
}

const testCountTrend = computed(() => {
  const runs = [...runSummaries.value].reverse() // oldest → newest
  return runs.map((run, i) => {
    const prev = i > 0 ? runs[i - 1] : null
    const delta = prev !== null ? run.totalTests - prev.totalTests : null
    return {
      ...run,
      delta,
      hasDropWarning: delta !== null && delta < -5,
    }
  }).reverse() // back to newest first
})

const maxSkippedTests = computed(() =>
  Math.max(...runSummaries.value.map(r => r.skippedTests), 1),
)

const SKIP_CHART_HEIGHT = 64

function skipBarHeight(count: number): number {
  if (!count || !maxSkippedTests.value) return 2
  return Math.max(2, Math.round((count / maxSkippedTests.value) * SKIP_CHART_HEIGHT))
}

const passRateByBranch = computed(() => {
  const map = new Map<string, { total: number; passed: number; failed: number; skipped: number; runs: number }>()
  for (const run of runSummaries.value) {
    const branch = run.branch || '(no branch)'
    const entry = map.get(branch) ?? { total: 0, passed: 0, failed: 0, skipped: 0, runs: 0 }
    entry.total += run.totalTests
    entry.passed += run.passedTests
    entry.failed += run.failedTests
    entry.skipped += run.skippedTests
    entry.runs++
    map.set(branch, entry)
  }
  return [...map.entries()]
    .map(([branch, data]) => ({
      branch,
      ...data,
      passRate: data.total > 0 ? data.passed / data.total : 0,
    }))
    .sort((a, b) => b.total - a.total)
})

const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as const

const passRateByDayOfWeek = computed(() => {
  const days = Array.from({ length: 7 }, (_, i) => ({
    dayIndex: i,
    dayName: DAY_NAMES[i],
    total: 0,
    passed: 0,
    failed: 0,
    runs: 0,
  }))
  for (const run of runSummaries.value) {
    const day = new Date(run.startedAt).getDay()
    days[day].total += run.totalTests
    days[day].passed += run.passedTests
    days[day].failed += run.failedTests
    days[day].runs++
  }
  return days.map(d => ({
    ...d,
    passRate: d.total > 0 ? d.passed / d.total : null as number | null,
  }))
})

function heatmapBg(rate: number | null): string {
  if (rate === null) return 'bg-gray-800'
  if (rate >= 0.95) return 'bg-green-600'
  if (rate >= 0.8) return 'bg-green-700/70'
  if (rate >= 0.6) return 'bg-yellow-600/70'
  if (rate >= 0.4) return 'bg-orange-600/70'
  return 'bg-red-600/70'
}

// ───────────────────────────────────────────────────────────────────────────

const filteredTests = computed(() => {
  if (!searchQuery.value) return allTests.value
  const q = searchQuery.value.toLowerCase()
  return allTests.value.filter(t => t.fullName.toLowerCase().includes(q))
})

function isFlaky(test: TestStats) {
  return test.failedRuns > 0 && test.passedRuns > 0
}

function failRateClass(rate: number) {
  if (rate >= 0.5) return 'text-red-400'
  if (rate > 0) return 'text-yellow-400'
  return 'text-green-400'
}

function formatCoverageRate(rate: number): string {
  return `${Math.round(rate * 100)}%`
}

function coverageRateClass(rate: number): string {
  if (rate >= 0.8) return 'text-green-400'
  if (rate >= 0.6) return 'text-yellow-400'
  return 'text-red-400'
}

function coverageBgColor(rate: number): string {
  if (rate >= 0.8) return '#22c55e'
  if (rate >= 0.6) return '#eab308'
  return '#ef4444'
}

function outcomeClass(name: string) {
  if (name === 'Passed') return 'text-green-400'
  if (name === 'Failed') return 'text-red-400'
  return 'text-yellow-500'
}

function formatCommit(sha?: string) {
  return sha ? sha.slice(0, 7) : '—'
}

function formatDate(d: string) {
  const dt = new Date(d)
  const day = dt.getDate()
  const month = dt.toLocaleDateString('de-DE', { month: 'short' })
  const monthStr = month.charAt(0).toUpperCase() + month.slice(1).replace('.', '')
  const year = dt.getFullYear()
  const h = String(dt.getHours()).padStart(2, '0')
  const m = String(dt.getMinutes()).padStart(2, '0')
  return `${day}. ${monthStr} ${year}, ${h}:${m}`
}

function formatDuration(ms: number) {
  if (!ms) return '—'
  if (ms < 1000) return `${Math.round(ms)}ms`
  const s = ms / 1000
  if (s < 60) return `${s.toFixed(1)}s`
  const m = Math.floor(s / 60)
  return `${m}m ${Math.round(s % 60)}s`
}

/** Returns the internal code viewer URL for a specific commit SHA, if one is provided. */
function commitUrl(sha?: string): string | null {
  if (!sha) return null
  return `/projects/${projectId}/code?sha=${sha}`
}

const maxTotal = computed(() => Math.max(...runSummaries.value.map(r => r.totalTests), 1))
const CHART_HEIGHT = 160

function barHeight(count: number, total: number) {
  if (!total || !maxTotal.value) return 0
  return Math.max(2, Math.round((count / maxTotal.value) * CHART_HEIGHT))
}

const TEST_BAR_HEIGHT = 56
const maxTestDuration = computed(() => Math.max(...testHistory.value.map(e => e.durationMs ?? 0), 1))

function testBarHeight(ms: number) {
  if (!ms || !maxTestDuration.value) return 2
  return Math.max(2, Math.round((ms / maxTestDuration.value) * TEST_BAR_HEIGHT))
}

async function reload() {
  loading.value = true
  try {
    const branchParams = branchFilters.value.map(b => `branch=${encodeURIComponent(b)}`).join('&')
    const branchSep = branchParams ? `&${branchParams}` : ''
    const [runs, tests, coverage] = await Promise.all([
      api.get<TestRunSummary[]>(`/api/projects/${projectId}/test-history/runs?take=50${branchSep}`),
      api.get<TestStats[]>(`/api/projects/${projectId}/test-history/tests?take=500${branchSep}`),
      api.get<CoverageRunSummary[]>(`/api/projects/${projectId}/test-history/coverage/runs?take=50${branchSep}`).catch(() => [] as CoverageRunSummary[]),
    ])
    runSummaries.value = runs
    allTests.value = tests
    coverageRuns.value = coverage
  }
  finally {
    loading.value = false
  }
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
  // Filtering is handled client-side by the filteredTests computed property.
  // The timer is kept here as a hook for future server-side search if needed.
  searchTimer = setTimeout(() => { /* client-side filtering via filteredTests */ }, 300)
}

async function openTestDetail(test: TestStats) {
  selectedTest.value = test
  testHistory.value = []
  testHistoryLoading.value = true
  try {
    const branchParams = branchFilters.value.map(b => `branch=${encodeURIComponent(b)}`).join('&')
    const branchSep = branchParams ? `&${branchParams}` : ''
    testHistory.value = await api.get<TestCaseHistoryEntry[]>(
      `/api/projects/${projectId}/test-history/tests/${encodeURIComponent(test.fullName)}?take=50${branchSep}`,
    )
  }
  finally {
    testHistoryLoading.value = false
  }
}

async function createIssueForTest(test: TestStats) {
  const name = test.methodName || test.fullName
  const recentFailure = allTests.value.find(t => t.fullName === test.fullName)
  const failRate = recentFailure ? `${Math.round((recentFailure.failedRuns / recentFailure.totalRuns) * 100)}%` : 'unknown'
  const title = `fix: flaky test – ${name}`
  const body = `## Flaky Test Report\n\n**Test:** \`${test.fullName}\`\n**Fail Rate:** ${failRate} (${recentFailure?.failedRuns ?? '?'} / ${recentFailure?.totalRuns ?? '?'} runs)\n**Avg Duration:** ${formatDuration(test.avgDurationMs)}\n\n### Next Steps\n- [ ] Investigate root cause (race condition, timing, external dependency?)\n- [ ] Add a retry mechanism or make the test deterministic\n- [ ] Verify fix across multiple runs`
  const params = new URLSearchParams({ title, body })
  await navigateTo(`/projects/${projectId}/issues/new?${params.toString()}`)
}

function onFileChange(e: Event) {
  const input = e.target as HTMLInputElement
  importForm.file = input.files?.[0] ?? null
}

async function importTrx() {
  if (!importForm.file) return
  importing.value = true
  importError.value = ''
  importSuccess.value = ''
  try {
    const form = new FormData()
    form.append('file', importForm.file)
    if (importForm.commitSha) form.append('commitSha', importForm.commitSha)
    if (importForm.branch) form.append('branch', importForm.branch)
    if (importForm.artifactName) form.append('artifactName', importForm.artifactName)
    const result = await api.post<{ totalTests: number; passedTests: number; failedTests: number }>(`/api/projects/${projectId}/test-history/import`, form)
    importSuccess.value = `Imported ${result.totalTests} tests (${result.passedTests} passed, ${result.failedTests} failed).`
    await reload()
  }
  catch (e: unknown) {
    importError.value = (e as { data?: { title?: string } })?.data?.title ?? 'Failed to import TRX file.'
  }
  finally {
    importing.value = false
  }
}

const compareRunAId = ref('')
const compareRunBId = ref('')
const compareLoading = ref(false)
const compareResult = ref<TestRunCompareResult | null>(null)

async function runCompare() {
  if (!compareRunAId.value || !compareRunBId.value) return
  compareLoading.value = true
  compareResult.value = null
  try {
    compareResult.value = await api.get<TestRunCompareResult>(
      `/api/projects/${projectId}/test-history/compare?runA=${compareRunAId.value}&runB=${compareRunBId.value}`,
    )
  }
  finally {
    compareLoading.value = false
  }
}

async function fetchAvailableBranches() {
  try {
    availableBranches.value = await api.get<string[]>(`/api/projects/${projectId}/test-history/branches`)
  }
  catch (e) {
    // fail silently — the filter still works even without the branch list
    console.warn('Failed to fetch branch list for test history filter:', e)
  }
}

onMounted(async () => {
  projectsStore.fetchProject(projectId)
  await Promise.all([fetchAvailableBranches(), reload()])
  // If a specific test name is in the URL (e.g. navigated from cicd run tests tab), open its detail
  const testParam = route.query.test as string | undefined
  if (testParam && activeTab.value === 'Tests') {
    const match = allTests.value.find(t => t.fullName === testParam)
    if (match) openTestDetail(match)
  }
})
</script>
