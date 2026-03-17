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
      <div class="flex items-center gap-2 bg-gray-900 border border-gray-800 rounded-lg px-3 py-1.5">
        <svg class="w-3.5 h-3.5 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M3 7h18M3 12h18M3 17h18" />
        </svg>
        <span class="text-xs text-gray-500">Branch</span>
        <input
          v-model="branchFilter"
          type="text"
          placeholder="all branches"
          class="bg-transparent text-xs text-gray-300 placeholder-gray-600 focus:outline-none w-28"
          @keydown.enter="reload" />
        <button v-if="branchFilter" class="text-gray-600 hover:text-gray-400 transition-colors" @click="branchFilter = ''; reload()">✕</button>
      </div>

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
              @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}`)">
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
                @click="navigateTo(`/projects/${projectId}/runs/cicd/${run.runId}`)">
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
                    :to="`/projects/${projectId}/runs/cicd/${entry.runId}`"
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
import type { TestRunSummary, TestStats, TestCaseHistoryEntry, TestRunCompareResult } from '~/types'
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const projectId = route.params.id as string
const projectsStore = useProjectsStore()

const api = useApi()

const tabs = ['Overview', 'Tests', 'Flaky', 'Compare'] as const
const activeTab = ref<typeof tabs[number]>('Overview')

const branchFilter = ref((route.query.branch as string) || '')
const searchQuery = ref('')

const loading = ref(false)
const runSummaries = ref<TestRunSummary[]>([])
const allTests = ref<TestStats[]>([])

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

const maxTotal = computed(() => Math.max(...runSummaries.value.map(r => r.totalTests), 1))
const CHART_HEIGHT = 160

function barHeight(count: number, total: number) {
  if (!total || !maxTotal.value) return 0
  return Math.max(2, Math.round((count / maxTotal.value) * CHART_HEIGHT))
}

async function reload() {
  loading.value = true
  try {
    const branch = branchFilter.value || undefined
    const branchParam = branch ? `&branch=${encodeURIComponent(branch)}` : ''
    const [runs, tests] = await Promise.all([
      api.get<TestRunSummary[]>(`/api/projects/${projectId}/test-history/runs?take=50${branchParam}`),
      api.get<TestStats[]>(`/api/projects/${projectId}/test-history/tests?take=500${branchParam}`),
    ])
    runSummaries.value = runs
    allTests.value = tests
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
    const branch = branchFilter.value || undefined
    const branchParam = branch ? `&branch=${encodeURIComponent(branch)}` : ''
    testHistory.value = await api.get<TestCaseHistoryEntry[]>(
      `/api/projects/${projectId}/test-history/tests/${encodeURIComponent(test.fullName)}?take=50${branchParam}`,
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

onMounted(async () => {
  projectsStore.fetchProject(projectId)
  await reload()
})
</script>
